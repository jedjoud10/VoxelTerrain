using jedjoud.VoxelTerrain.Generation;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Props {
    public struct GpuProp {
        public Variable<float3> position;
        public Variable<float> scale;
        public Variable<bool> spawn;

        public static GpuProp Empty = new GpuProp() {
            position = float3.zero,
            scale = 1.0f,
            spawn = false
        };
    }
}