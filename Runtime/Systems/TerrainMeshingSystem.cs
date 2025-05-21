using System.Collections.Generic;
using System.Linq;
using jedjoud.VoxelTerrain.Meshing;
using jedjoud.VoxelTerrain.Octree;
using Unity.Burst;
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

namespace jedjoud.VoxelTerrain.Generation {
    [UpdateInGroup(typeof(FixedStepTerrainSystemGroup))]
    [UpdateAfter(typeof(TerrainReadbackSystem))]
    public partial class TerrainMeshingSystem : SystemBase {
        private List<MeshJobHandler> handlers;
        private EntitiesGraphicsSystem graphicsSystem;

        const int MESH_JOBS_PER_TICK = 2;

        protected override void OnCreate() {
            RequireForUpdate<TerrainMesherConfig>();
            graphicsSystem = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            handlers = new List<MeshJobHandler>(MESH_JOBS_PER_TICK);
            for (int i = 0; i < MESH_JOBS_PER_TICK; i++) {
                handlers.Add(new MeshJobHandler());
            }
        }

        public bool IsFree() {
            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainChunk, TerrainChunkVoxels, TerrainChunkRequestMeshingTag, TerrainChunkVoxelsReadyTag>().Build();
            return query.CalculateEntityCount() == 0 && handlers.All(x => x.Free);
        }

        protected override void OnUpdate() {
            foreach (var handler in handlers) {

                if (handler.IsComplete(EntityManager)) {
                    Profiler.BeginSample("Finish Mesh Jobs");
                    FinishJob(handler);
                    Profiler.EndSample();
                    //Debug.Log("Finish Job: " + entity.ToString());
                }
            }

            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainChunk, TerrainChunkVoxels, TerrainChunkRequestMeshingTag, TerrainChunkVoxelsReadyTag>().Build();
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
                Profiler.BeginSample("Begin Mesh Jobs");
                //Debug.Log(entitiesArray[i].ToString());
                BeginJob(handler, entitiesArray[i], voxelsArray[i].inner);
                Profiler.EndSample();

                SystemAPI.SetComponentEnabled<TerrainChunkRequestMeshingTag>(entitiesArray[i], false);
            }

            voxelsArray.Dispose();
            entitiesArray.Dispose();
        }

        private void FinishJob(MeshJobHandler handler) {
            if (handler.TryComplete(EntityManager, out Mesh mesh, out Entity entity, out VoxelMesh stats)) {
                RenderFilterSettings filterSettings = new RenderFilterSettings {
                    ShadowCastingMode = ShadowCastingMode.On,
                    ReceiveShadows = true,
                    MotionMode = MotionVectorGenerationMode.ForceNoMotion,
                    StaticShadowCaster = false,
                    Layer = 0,
                    RenderingLayerMask = ~0u,
                };

                RenderMeshDescription description = new RenderMeshDescription {
                    FilterSettings = filterSettings,
                    LightProbeUsage = LightProbeUsage.Off,
                };

                TerrainMesherConfig config = SystemAPI.ManagedAPI.GetSingleton<TerrainMesherConfig>();
                Material[] materials = new Material[1] { config.material };
                Mesh[] meshes = new Mesh[1] { mesh };

                MaterialMeshIndex[] indices = new MaterialMeshIndex[1] {
                    new MaterialMeshIndex {
                        MaterialIndex = 0,
                        SubMeshIndex = 0,
                        MeshIndex = 0,
                    }
                };

                RenderMeshArray renderMeshArray = new RenderMeshArray(materials, meshes, indices);

                //Debug.Log($"{mesh.vertexCount}, {mesh.GetIndexCount(0)}");
                BatchMeshID meshId = graphicsSystem.RegisterMesh(mesh);
                //Debug.Log("registered mesh!");


                MaterialMeshInfo materialMeshInfo = MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0);

                OctreeNode node = EntityManager.GetComponentData<TerrainChunk>(entity).node;

                RenderMeshUtility.AddComponents(entity, EntityManager, description, renderMeshArray, materialMeshInfo);

                float scalingFactor = node.size / (64f);
                AABB localRenderBounds = new Unity.Mathematics.MinMaxAABB {
                    Min = stats.Bounds.min,
                    Max = stats.Bounds.max,
                };

                AABB worldRenderBounds = localRenderBounds;
                worldRenderBounds.Center += (float3)node.position;
                worldRenderBounds.Extents *= scalingFactor;

                EntityManager.SetComponentData<RenderBounds>(entity, new RenderBounds() {
                    Value = localRenderBounds,
                });

                EntityManager.SetComponentData<WorldRenderBounds>(entity, new WorldRenderBounds() {
                    Value = worldRenderBounds
                });
            }
                //chunk.voxelMaterialsLookup = stats.VoxelMaterialsLookup;
                //chunk.triangleOffsetLocalMaterials = stats.TriangleOffsetLocalMaterials;
                //chunk.state = VoxelChunk.ChunkState.Done;

                //onMeshingComplete?.Invoke(chunk, stats);
                //handler.request.callback?.Invoke(chunk);

                /*
                chunk.GetComponent<MeshFilter>().sharedMesh = chunk.sharedMesh;
                var renderer = chunk.GetComponent<MeshRenderer>();
                renderer.enabled = true;
                renderer.materials = stats.VoxelMaterialsLookup.Select(x => terrain.materials[x].material).ToArray();

                float scalingFactor = chunk.node.size / (64f * terrain.voxelSizeFactor);
                chunk.bounds = new Bounds {
                    min = chunk.transform.position + stats.Bounds.min * scalingFactor,
                    max = chunk.transform.position + stats.Bounds.max * scalingFactor,
                };
                renderer.bounds = chunk.bounds;
                */

            
        }

        private void BeginJob(MeshJobHandler handler, Entity entity, NativeArray<Voxel> voxels) {
            handler.BeginJob(entity, voxels);
        }

        protected override void OnDestroy() {
            foreach (MeshJobHandler handler in handlers) {
                handler.TryComplete(EntityManager, out Mesh _, out Entity _, out VoxelMesh _);
                handler.Dispose();
            }
        }
    }
}