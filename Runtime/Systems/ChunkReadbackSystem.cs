using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Generation {
    // This MUST stay as a SystemBase since we have some AsyncGPUReadback stuff with delegates that keep a handle to the properties stored here
    [UpdateInGroup(typeof(FixedStepTerrainSystemGroup))]
    [UpdateAfter(typeof(ManagerSystem))]
    public partial class ReadbackSystem : SystemBase {
        private bool free;
        private NativeArray<uint> data;
        private List<Entity> entities;
        private JobHandle? pendingCopies;
        private NativeArray<JobHandle> copies;
        private NativeArray<int> counters;
        private bool countersFetched, voxelsFetched;
        private bool disposed;
        private OctalReadbackExecutor octalExecutor;

        protected override void OnCreate() {
            RequireForUpdate<TerrainReadbackConfig>();
            data = new NativeArray<uint>(VoxelUtils.VOLUME * VoxelUtils.OCTAL_CHUNK_COUNT, Allocator.Persistent);
            entities = new List<Entity>(VoxelUtils.OCTAL_CHUNK_COUNT);
            copies = new NativeArray<JobHandle>(VoxelUtils.OCTAL_CHUNK_COUNT, Allocator.Persistent);
            counters = new NativeArray<int>(VoxelUtils.OCTAL_CHUNK_COUNT, Allocator.Persistent);
            free = true;
            pendingCopies = null;
            voxelsFetched = false;
            countersFetched = false;
            disposed = false;

            octalExecutor = new OctalReadbackExecutor(VoxelUtils.SIZE * VoxelUtils.OCTAL_CHUNK_SIZE_RATIO);
        }

        private void Reset() {
            free = true;
            entities.Clear();
            pendingCopies = null;
            copies.AsSpan().Fill(default);
            voxelsFetched = false;
            countersFetched = false;
        }

        protected override void OnDestroy() {
            disposed = true;
            AsyncGPUReadback.WaitAllRequests();
            data.Dispose();
            entities.Clear();
            counters.Dispose();
            copies.Dispose();
            octalExecutor.DisposeResources();
        }

        protected override void OnUpdate() {
            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainChunkVoxels, TerrainChunk, TerrainChunkRequestReadbackTag>().Build();
            bool ready = query.CalculateEntityCount() == 0 && free;

            RefRW<TerrainReadySystems> _ready = SystemAPI.GetSingletonRW<TerrainReadySystems>();
            _ready.ValueRW.readback = ready;

            if (ManagedTerrain.instance == null) {
                throw new System.Exception("Missing managed terrain instance");
            }

            if (free) {
                TryBeginReadback();
            } else {
                TryCheckIfReadbackComplete();
            }
        }

        private void TryBeginReadback() {
            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainChunkVoxels, TerrainChunk, TerrainChunkRequestReadbackTag>().Build();
            NativeArray<TerrainChunkVoxels> voxelsArray = query.ToComponentDataArray<TerrainChunkVoxels>(Allocator.Temp);
            NativeArray<TerrainChunk> chunksArray = query.ToComponentDataArray<TerrainChunk>(Allocator.Temp);
            NativeArray<Entity> entitiesArray = query.ToEntityArray(Allocator.Temp);

            if (voxelsArray.Length == 0) {
                voxelsArray.Dispose();
                entitiesArray.Dispose();
                chunksArray.Dispose();
                return;
            }
                
            int numChunks = math.min(VoxelUtils.OCTAL_CHUNK_COUNT, voxelsArray.Length);

            OctalReadbackPosScaleData[] posScaleOctals = new OctalReadbackPosScaleData[VoxelUtils.OCTAL_CHUNK_COUNT];

            free = false;

            // Change chunk states, since we are now waiting for voxel readback
            entities.Clear();
            for (int j = 0; j < numChunks; j++) {
                TerrainChunk chunk = chunksArray[j];
                Entity entity = entitiesArray[j];
                entities.Add(entity);

                float3 pos = (float3)chunk.node.position;
                float scale = chunk.node.size / VoxelUtils.PHYSICAL_CHUNK_SIZE;

                posScaleOctals[j] = new OctalReadbackPosScaleData {
                    scale = scale,
                    position = pos
                };

                // Disable the tag component since we won't need to readback anymore
                EntityManager.SetComponentEnabled<TerrainChunkRequestReadbackTag>(entity, false);
                EntityManager.SetComponentEnabled<TerrainChunkVoxelsReadyTag>(entity, false);
                EntityManager.SetComponentEnabled<TerrainChunkEndOfPipeTag>(entity, false);
                EntityManager.SetComponentEnabled<TerrainChunkMeshReady>(entity, false);
            }

            // Size*4 since we are using octal generation!!!! (not really octal atp but wtv)
            OctalReadbackExecutorParameters parameters = new OctalReadbackExecutorParameters() {
                commandBufferName = "Terrain Readback System Async Dispatch",
                posScaleOctals = posScaleOctals,
                dispatchName = "voxels",
                updateInjected = false,
                compiler = ManagedTerrain.instance.compiler,
                seeder = ManagedTerrain.instance.seeder,
            };

            GraphicsFence fence = octalExecutor.Execute(parameters);
            CommandBuffer cmds = new CommandBuffer();
            cmds.name = "Terrain Readback System Async Readback";
            cmds.WaitOnAsyncGraphicsFence(fence, SynchronisationStageFlags.ComputeProcessing);

            // Request GPU data into the native array we allocated at the start
            // When we get it back, start off multiple memcpy jobs that we can wait for the next tick
            // This avoids waiting on the memory copies and can spread them out on many threads
            NativeArray<uint> voxelData = data;
            cmds.RequestAsyncReadbackIntoNativeArray(
                ref voxelData,
                octalExecutor.Buffers["voxels"],
                delegate (AsyncGPUReadbackRequest asyncRequest) {
                    unsafe {
                        if (disposed) {
                            return;
                        }

                        // We have to do this to stop unity from complaining about using the data...
                        // fuck you...
                        uint* pointer = (uint*)NativeArrayUnsafeUtility.GetUnsafePtr<uint>(data);

                        // Start doing the memcpy asynchronously...
                        for (int j = 0; j < entities.Count; j++) {
                            Entity entity = entities[j];

                            // Since we are using a buffer where the data for chunks is contiguous
                            // we can just do parallel copies from the source buffer at the appropriate offset
                            uint* src = pointer + (VoxelUtils.VOLUME * j);

                            uint* dst = (uint*) EntityManager.GetComponentData<TerrainChunkVoxels>(entity).inner.GetUnsafePtr();
                            copies[j] = new UnsafeAsyncMemCpy {
                                src = (void*)src,
                                dst = (void*)dst,
                                byteSize = Voxel.size * VoxelUtils.VOLUME,
                            }.Schedule();
                        }

                        pendingCopies = JobHandle.CombineDependencies(copies.Slice(0, entities.Count));
                        voxelsFetched = true;
                    }
                }
            );

            cmds.RequestAsyncReadbackIntoNativeArray(
                ref counters,
                octalExecutor.OctalCountersBuffer,
                delegate (AsyncGPUReadbackRequest asyncRequest) {
                    if (disposed) {
                        return;
                    }

                    countersFetched = true;
                }
            );

            Graphics.ExecuteCommandBuffer(cmds);

            voxelsArray.Dispose();
            entitiesArray.Dispose();
            chunksArray.Dispose();
        }

        private void TryCheckIfReadbackComplete() {
            if (pendingCopies.HasValue && pendingCopies.Value.IsCompleted && countersFetched && voxelsFetched) {
                pendingCopies.Value.Complete();

                bool skipEmptyChunks = SystemAPI.GetSingleton<TerrainReadbackConfig>().skipEmptyChunks;

                // Since we now fetch n+2 voxels (66^3) we can actually use the pos/neg optimizations
                // to check early if we need to do any meshing for a chunk whose voxels are from the GPU!
                // heheheha....
                for (int j = 0; j < entities.Count; j++) {
                    int count = counters[j];
                    Entity entity = entities[j];

                    int max = VoxelUtils.VOLUME;
                    bool skipped = count == max || count == -max;

                    // Voxel data is always ready no matter what
                    EntityManager.SetComponentEnabled<TerrainChunkVoxelsReadyTag>(entity, true);

                    // Skip empty chunks!!!
                    if (skipEmptyChunks && skipped) {
                        EntityManager.SetComponentEnabled<TerrainChunkRequestMeshingTag>(entity, false);
                        EntityManager.SetComponentEnabled<TerrainChunkMeshReady>(entity, false);

                        // this chunk will directly go to the end of pipe, no need to deal with it anymore
                        EntityManager.SetComponentEnabled<TerrainChunkEndOfPipeTag>(entity, true);
                    } else {
                        EntityManager.SetComponentEnabled<TerrainChunkRequestMeshingTag>(entity, true);
                        EntityManager.SetComponentEnabled<TerrainChunkMeshReady>(entity, false);
                    }
                }

                Reset();
            }
        }
    }
}
