using Unity.Entities;
using UnityEngine;

namespace jedjoud.VoxelTerrain {
    class TerrainMainCameraAuthoring : MonoBehaviour {
    }

    class TerrainMainCameraBaker : Baker<TerrainMainCameraAuthoring> {
        public override void Bake(TerrainMainCameraAuthoring authoring) {
            AddComponent(GetEntity(TransformUsageFlags.Dynamic), new TerrainMainCamera {
            });
        }
    }
}