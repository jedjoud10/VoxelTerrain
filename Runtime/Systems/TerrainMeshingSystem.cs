using System.Collections.Generic;
using System.Linq;
using jedjoud.VoxelTerrain.Generation;
using jedjoud.VoxelTerrain.Octree;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Meshing {
    [UpdateInGroup(typeof(FixedStepTerrainSystemGroup))]
    [UpdateAfter(typeof(TerrainReadbackSystem))]
    public partial class TerrainMeshingSystem : SystemBase {
        private List<MeshJobHandler> handlers;
        const int MESH_JOBS_PER_TICK = 2;
        private RenderMeshDescription meshDescription;
        private RenderMeshDescription mainSkirtsDescription;
        private RenderMeshDescription forcedSkirtsDescription;
        private EntitiesGraphicsSystem graphics;
        private BatchMaterialID materialId;

        protected override void OnCreate() {
            RequireForUpdate<TerrainMesherConfig>();
            handlers = new List<MeshJobHandler>(MESH_JOBS_PER_TICK);
            for (int i = 0; i < MESH_JOBS_PER_TICK; i++) {
                handlers.Add(new MeshJobHandler());
            }

            meshDescription = new RenderMeshDescription {
                FilterSettings = new RenderFilterSettings {
                    ShadowCastingMode = ShadowCastingMode.On,
                    ReceiveShadows = true,
                    MotionMode = MotionVectorGenerationMode.ForceNoMotion,
                    StaticShadowCaster = false,
                    Layer = 0,
                    RenderingLayerMask = ~0u,
                },
                LightProbeUsage = LightProbeUsage.Off,
            };

            mainSkirtsDescription = new RenderMeshDescription {
                FilterSettings = new RenderFilterSettings {
                    ShadowCastingMode = ShadowCastingMode.On,
                    ReceiveShadows = true,
                    MotionMode = MotionVectorGenerationMode.ForceNoMotion,
                    StaticShadowCaster = false,
                    Layer = 0,
                    RenderingLayerMask = ~0u,
                },
                LightProbeUsage = LightProbeUsage.Off,
            };

            forcedSkirtsDescription = new RenderMeshDescription {
                FilterSettings = new RenderFilterSettings {
                    ShadowCastingMode = ShadowCastingMode.Off,
                    ReceiveShadows = false,
                    MotionMode = MotionVectorGenerationMode.ForceNoMotion,
                    StaticShadowCaster = false,
                    Layer = 0,
                    RenderingLayerMask = ~0u,
                },
                LightProbeUsage = LightProbeUsage.Off,
            };

            graphics = null;
        }

        protected override void OnUpdate() {
            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainChunk, TerrainChunkVoxels, TerrainChunkRequestMeshingTag, TerrainChunkVoxelsReadyTag>().Build();
            bool ready = query.CalculateEntityCount() == 0 && handlers.All(x => x.Free);

            RefRW<TerrainReadySystems> _ready = SystemAPI.GetSingletonRW<TerrainReadySystems>();
            _ready.ValueRW.mesher = ready;

            if (SystemAPI.ManagedAPI.TryGetSingleton<TerrainMesherConfig>(out TerrainMesherConfig config) && graphics == null) {
                graphics = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
                materialId = graphics.RegisterMaterial(config.material.material);
            }

            foreach (var handler in handlers) {

                if (handler.IsComplete(EntityManager)) {
                    Profiler.BeginSample("Finish Mesh Jobs");
                    FinishJob(handler);
                    Profiler.EndSample();
                }
            }

            NativeArray<TerrainChunkVoxels> voxelsArray = query.ToComponentDataArray<TerrainChunkVoxels>(Allocator.Temp);
            NativeArray<Entity> entitiesArray = query.ToEntityArray(Allocator.Temp);

            MeshJobHandler[] freeHandlers = handlers.AsEnumerable().Where(x => x.Free).ToArray();
            int numChunksToProcess = math.min(freeHandlers.Length, entitiesArray.Length);

            if (numChunksToProcess == 0) {
                voxelsArray.Dispose();
                entitiesArray.Dispose();
                return;
            }


            //Debug.Log(numChunksToProcess);

            for (int i = 0; i < numChunksToProcess; i++) {
                MeshJobHandler handler = freeHandlers[i];
                Entity chunkEntity = entitiesArray[i];
                NativeArray<Voxel> voxels = voxelsArray[i].inner;

                Profiler.BeginSample("Begin Mesh Jobs");
                handler.BeginJob(chunkEntity, voxels, default);
                Profiler.EndSample();

                SystemAPI.SetComponentEnabled<TerrainChunkEndOfPipeTag>(chunkEntity, false);
                SystemAPI.SetComponentEnabled<TerrainChunkRequestMeshingTag>(chunkEntity, false);
            }

            voxelsArray.Dispose();
            entitiesArray.Dispose();
        }

        private void FinishJob(MeshJobHandler handler) {
            if (handler.TryComplete(EntityManager, out Mesh mesh, out Entity chunkEntity, out MeshJobHandler.Stats stats)) {
                EntityManager.SetComponentEnabled<TerrainChunkEndOfPipeTag>(chunkEntity, true);

                if (stats.empty)
                    return;

                {
                    EntityManager.SetComponentEnabled<TerrainChunkMeshReady>(chunkEntity, true);
                    NativeArray<float3> vertices = new NativeArray<float3>(stats.vertices.Length, Allocator.Persistent);
                    NativeArray<int> indices = new NativeArray<int>(stats.indices.Length, Allocator.Persistent);

                    vertices.CopyFrom(stats.vertices);
                    indices.CopyFrom(stats.indices);

                    EntityManager.SetComponentData<TerrainChunkMeshReady>(chunkEntity, new TerrainChunkMeshReady {
                        vertices = vertices,
                        indices = indices
                    });
                }

                TerrainChunk chunk = EntityManager.GetComponentData<TerrainChunk>(chunkEntity);
                OctreeNode node = chunk.node;

                EntityManager.SetComponentEnabled<TerrainChunkRequestCollisionTag>(chunkEntity, chunk.generateCollisions);

                TerrainMesherConfig config = SystemAPI.ManagedAPI.GetSingleton<TerrainMesherConfig>();

                BatchMeshID meshId = graphics.RegisterMesh(mesh);
                MaterialMeshInfo materialMeshInfo = new MaterialMeshInfo(materialId, meshId, 0);

                RenderMeshUtility.AddComponents(chunkEntity, EntityManager, meshDescription, materialMeshInfo);

                EntityManager.AddComponent<UnregisterMeshCleanup>(chunkEntity);

                EntityManager.SetComponentData<UnregisterMeshCleanup>(chunkEntity, new UnregisterMeshCleanup {
                    meshId = meshId
                });

                float scalingFactor = node.size / (64f);
                AABB localRenderBounds = new Unity.Mathematics.MinMaxAABB {
                    Min = stats.bounds.min,
                    Max = stats.bounds.max,
                };

                AABB worldRenderBounds = localRenderBounds;
                worldRenderBounds.Center += (float3)node.position;
                worldRenderBounds.Extents *= scalingFactor;

                EntityManager.SetComponentData<RenderBounds>(chunkEntity, new RenderBounds() {
                    Value = localRenderBounds,
                });

                EntityManager.SetComponentData<WorldRenderBounds>(chunkEntity, new WorldRenderBounds() {
                    Value = worldRenderBounds
                });

                /*
                AABB skirtLocalRenderBounds = localRenderBounds;
                skirtLocalRenderBounds.Extents *= 1.1f;

                AABB skirtWorldRenderBounds = skirtLocalRenderBounds;
                worldRenderBounds.Center += (float3)node.position;
                worldRenderBounds.Extents *= scalingFactor;

                for (int skirtIndex = 0; skirtIndex < 7; skirtIndex++) {
                    if (skirtIndex > 1 && stats.forcedSkirtFacesTriCount[skirtIndex - 1] == 0)
                        continue;
                    
                    Entity skirtEntity = chunk.skirts[skirtIndex];

                    BatchMeshID skirtMeshId = graphics.RegisterMesh(skirtMesh);
                    MaterialMeshInfo skirtMaterialMeshInfo = new MaterialMeshInfo(materialId, skirtMeshId, (ushort)skirtIndex);

                    RenderMeshDescription skirtDescription = skirtIndex == 0 ? mainSkirtsDescription : forcedSkirtsDescription;
                    RenderMeshUtility.AddComponents(skirtEntity, EntityManager, skirtDescription, skirtMaterialMeshInfo);

                    EntityManager.SetComponentData<RenderBounds>(skirtEntity, new RenderBounds() {
                        Value = skirtLocalRenderBounds,
                    });

                    EntityManager.SetComponentData<WorldRenderBounds>(skirtEntity, new WorldRenderBounds() {
                        Value = skirtWorldRenderBounds
                    });
                }
                */
            }
        }

        protected override void OnDestroy() {
            foreach (MeshJobHandler handler in handlers) {
                handler.Dispose();
            }

            handlers.Clear();
        }
    }
}