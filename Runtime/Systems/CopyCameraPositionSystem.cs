using Unity.Entities;
using Unity.Transforms;

namespace jedjoud.VoxelTerrain {
    [UpdateInGroup(typeof(FixedStepTerrainSystemGroup))]
    [UpdateBefore(typeof(Octree.OctreeSystem))]
    [RequireMatchingQueriesForUpdate]
    partial class CopyCameraSystem : SystemBase {
        protected override void OnCreate() {
        }

        protected override void OnUpdate() {
            if (ManagedTerrainMainCamera.instance != null) {
                ManagedTerrainMainCamera go = ManagedTerrainMainCamera.instance;
                Entity cameraEntity = SystemAPI.GetSingletonEntity<TerrainMainCamera>();

                LocalTransform leTransform = LocalTransform.FromPositionRotation(go.transform.position, go.transform.rotation);
                SystemAPI.SetComponent<LocalTransform>(cameraEntity, leTransform);
            }
        }
    }
}
