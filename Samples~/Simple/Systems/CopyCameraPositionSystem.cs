using Unity.Entities;
using Unity.Transforms;
using jedjoud.VoxelTerrain;

namespace jedjoud.VoxelTerrain.Demo {
    [UpdateInGroup(typeof(FixedStepTerrainSystemGroup))]
    [UpdateBefore(typeof(jedjoud.VoxelTerrain.Octree.OctreeSystem))]
    [RequireMatchingQueriesForUpdate]
    partial class CopyCameraPositionSystem : SystemBase {
        protected override void OnCreate() {
        }

        protected override void OnUpdate() {
            if (MainCameraGameObject.instance != null) {
                MainCameraGameObject go = MainCameraGameObject.instance;
                Entity entity = SystemAPI.GetSingletonEntity<TerrainLoader>();
                SystemAPI.SetComponent<LocalTransform>(entity, LocalTransform.FromPosition(go.transform.position));
            }
        }
    }
}
