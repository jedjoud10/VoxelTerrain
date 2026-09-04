using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace jedjoud.VoxelTerrain {
    partial class TerrainCopyMainCameraSystem : SystemBase {
        protected override void OnCreate() {
            RequireForUpdate<TerrainMainCamera>();
        }

        protected override void OnUpdate() {
            if (ManagedTerrainMainCamera.instance != null) {
                ManagedTerrainMainCamera go = ManagedTerrainMainCamera.instance;
                Camera camera = go.GetComponent<Camera>();

                Entity cameraEntity = SystemAPI.GetSingletonEntity<TerrainMainCamera>();

                if (go.copyTransformIntoEntity) {
                    // does not copy scale
                    LocalToWorld worldTransform = new LocalToWorld { Value = float4x4.TRS(go.transform.position, go.transform.rotation, 1f) };
                    SystemAPI.SetComponent<LocalToWorld>(cameraEntity, worldTransform);
                }

                SystemAPI.SetComponent<TerrainMainCamera>(cameraEntity, new TerrainMainCamera {
                    projectionMatrix = camera.projectionMatrix,
                    worldToCameraMatrix = camera.worldToCameraMatrix,
                    nearFarPlanes = new float2(camera.nearClipPlane, camera.farClipPlane),
                    position = camera.transform.position,
                    up = camera.transform.up,
                });
            }
        }
    }
}
