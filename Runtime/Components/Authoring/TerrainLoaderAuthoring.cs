using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain {
    class TerrainLoaderAuthoring : MonoBehaviour {
        [Range(0f, 1f)]
        public float octreeNodeFactor = 0.5f;
        public Vector3Int segmentExtent = Vector3Int.one * 10;
        public float segmentLodFactor = 0.8f;
    }

    class TerrainOctreeLoaderBaker : Baker<TerrainLoaderAuthoring> {
        public override void Bake(TerrainLoaderAuthoring authoring) {
            AddComponent(GetEntity(TransformUsageFlags.Dynamic), new TerrainLoader {
                octreeNodeFactor = authoring.octreeNodeFactor,
                segmentExtent = new int3(authoring.segmentExtent.x, authoring.segmentExtent.y, authoring.segmentExtent.z),
                segmentLodFactor = authoring.segmentLodFactor
            });
        }
    }
}