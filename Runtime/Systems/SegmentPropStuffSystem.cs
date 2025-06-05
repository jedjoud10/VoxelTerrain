using jedjoud.VoxelTerrain.Props;
using Unity.Entities;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Segments {
    [UpdateInGroup(typeof(FixedStepTerrainSystemGroup))]
    [UpdateAfter(typeof(SegmentVoxelSystem))]
    public partial class SegmentPropStuffSystem : SystemBase {
        public bool initialized;
        private Entity singleton;

        public TerrainPropsConfig config;
        public TerrainPropTempBuffers temp;
        public TerrainPropPermBuffers perm;
        public TerrainPropRenderingBuffers render;

        protected override void OnCreate() {
            RequireForUpdate<TerrainPropsConfig>();
            initialized = false;

            config = null;
            temp = null;
            perm = null;
            render = null;
        }

        protected override void OnUpdate() {
            TerrainPropsConfig config = SystemAPI.ManagedAPI.GetSingleton<TerrainPropsConfig>();
            
            if (!initialized) {
                initialized = true;
                this.config = config;

                if (config.props.Count > 0 && config.baked.Count > 0) {
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
                } else {
                    singleton = Entity.Null;
                }
            }
        }

        protected override void OnDestroy() {
            AsyncGPUReadback.WaitAllRequests();

            if (initialized) {
                if (singleton != Entity.Null) {
                    EntityManager.DestroyEntity(singleton);
                    temp.Dispose();
                    perm.Dispose();
                    render.Dispose();
                    temp = null;
                    perm = null;
                    render = null;
                }
            }
        }
    }
}