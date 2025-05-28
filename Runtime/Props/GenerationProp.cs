using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Props {
    public struct GenerationProp {
        public Generation.Variable<float3> position;
        public Generation.Variable<float3> rotation;
        public Generation.Variable<float> scale;
        public Generation.Variable<int> type;
        public Generation.Variable<int> variant;
    }
}