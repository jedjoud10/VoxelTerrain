using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace jedjoud.VoxelTerrain.Octree {
    [UpdateInGroup(typeof(FixedStepTerrainSystemGroup))]
    [UpdateBefore(typeof(TerrainOctreeJobSystem))]
    [RequireMatchingQueriesForUpdate]
    partial class CopyCameraPositionSystem : SystemBase {
        protected override void OnCreate() {
        }

        protected override void OnUpdate() {
            if (MainCameraGameObject.instance != null) {
                MainCameraGameObject go = MainCameraGameObject.instance;
                Entity entity = SystemAPI.GetSingletonEntity<TerrainOctreeLoader>();
                SystemAPI.SetComponent<LocalTransform>(entity, LocalTransform.FromPosition(go.transform.position));
            }
        }
    }
}
