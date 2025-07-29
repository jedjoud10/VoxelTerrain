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
    [UpdateInGroup(typeof(TerrainFixedStepSystemGroup))]
    [UpdateAfter(typeof(TerrainManagerSystem))]
    public partial class TerrainReadbackSystem : SystemBase {
        private bool free;
        private NativeArray<GpuVoxel> multiData;
        private List<Entity> entities;
        private JobHandle? pendingCopies;
        private NativeArray<JobHandle> copies;
        private NativeArray<int> multiSignCounters;
        private bool countersFetched, voxelsFetched;
        private bool disposed;
        private MultiReadbackExecutor multiExecutor;
        private ComputeBuffer multiSignCountersBuffer;

        protected override void OnCreate() {
            RequireForUpdate<TerrainReadbackConfig>();
            RequireForUpdate<TerrainReadySystems>();
            multiData = new NativeArray<GpuVoxel>(VoxelUtils.VOLUME * VoxelUtils.MULTI_READBACK_CHUNK_COUNT, Allocator.Persistent);
            entities = new List<Entity>(VoxelUtils.MULTI_READBACK_CHUNK_COUNT);
            copies = new NativeArray<JobHandle>(VoxelUtils.MULTI_READBACK_CHUNK_COUNT, Allocator.Persistent);
            multiSignCounters = new NativeArray<int>(VoxelUtils.MULTI_READBACK_CHUNK_COUNT, Allocator.Persistent);
            free = true;
            pendingCopies = null;
            voxelsFetched = false;
            countersFetched = false;
            disposed = false;

            multiExecutor = new MultiReadbackExecutor();
            multiSignCountersBuffer = new ComputeBuffer(VoxelUtils.MULTI_READBACK_CHUNK_COUNT, sizeof(int), ComputeBufferType.Structured);
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
            multiData.Dispose();
            entities.Clear();
            multiSignCounters.Dispose();
            copies.Dispose();
            multiExecutor.DisposeResources();
            multiSignCountersBuffer.Dispose();
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
                return;
            }
                
            int numChunks = math.min(VoxelUtils.MULTI_READBACK_CHUNK_COUNT, voxelsArray.Length);

            MultiReadbackTransform[] posScaleOctals = new MultiReadbackTransform[VoxelUtils.MULTI_READBACK_CHUNK_COUNT];

            free = false;

            // Change chunk states, since we are now waiting for voxel readback
            entities.Clear();
            for (int j = 0; j < numChunks; j++) {
                TerrainChunk chunk = chunksArray[j];
                Entity entity = entitiesArray[j];
                entities.Add(entity);

                float3 pos = (float3)chunk.node.position;
                float scale = chunk.node.size / VoxelUtils.PHYSICAL_CHUNK_SIZE;

                posScaleOctals[j] = new MultiReadbackTransform {
                    scale = scale,
                    position = pos
                };

                // Disable the tag component since we won't need to readback anymore
                EntityManager.SetComponentEnabled<TerrainChunkRequestReadbackTag>(entity, false);
            }

            // Size*4 since we are using octal generation!!!! (not really octal atp but wtv)
            MultiReadbackExecutorParameters parameters = new MultiReadbackExecutorParameters() {
                commandBufferName = "Terrain Readback System Async Dispatch",
                transforms = posScaleOctals,
                kernelName = "CSVoxels",
                updateInjected = false,
                compiler = ManagedTerrain.instance.compiler,
                seeder = ManagedTerrain.instance.seeder,
                multiSignCountersBuffer = multiSignCountersBuffer,
            };

            GraphicsFence fence = multiExecutor.Execute(parameters);
            CommandBuffer cmds = new CommandBuffer();
            cmds.name = "Terrain Readback System Async Readback";
            cmds.WaitOnAsyncGraphicsFence(fence, SynchronisationStageFlags.ComputeProcessing);

            // Request GPU data into the native array we allocated at the start
            // When we get it back, start off multiple memcpy jobs that we can wait for the next tick
            // This avoids waiting on the memory copies and can spread them out on many threads
            NativeArray<GpuVoxel> voxelData = multiData;
            cmds.RequestAsyncReadbackIntoNativeArray(
                ref voxelData,
                multiExecutor.Buffers["voxels"],
                delegate (AsyncGPUReadbackRequest asyncRequest) {
                    unsafe {
                        if (disposed)
                            return;

                        // We have to do this to stop unity from complaining about using the data...
                        // fuck you...
                        GpuVoxel* pointer = (GpuVoxel*)NativeArrayUnsafeUtility.GetUnsafePtr<GpuVoxel>(multiData);

                        // Start doing the memcpy asynchronously...
                        for (int j = 0; j < entities.Count; j++) {
                            Entity entity = entities[j];

                            // Since we are using a buffer where the data for chunks is contiguous
                            // we can just do parallel copies from the source buffer at the appropriate offset
                            GpuVoxel* src = pointer + (VoxelUtils.VOLUME * j);

                            bool enabled = EntityManager.IsComponentEnabled<TerrainChunkVoxels>(entity);

                            if (!enabled)
                                return;

                            RefRW<TerrainChunkVoxels> _voxels = SystemAPI.GetComponentRW<TerrainChunkVoxels>(entity);
                            ref TerrainChunkVoxels voxels = ref _voxels.ValueRW;

                            JobHandle dep = JobHandle.CombineDependencies(voxels.asyncReadJobHandle, voxels.asyncWriteJobHandle);
                            JobHandle handle = new GpuToCpuCopy {
                                cpuData = voxels.data,
                                rawGpuData = src,
                            }.Schedule(VoxelUtils.VOLUME, BatchUtils.HALF_BATCH, dep);

                            copies[j] = handle;
                            voxels.asyncWriteJobHandle = handle;
                        }

                        pendingCopies = JobHandle.CombineDependencies(copies.Slice(0, entities.Count));
                        voxelsFetched = true;
                    }
                }
            );

            cmds.RequestAsyncReadbackIntoNativeArray(
                ref multiSignCounters,
                multiSignCountersBuffer,
                delegate (AsyncGPUReadbackRequest asyncRequest) {
                    if (disposed)
                        return;
                    

                    countersFetched = true;
                }
            );

            Graphics.ExecuteCommandBuffer(cmds);
        }

        private void TryCheckIfReadbackComplete() {
            if (pendingCopies.HasValue && pendingCopies.Value.IsCompleted && countersFetched && voxelsFetched) {
                pendingCopies.Value.Complete();

                // Since we now fetch n+2 voxels (66^3) we can actually use the pos/neg optimizations
                // to check early if we need to do any meshing for a chunk whose voxels are from the GPU!
                // heheheha....
                for (int j = 0; j < entities.Count; j++) {
                    int count = multiSignCounters[j];
                    Entity entity = entities[j];

                    int max = VoxelUtils.VOLUME;
                    bool empty = count == max || count == -max;
                    bool skipIfEmpty = EntityManager.GetComponentData<TerrainChunkRequestReadbackTag>(entity).skipMeshingIfEmpty;
                    
                    // Voxel data is always ready no matter what
                    EntityManager.SetComponentEnabled<TerrainChunkVoxelsReadyTag>(entity, true);

                    // Skip empty chunks!!!
                    if (empty && skipIfEmpty) {
                        EntityManager.SetComponentEnabled<TerrainChunkRequestMeshingTag>(entity, false);

                        // this chunk will directly go to the end of pipe, no need to deal with it anymore
                        EntityManager.SetComponentEnabled<TerrainChunkEndOfPipeTag>(entity, true);
                    } else {
                        EntityManager.SetComponentEnabled<TerrainChunkRequestMeshingTag>(entity, true);
                    }
                }

                Reset();
            }
        }
    }
}
