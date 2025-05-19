using Unity.Entities;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Octree {
    class TerrainManagerConfigAuthoring : MonoBehaviour {
        [Range(0, 1)]
        public float ditherTransitionTime;

        [Range(0, 4)]
        public int voxelSizeReduction;
    }

    class TerrainManagerConfigBaker : Baker<TerrainManagerConfigAuthoring> {
        public override void Bake(TerrainManagerConfigAuthoring authoring) {
            Entity self = GetEntity(TransformUsageFlags.None);

            AddComponent(self, new TerrainManagerConfig {
                voxelSizeReduction = authoring.voxelSizeReduction,
                ditherTransitionTime = authoring.ditherTransitionTime
            });
        }
    }
}