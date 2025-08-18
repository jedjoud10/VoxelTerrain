using Unity.Entities;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Meshing {
    class TerrainMesherConfigAuthoring : MonoBehaviour {
        public Material material;
        [Tooltip("Creates a copy of the material so that we can set the required '_SKIRT' keyword. Enable this so that skirts fallback to using smoothed normals instead of DDX/DDY normals")]
        public bool createCopyMaterial;

        [Tooltip("Max number of meshing jobs that we can start per tick. The higher the number, the more saturated the threads become")]
        [Min(1)]
        public int meshJobsPerTick;
    }

    class TerrainMesherConfigBaker: Baker<TerrainMesherConfigAuthoring> {
        public override void Bake(TerrainMesherConfigAuthoring authoring) {
            Entity self = GetEntity(TransformUsageFlags.None);

            AddComponentObject(self, new TerrainMesherConfig {
                material = authoring.material,
                createCopyMaterial = authoring.createCopyMaterial,
                meshJobsPerTick = authoring.meshJobsPerTick,
            });
        }
    }
}