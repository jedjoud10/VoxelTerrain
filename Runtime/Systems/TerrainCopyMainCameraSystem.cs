using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace jedjoud.VoxelTerrain {
    [UpdateInGroup(typeof(TerrainFixedStepSystemGroup))]
    [UpdateBefore(typeof(Octree.TerrainOctreeSystem))]
    [RequireMatchingQueriesForUpdate]
    partial class TerrainCopyMainCameraSystem : SystemBase {
        protected override void OnCreate() {
        }

        protected override void OnUpdate() {
            if (ManagedTerrainMainCamera.instance != null) {
                ManagedTerrainMainCamera go = ManagedTerrainMainCamera.instance;
                Camera camera = go.GetComponent<Camera>();

                Entity cameraEntity = SystemAPI.GetSingletonEntity<TerrainMainCamera>();

                LocalToWorld worldTransform = new LocalToWorld { Value = float4x4.TRS(go.transform.position, go.transform.rotation, 1f) };
                SystemAPI.SetComponent<LocalToWorld>(cameraEntity, worldTransform);
                SystemAPI.SetComponent<TerrainMainCamera>(cameraEntity, new TerrainMainCamera {
                    projectionMatrix = camera.projectionMatrix,
                    worldToCamera = camera.worldToCameraMatrix,
                    nearFarPlanes = new float2(camera.nearClipPlane, camera.farClipPlane)
                });
            }
        }
    }
}
