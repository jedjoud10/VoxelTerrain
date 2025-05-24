using Unity.Entities;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Generation {
    class TerrainReadbackConfigAuthoring : MonoBehaviour {
        public bool skipEmptyChunks;
    }

    class TerrainReadbackConfigBaker : Baker<TerrainReadbackConfigAuthoring> {
        public override void Bake(TerrainReadbackConfigAuthoring authoring) {
            Entity self = GetEntity(TransformUsageFlags.None);

            AddComponent(self, new TerrainReadbackConfig {
                skipEmptyChunks = authoring.skipEmptyChunks,
            });
        }
    }
}