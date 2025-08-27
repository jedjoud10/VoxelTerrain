using Unity.Entities;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Occlusion {
    class TerrainOcclusionConfigAuthoring : MonoBehaviour {
        [Min(1)]
        public int width = 64;
        [Min(1)]
        public int height = 64;
        [Min(8)]
        public int searchSize = 32;

        public float nearPlaneDepthOffsetFactor = 0.005f;
        [Min(0)]
        public float uvExpansionFactor = 0.02f;
    }

    class TerrainOcclusionConfigBaker: Baker<TerrainOcclusionConfigAuthoring> {
        public override void Bake(TerrainOcclusionConfigAuthoring authoring) {
            Entity self = GetEntity(TransformUsageFlags.None);

            AddComponent(self, new TerrainOcclusionConfig {
                width = authoring.width,
                height = authoring.height,
                size = authoring.searchSize,
                volume = authoring.searchSize * authoring.searchSize * authoring.searchSize,
                nearPlaneDepthOffsetFactor = authoring.nearPlaneDepthOffsetFactor,
                uvExpansionFactor = authoring.uvExpansionFactor,
            });
        }
    }
}