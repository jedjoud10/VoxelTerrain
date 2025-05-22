using Unity.Entities;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Meshing {
    class TerrainMesherConfigAuthoring : MonoBehaviour {
        public TerrainMaterial material;
    }

    class TerrainMesherConfigBaker: Baker<TerrainMesherConfigAuthoring> {
        public override void Bake(TerrainMesherConfigAuthoring authoring) {
            Entity self = GetEntity(TransformUsageFlags.None);

            AddComponentObject(self, new TerrainMesherConfig {
                material = authoring.material
            });
        }
    }
}