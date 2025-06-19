using Unity.Entities;
using UnityEngine;

namespace jedjoud.VoxelTerrain {
    class TerrainManagerConfigAuthoring : MonoBehaviour {
    }

    class TerrainManagerConfigBaker : Baker<TerrainManagerConfigAuthoring> {
        public override void Bake(TerrainManagerConfigAuthoring authoring) {
            Entity self = GetEntity(TransformUsageFlags.None);

            AddComponent(self, new TerrainManagerConfig {
            });
        }
    }
}