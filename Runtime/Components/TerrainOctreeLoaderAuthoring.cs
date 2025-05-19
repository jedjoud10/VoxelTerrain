using Unity.Entities;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Octree {
    class TerrainOctreeLoaderAuthoring : MonoBehaviour {
        public float factor;
    }

    class TerrainOctreeLoaderBaker : Baker<TerrainOctreeLoaderAuthoring> {
        public override void Bake(TerrainOctreeLoaderAuthoring authoring) {
            AddComponent(GetEntity(TransformUsageFlags.Dynamic), new TerrainOctreeLoader {
                factor = authoring.factor,
            });
        }
    }
}