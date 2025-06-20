using System;
using System.Collections.Generic;
using System.Linq;
using jedjoud.VoxelTerrain.Generation;
using jedjoud.VoxelTerrain.Octree;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Meshing {
    [UpdateInGroup(typeof(TerrainFixedStepSystemGroup))]
    [UpdateAfter(typeof(TerrainMeshingSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial class TerrainLightingSystem : SystemBase {
        class LightingHandler {
            public Mesh mesh;
            public UnsafePtrList<half> densityDataPtrs;
            //public NativeArray<half> combinedDensities;
            public JobHandle jobHandle;
            public Mesh.MeshDataArray meshDataArray;
            public bool Free => jobHandle.IsCompleted && mesh == null;
            private NativeArray<float3> vertices;
            private NativeArray<float3> normals;

            public LightingHandler() {
                densityDataPtrs = new UnsafePtrList<half>(27, Allocator.Persistent);

                mesh = null;
                //combinedDensities = new NativeArray<half>(VoxelUtils.VOLUME * 27, Allocator.Persistent);
                jobHandle = default;
            }

            public unsafe void Begin(BitField32 neighbourMask, TerrainChunkMesh chunkMesh, half*[] densityPtrs) {
                densityDataPtrs.Clear();
                for (int i = 0; i < 27; i++) {
                    densityDataPtrs.Add(densityPtrs[i]);
                }

                Mesh.MeshData data = meshDataArray[0];
                NativeArray<float4> colours = data.GetVertexData<float4>(2);
                colours.AsSpan().Fill(0.2f);

                vertices = new NativeArray<float3>(chunkMesh.vertices.Length, Allocator.Persistent);
                vertices.CopyFrom(chunkMesh.vertices);

                normals = new NativeArray<float3>(chunkMesh.normals.Length, Allocator.Persistent);
                normals.CopyFrom(chunkMesh.normals);

                AoJob job = new AoJob() {
                    strength = 1f,
                    globalSpread = 2f,
                    globalOffset = 0.5f,
                    minDotNormal = 0.5f,
                    neighbourMask = neighbourMask,
                    vertices = vertices,
                    normals = normals,
                    uvs = colours,
                    densityDataPtrs = densityDataPtrs,
                };

                jobHandle = job.Schedule(colours.Length, BatchUtils.VERTEX_BATCH);
            }

            public void Complete() {
                jobHandle.Complete();

                if (mesh != null) {
                    Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);
                    vertices.Dispose();
                    normals.Dispose();
                }

                mesh = null;
                jobHandle = default;
            }

            public void Dispose() {
                jobHandle.Complete();
                //combinedDensities.Dispose();
                densityDataPtrs.Dispose();
            }
        }

        private EntitiesGraphicsSystem graphics;
        
        private List<LightingHandler> handlers;
        const int MAX_LIGHTING_HANDLES_PER_TICK = 1;

        protected override void OnCreate() {
            handlers = new List<LightingHandler>();

            for (int i = 0; i < MAX_LIGHTING_HANDLES_PER_TICK; i++) {
                handlers.Add(new LightingHandler());
            }
        }

        public bool TryCheckShouldCalculateLighting(Entity entity, TerrainManager manager, out NativeArray<Entity> entities) {
            TerrainChunk chunk = SystemAPI.GetComponent<TerrainChunk>(entity);
            OctreeNode self = chunk.node;
            BitField32 mask = chunk.neighbourMask;

            entities = new NativeArray<Entity>(27, Allocator.Temp);
            entities.FillArray(Entity.Null);

            for (int j = 0; j < 27; j++) {
                
                uint3 _offset = VoxelUtils.IndexToPos(j, 3);
                int3 offset = (int3)_offset - 1;

                if (math.all(offset == int3.zero)) {
                    entities[j] = entity;
                    continue;
                }

                if (mask.IsSet(j)) {
                    OctreeNode neighbourNode = new OctreeNode {
                        size = self.size,
                        childBaseIndex = -1,
                        depth = self.depth,

                        // doesn't matter since we don't consider this in the hash/equality check!!!
                        index = -1,
                        parentIndex = -1,

                        position = self.position + offset * self.size,
                    };

                    if (manager.chunks.TryGetValue(neighbourNode, out var neighbourChunk)) {
                        if (SystemAPI.IsComponentEnabled<TerrainChunkVoxels>(neighbourChunk)) {
                            entities[j] = neighbourChunk;
                        } else {
                            return false;
                        }
                    } else {
                        return false;
                    }
                }
            }

            return true;
        }

        protected override void OnUpdate() {
            if (graphics == null) {
                graphics = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            }

            foreach (var handler in handlers) {
                if (handler.jobHandle.IsCompleted) {
                    handler.Complete();
                }
            }

            TerrainManager manager = SystemAPI.GetSingleton<TerrainManager>();

            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainChunkRequestLightingTag, TerrainChunk, TerrainChunkVoxels, TerrainChunkVoxelsReadyTag, TerrainChunkEndOfPipeTag>().WithPresent<MaterialMeshInfo>().Build();
            NativeArray<Entity> entitiesArray = query.ToEntityArray(Allocator.Temp);

            LightingHandler[] freeHandlers = handlers.AsEnumerable().Where(x => x.Free).ToArray();
            int numChunksToProcess = math.min(freeHandlers.Length, entitiesArray.Length);

            for (int i = 0; i < numChunksToProcess; i++) {
                LightingHandler handler = freeHandlers[i];
                Entity chunkEntity = entitiesArray[i];
                TerrainChunk chunk = SystemAPI.GetComponent<TerrainChunk>(chunkEntity);
                TerrainChunkMesh chunkMesh = SystemAPI.GetComponent<TerrainChunkMesh>(chunkEntity);

                if (TryCheckShouldCalculateLighting(chunkEntity, manager, out NativeArray<Entity> chunks)) {
                    SystemAPI.SetComponentEnabled<TerrainChunkRequestLightingTag>(chunkEntity, false);
                    MaterialMeshInfo materialMeshInfo = SystemAPI.GetComponent<MaterialMeshInfo>(chunkEntity);
                    Mesh mesh = graphics.GetMesh(materialMeshInfo.MeshID);
                    Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(mesh);

                    unsafe {
                        half*[] densityPtrs = new half*[27];

                        for (int j = 0; j < 27; j++) {
                            if (EntityManager.Exists(chunks[j])) {
                                TerrainChunkVoxels voxels = SystemAPI.GetComponent<TerrainChunkVoxels>(chunks[j]);

                                // TODO: remove this; add it as a scheduling dep instead
                                voxels.asyncWriteJobHandle.Complete();

                                densityPtrs[j] = (half*)voxels.data.densities.GetUnsafeReadOnlyPtr();
                            } else {
                                densityPtrs[j] = (half*)IntPtr.Zero;
                            }
                        }

                        handler.mesh = mesh;
                        handler.meshDataArray = meshDataArray;
                        handler.Begin(chunk.neighbourMask, chunkMesh, densityPtrs);
                    }
                }

            }
        }

        protected override void OnDestroy() {
            foreach (var handler in handlers) {
                handler.Dispose();
            }
        }
    }
}