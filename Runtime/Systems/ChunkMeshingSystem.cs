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
    [UpdateAfter(typeof(ReadbackSystem))]
    public partial class MeshingSystem : SystemBase {
        private List<MeshJobHandler> handlers;
        const int MESH_JOBS_PER_TICK = 2;
        private RenderMeshDescription mainMeshDescription;
        private RenderMeshDescription skirtsMeshDescription;
        private EntitiesGraphicsSystem graphics;

        private BatchMaterialID mainMeshMaterialId;
        private BatchMaterialID skirtMeshMaterialId;

        protected override void OnCreate() {
            RequireForUpdate<TerrainMesherConfig>();
            handlers = new List<MeshJobHandler>(MESH_JOBS_PER_TICK);
            for (int i = 0; i < MESH_JOBS_PER_TICK; i++) {
                handlers.Add(new MeshJobHandler());
            }

            mainMeshDescription = new RenderMeshDescription {
                FilterSettings = new RenderFilterSettings {
                    ShadowCastingMode = ShadowCastingMode.TwoSided,
                    ReceiveShadows = true,
                    MotionMode = MotionVectorGenerationMode.ForceNoMotion,
                    StaticShadowCaster = false,
                    Layer = 0,
                    RenderingLayerMask = ~0u,
                },
                LightProbeUsage = LightProbeUsage.Off,
            };

            skirtsMeshDescription = new RenderMeshDescription {
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
                Material mainMeshMaterial = new Material(config.material.material);

                Material skirtMeshMaterial = new Material(config.material.material);

                LocalKeyword keyword = skirtMeshMaterial.shader.keywordSpace.FindKeyword("_SKIRT");
                skirtMeshMaterial.SetKeyword(keyword, true);

                graphics = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
                mainMeshMaterialId = graphics.RegisterMaterial(mainMeshMaterial);
                skirtMeshMaterialId = graphics.RegisterMaterial(skirtMeshMaterial);
            }

            foreach (var handler in handlers) {

                if (handler.IsComplete(EntityManager)) {
                    Profiler.BeginSample("Finish Mesh Jobs");
                    FinishJob(handler);
                    Profiler.EndSample();
                }
            }

            NativeArray<Entity> entitiesArray = query.ToEntityArray(Allocator.Temp);

            MeshJobHandler[] freeHandlers = handlers.AsEnumerable().Where(x => x.Free).ToArray();
            int numChunksToProcess = math.min(freeHandlers.Length, entitiesArray.Length);

            if (numChunksToProcess == 0) {
                return;
            }

            for (int i = 0; i < numChunksToProcess; i++) {
                MeshJobHandler handler = freeHandlers[i];
                Entity chunkEntity = entitiesArray[i];

                RefRW<TerrainChunkVoxels> _voxels = SystemAPI.GetComponentRW<TerrainChunkVoxels>(chunkEntity);

                Profiler.BeginSample("Begin Mesh Jobs");
                handler.BeginJob(chunkEntity, ref _voxels.ValueRW, default);
                Profiler.EndSample();

                SystemAPI.SetComponentEnabled<TerrainChunkEndOfPipeTag>(chunkEntity, false);
                SystemAPI.SetComponentEnabled<TerrainChunkRequestMeshingTag>(chunkEntity, false);
            }

            entitiesArray.Dispose();
        }

        private void FinishJob(MeshJobHandler handler) {
            if (handler.TryComplete(EntityManager, out Mesh mesh, out Entity chunkEntity, out MeshJobHandler.Stats stats)) {
                EntityManager.SetComponentEnabled<TerrainChunkEndOfPipeTag>(chunkEntity, true);

                if (stats.empty)
                    return;

                // we MUST take a copy of TerrainChunk here (we cannot use GetComponentRW)
                // because we execute RenderMeshUtility.AddComponents right after
                // RenderMeshUtility.AddComponents changes the archetype of the chunk entity, invalidating the reference we hold to TerrainChunk
                TerrainChunk chunk = SystemAPI.GetComponent<TerrainChunk>(chunkEntity);
                OctreeNode node = chunk.node;
                
                BatchMeshID meshId = graphics.RegisterMesh(mesh);
                MaterialMeshInfo materialMeshInfo = new MaterialMeshInfo(mainMeshMaterialId, meshId, 0);

                float scalingFactor = node.size / VoxelUtils.PHYSICAL_CHUNK_SIZE;
                AABB localRenderBounds = new MinMaxAABB {
                    Min = stats.bounds.min,
                    Max = stats.bounds.max,
                };

                AABB worldRenderBounds = localRenderBounds;
                worldRenderBounds.Center += (float3)node.position;
                worldRenderBounds.Extents *= scalingFactor;

                if (stats.indexCount > 0) {

                    EntityManager.SetComponentEnabled<TerrainChunkRequestCollisionTag>(chunkEntity, chunk.generateCollisions);
                    RenderMeshUtility.AddComponents(chunkEntity, EntityManager, mainMeshDescription, materialMeshInfo);

                    EntityManager.SetComponentEnabled<MaterialMeshInfo>(chunkEntity, false);

                    NativeArray<float3> vertices = new NativeArray<float3>(stats.vertices.Length, Allocator.Persistent);
                    NativeArray<int> indices = new NativeArray<int>(stats.indices.Length, Allocator.Persistent);

                    vertices.CopyFrom(stats.vertices);
                    indices.CopyFrom(stats.indices);

                    EntityManager.SetComponentEnabled<TerrainChunkMesh>(chunkEntity, true);
                    EntityManager.SetComponentData<TerrainChunkMesh>(chunkEntity, new TerrainChunkMesh {
                        vertices = vertices,
                        indices = indices,
                        meshId = meshId,
                    });

                    EntityManager.SetComponentData<RenderBounds>(chunkEntity, new RenderBounds() {
                        Value = localRenderBounds,
                    });

                    EntityManager.SetComponentData<WorldRenderBounds>(chunkEntity, new WorldRenderBounds() {
                        Value = worldRenderBounds
                    });
                }

                for (int skirtIndex = 0; skirtIndex < 6; skirtIndex++) {
                    if (stats.forcedSkirtFacesTriCount[skirtIndex] == 0)
                        continue;
                    
                    Entity skirtEntity = chunk.skirts[skirtIndex];

                    MaterialMeshInfo skirtMaterialMeshInfo = new MaterialMeshInfo(skirtMeshMaterialId, meshId, (ushort)(skirtIndex + 1));
                    RenderMeshUtility.AddComponents(skirtEntity, EntityManager, skirtsMeshDescription, skirtMaterialMeshInfo);

                    EntityManager.SetComponentEnabled<MaterialMeshInfo>(skirtEntity, false);

                    EntityManager.SetComponentData<RenderBounds>(skirtEntity, new RenderBounds() {
                        Value = localRenderBounds,
                    });

                    EntityManager.SetComponentData<WorldRenderBounds>(skirtEntity, new WorldRenderBounds() {
                        Value = worldRenderBounds
                    });
                }
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