using System.Collections.Generic;
using System.Linq;
using jedjoud.VoxelTerrain.Generation;
using jedjoud.VoxelTerrain.Octree;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Meshing {
    [UpdateInGroup(typeof(TerrainFixedStepSystemGroup))]
    [UpdateAfter(typeof(TerrainReadbackSystem))]
    public partial class TerrainMeshingSystem : SystemBase {
        private List<MeshJobHandler> handlers;
        const int MAX_MESH_HANDLERS_PER_TICK = 2;
        public static readonly RenderMeshDescription renderMeshDescription = new RenderMeshDescription {
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
        private EntitiesGraphicsSystem graphics;

        private BatchMaterialID mainMeshMaterialId;
        private BatchMaterialID skirtMeshMaterialId;

        protected override void OnCreate() {
            RequireForUpdate<TerrainMesherConfig>();
            handlers = new List<MeshJobHandler>(MAX_MESH_HANDLERS_PER_TICK);
            for (int i = 0; i < MAX_MESH_HANDLERS_PER_TICK; i++) {
                handlers.Add(new MeshJobHandler());
            }

            graphics = null;
        }

        protected override void OnUpdate() {
            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainChunk, TerrainChunkVoxels, TerrainChunkRequestMeshingTag, TerrainChunkVoxelsReadyTag>().Build();
            bool ready = query.IsEmpty && handlers.All(x => x.Free);

            RefRW<TerrainReadySystems> _ready = SystemAPI.GetSingletonRW<TerrainReadySystems>();
            _ready.ValueRW.mesher = ready;

            if (SystemAPI.ManagedAPI.TryGetSingleton<TerrainMesherConfig>(out TerrainMesherConfig config) && graphics == null) {
                Material mainMeshMaterial, skirtMeshMaterial;
                mainMeshMaterial = skirtMeshMaterial = config.material;
                    
                if (config.createCopyMaterial) {
                    mainMeshMaterial = new Material(config.material);
                    skirtMeshMaterial = new Material(config.material);

                    LocalKeyword keyword = skirtMeshMaterial.shader.keywordSpace.FindKeyword("_SKIRT");
                    skirtMeshMaterial.SetKeyword(keyword, true);
                }

                graphics = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
                mainMeshMaterialId = graphics.RegisterMaterial(mainMeshMaterial);
                skirtMeshMaterialId = graphics.RegisterMaterial(skirtMeshMaterial);
            }

            foreach (var handler in handlers) {

                if (handler.IsComplete(EntityManager)) {
                    Profiler.BeginSample("Complete Mesh Handler Jobs");
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

                Profiler.BeginSample("Begin Mesh Handler Jobs");
                handler.BeginJob(chunkEntity, ref _voxels.ValueRW, EntityManager);
                Profiler.EndSample();

                SystemAPI.SetComponentEnabled<TerrainChunkEndOfPipeTag>(chunkEntity, false);
                SystemAPI.SetComponentEnabled<TerrainChunkRequestMeshingTag>(chunkEntity, false);
            }
        }

        private void FinishJob(MeshJobHandler handler) {
            if (handler.TryComplete(EntityManager, out Mesh mesh, out Entity chunkEntity, out MeshJobHandler.Stats stats)) {

                SystemAPI.SetComponentEnabled<TerrainChunkEndOfPipeTag>(chunkEntity, true);
                // we MUST take a copy of TerrainChunk here (we cannot use GetComponentRW)
                // because we execute RenderMeshUtility.AddComponents right after
                // RenderMeshUtility.AddComponents is a structural change, invalidating the reference we hold to TerrainChunk
                TerrainChunk chunk = SystemAPI.GetComponent<TerrainChunk>(chunkEntity);

                if (stats.empty) {
                    if (SystemAPI.HasComponent<MaterialMeshInfo>(chunkEntity)) {
                        SystemAPI.SetComponentEnabled<MaterialMeshInfo>(chunkEntity, false);
                        SystemAPI.SetComponentEnabled<TerrainDeferredVisible>(chunkEntity, false);
                    }
                    return;
                }

                if (SystemAPI.HasComponent<TerrainChunkMesh>(chunkEntity)) {
                    DynamicBuffer<TerrainUnregisterMeshBuffer> unregisterBuffer = SystemAPI.GetSingletonBuffer<TerrainUnregisterMeshBuffer>();
                    TerrainChunkMesh tmpMesh = SystemAPI.GetComponent<TerrainChunkMesh>(chunkEntity);
                    tmpMesh.Dispose();
                    
                    if (SystemAPI.HasComponent<MaterialMeshInfo>(chunkEntity)) {
                        MaterialMeshInfo matMeshInf = SystemAPI.GetComponent<MaterialMeshInfo>(chunkEntity);
                        unregisterBuffer.Add(new TerrainUnregisterMeshBuffer { meshId = matMeshInf.MeshID });
                        SystemAPI.SetComponentEnabled<MaterialMeshInfo>(chunkEntity, false);
                        SystemAPI.SetComponentEnabled<TerrainDeferredVisible>(chunkEntity, false);
                    }

                    if (chunk.skirts.Length > 0) {
                        for (int skirtIndex = 0; skirtIndex < 6; skirtIndex++) {
                            Entity skirtEntity = chunk.skirts[skirtIndex];

                            if (SystemAPI.HasComponent<MaterialMeshInfo>(skirtEntity)) {
                                MaterialMeshInfo matMeshInf = SystemAPI.GetComponent<MaterialMeshInfo>(skirtEntity);
                                unregisterBuffer.Add(new TerrainUnregisterMeshBuffer { meshId = matMeshInf.MeshID });
                                SystemAPI.SetComponentEnabled<MaterialMeshInfo>(skirtEntity, false);
                                SystemAPI.SetComponentEnabled<TerrainDeferredVisible>(skirtEntity, false);
                            }
                        }
                    }
                }

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

                bool deferredVisibility = SystemAPI.GetComponent<TerrainChunkRequestMeshingTag>(chunkEntity).deferredVisibility;

                RenderMeshUtility.AddComponents(chunkEntity, EntityManager, renderMeshDescription, materialMeshInfo);

                SystemAPI.SetComponentEnabled<MaterialMeshInfo>(chunkEntity, !deferredVisibility);
                SystemAPI.SetComponentEnabled<TerrainDeferredVisible>(chunkEntity, !deferredVisibility);
                SystemAPI.SetComponentEnabled<TerrainChunkMesh>(chunkEntity, true);
                SystemAPI.SetComponent<TerrainChunkMesh>(chunkEntity, TerrainChunkMesh.FromMeshJobHandlerStats(stats));

                SystemAPI.SetComponent<RenderBounds>(chunkEntity, new RenderBounds() {
                    Value = localRenderBounds,
                });

                SystemAPI.SetComponent<WorldRenderBounds>(chunkEntity, new WorldRenderBounds() {
                    Value = worldRenderBounds
                });

                if (stats.mainMeshIndexCount == 0) {
                    SystemAPI.SetComponentEnabled<TerrainDeferredVisible>(chunkEntity, false);
                }

                if (chunk.skirts.Length == 0) {
                    FixedList64Bytes<Entity> skirts = new FixedList64Bytes<Entity>();
                    Entity skirtPrototype = SystemAPI.GetSingleton<TerrainManager>().skirtPrototype;
                    LocalToWorld localToWorld = SystemAPI.GetComponent<LocalToWorld>(chunkEntity);
                    for (int i = 0; i < 6; i++) {
                        Entity skirt = EntityManager.Instantiate(skirtPrototype);
                        SystemAPI.SetComponent<TerrainSkirt>(skirt, new TerrainSkirt() { direction = (byte)i });
                        SystemAPI.SetComponent<LocalToWorld>(skirt, localToWorld);
                        SystemAPI.SetComponent<TerrainSkirtLinkedParent>(skirt, new TerrainSkirtLinkedParent() { chunkParent = chunkEntity });
                        skirts.Add(skirt);
                    }
                    chunk.skirts = skirts;
                }

                for (int skirtIndex = 0; skirtIndex < 6; skirtIndex++) {
                    Entity skirtEntity = chunk.skirts[skirtIndex];

                    MaterialMeshInfo skirtMaterialMeshInfo = new MaterialMeshInfo(skirtMeshMaterialId, meshId, (ushort)(skirtIndex + 1));

                    RenderMeshUtility.AddComponents(skirtEntity, EntityManager, renderMeshDescription, skirtMaterialMeshInfo);

                    bool skirtDeferredVisibility = BitUtils.IsBitSet(chunk.skirtMask, skirtIndex);
                    SystemAPI.SetComponentEnabled<TerrainDeferredVisible>(skirtEntity, false);

                    SystemAPI.SetComponent<RenderBounds>(skirtEntity, new RenderBounds() {
                        Value = localRenderBounds,
                    });

                    SystemAPI.SetComponent<WorldRenderBounds>(skirtEntity, new WorldRenderBounds() {
                        Value = worldRenderBounds
                    });

                    if (stats.forcedSkirtFacesTriCount[skirtIndex] == 0) {
                        SystemAPI.SetComponentEnabled<TerrainDeferredVisible>(skirtEntity, false);
                    }
                }

                SystemAPI.SetComponent(chunkEntity, chunk);
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