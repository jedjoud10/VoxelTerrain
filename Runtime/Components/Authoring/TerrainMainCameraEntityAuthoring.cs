using Unity.Entities;
using UnityEngine;

namespace jedjoud.VoxelTerrain {
    class TerrainMainCameraEntityAuthoring : MonoBehaviour {
    }

    class TerrainMainCameraEntityBaker : Baker<TerrainMainCameraEntityAuthoring> {
        public override void Bake(TerrainMainCameraEntityAuthoring authoring) {
            AddComponent(GetEntity(TransformUsageFlags.Dynamic), new TerrainMainCamera {
            });
        }
    }
}