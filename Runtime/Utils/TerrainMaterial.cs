using UnityEngine;

namespace jedjoud.VoxelTerrain {
    [CreateAssetMenu(fileName = "New Voxel Material", menuName = "Voxel Terrain/Create new Voxel Material")]
    public class TerrainMaterial: ScriptableObject {
        [Header("Rendering")]
        public Material material;

        [Header("Physics")]
        public float dynamicFriction = 0.3f;
        public float staticFriction = 0.3f;
        public float bounce = 0.0f;
    }
}