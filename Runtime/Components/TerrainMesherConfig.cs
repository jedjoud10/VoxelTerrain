using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Meshing {
    public class TerrainMesherConfig : IComponentData {
        public TerrainMaterial material;
    }
}