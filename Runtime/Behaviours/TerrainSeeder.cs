using UnityEngine;

namespace jedjoud.VoxelTerrain.Generation {
    public class TerrainSeeder : MonoBehaviour {
        [Header("Seeding")]
        public int seed = 1234;
        public Vector3Int permutationSeed;
        public Vector3Int moduloSeed;

        private void OnValidate() {
            ComputeSecondarySeeds();
            GetComponent<TerrainPreview>()?.OnPropertiesChanged();
        }

        private void ComputeSecondarySeeds() {
            var random = new System.Random(seed);
            permutationSeed.x = random.Next(-1000, 1000);
            permutationSeed.y = random.Next(-1000, 1000);
            permutationSeed.z = random.Next(-1000, 1000);
            moduloSeed.x = random.Next(-1000, 1000);
            moduloSeed.y = random.Next(-1000, 1000);
            moduloSeed.z = random.Next(-1000, 1000);
        }

        public void RandomizeSeed() {
            seed = UnityEngine.Random.Range(-9999, 9999);
            ComputeSecondarySeeds();
            GetComponent<TerrainPreview>()?.OnPropertiesChanged();
        }
    }
}