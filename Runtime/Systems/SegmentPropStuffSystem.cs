using jedjoud.VoxelTerrain.Props;
using Unity.Entities;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Segments {
    [UpdateInGroup(typeof(FixedStepTerrainSystemGroup))]
    [UpdateAfter(typeof(SegmentVoxelSystem))]
    public partial class SegmentPropStuffSystem : SystemBase {
        private bool initialized;
        private Entity singleton;

        TerrainPropTempBuffers temp;
        TerrainPropPermBuffers perm;
        TerrainPropRenderingBuffers render;

        protected override void OnCreate() {
            RequireForUpdate<TerrainPropsConfig>();
            initialized = false;
        }

        protected override void OnUpdate() {
            TerrainPropsConfig config = SystemAPI.ManagedAPI.GetSingleton<TerrainPropsConfig>();
            
            if (!initialized) {
                initialized = true;
                temp = new TerrainPropTempBuffers();
                perm = new TerrainPropPermBuffers();
                render = new TerrainPropRenderingBuffers();

                temp.Init(config);
                perm.Init(config);
                render.Init(perm.maxCombinedPermProps, config);
                singleton = EntityManager.CreateEntity();
                EntityManager.AddComponentObject(singleton, temp);
                EntityManager.AddComponentObject(singleton, perm);
                EntityManager.AddComponentObject(singleton, render);
            }
        }

        protected override void OnDestroy() {
            AsyncGPUReadback.WaitAllRequests();

            if (initialized) {
                EntityManager.DestroyEntity(singleton);
                temp.Dispose();
                perm.Dispose();
                render.Dispose();
            }
        }
    }
}