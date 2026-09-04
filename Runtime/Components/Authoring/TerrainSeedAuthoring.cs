using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain {
    class TerrainSeedAuthoring : MonoBehaviour {
        public int seed;
    }

    class TerrainSeedBaker : Baker<TerrainSeedAuthoring> {
        public override void Bake(TerrainSeedAuthoring authoring) {
            Entity self = GetEntity(TransformUsageFlags.None);

            AddComponent(self, new TerrainSeed {
                seed = authoring.seed,
                moduloSeed = int3.zero,
                permutationSeed = int3.zero,
                dirty = true,
            });
        }
    }
}