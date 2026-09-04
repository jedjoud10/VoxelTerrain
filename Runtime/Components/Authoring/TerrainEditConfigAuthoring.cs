using Unity.Entities;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Edits {
    class TerrainEditConfigAuthoring : MonoBehaviour {
    }

    class TerrainEditConfigBaker: Baker<TerrainEditConfigAuthoring> {
        public override void Bake(TerrainEditConfigAuthoring authoring) {
            Entity self = GetEntity(TransformUsageFlags.None);

            AddComponent(self, new TerrainEditConfig {
            });
        }
    }
}