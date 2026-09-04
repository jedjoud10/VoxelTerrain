using Unity.Entities;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain {
    [UpdateInGroup(typeof(TerrainFixedStepSystemGroup), OrderFirst = true)]
    public partial class TerrainSeedSystem : SystemBase {
        public static (int3, int3) ComputeSecondarySeeds(int seed) {
            var random = new System.Random(seed);
            int3 permutationSeed, moduloSeed;
            permutationSeed.x = random.Next(-1000, 1000);
            permutationSeed.y = random.Next(-1000, 1000);
            permutationSeed.z = random.Next(-1000, 1000);
            moduloSeed.x = random.Next(-1000, 1000);
            moduloSeed.y = random.Next(-1000, 1000);
            moduloSeed.z = random.Next(-1000, 1000);
            return (permutationSeed, moduloSeed);
        }

        protected override void OnCreate() {
            RequireForUpdate<TerrainSeed>();
        }

        protected override void OnUpdate() {
            ref TerrainSeed seed = ref SystemAPI.GetSingletonRW<TerrainSeed>().ValueRW;

            if (seed.dirty) {
                (int3 permutation, int3 modulo) = ComputeSecondarySeeds(seed.seed);
                seed.permutationSeed = permutation;
                seed.moduloSeed = modulo;
                seed.dirty = false;
            }
        }
    }
}