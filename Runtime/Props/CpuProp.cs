using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Props {
    [StructLayout(LayoutKind.Sequential)]
    public struct CpuProp {
        public float3 position;
        public float3 rotation;
        public float scale;

        // Props of a different type are stored in different GPU buffers
        // This makes instanced rendering a lot easy, and also allows us to have more props of a specific type
        public int type;
        public int variant;
    }
}