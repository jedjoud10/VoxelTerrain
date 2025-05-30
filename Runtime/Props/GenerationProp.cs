using Unity.Mathematics;
using jedjoud.VoxelTerrain.Generation;

namespace jedjoud.VoxelTerrain.Props {
    public struct GenerationProp {
        public Variable<float3> position;
        public Variable<quaternion> rotation;
        public Variable<float> scale;
        public Variable<int> variant;
        public Variable<int> type;
    }
}