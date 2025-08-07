using Unity.Entities;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Meshing {
    public class TerrainMesherConfig : IComponentData {
        public Material material;
        public bool createCopyMaterial;
    }
}