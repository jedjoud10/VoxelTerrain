using Unity.Entities;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Props {
    class TerrainPropAuthoring : MonoBehaviour {
    }

    class TerrainPropBaker : Baker<TerrainPropAuthoring> {
        public override void Bake(TerrainPropAuthoring authoring) {
            Entity self = GetEntity(TransformUsageFlags.Renderable | TransformUsageFlags.Dynamic);
            AddComponent<TerrainPropTag>(self);
        }
    }
}