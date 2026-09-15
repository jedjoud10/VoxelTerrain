using Unity.Entities;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Octree {
    class TerrainOctreeConfigAuthoring : MonoBehaviour {
        public int maxDepth;
    }

    class TerrainOctreeConfigAuthoringBaker : Baker<TerrainOctreeConfigAuthoring> {
        public override void Bake(TerrainOctreeConfigAuthoring authoring) {
            AddComponent(GetEntity(TransformUsageFlags.None), new TerrainOctreeConfig {
                maxDepth = authoring.maxDepth,
            });
        }
    }
}