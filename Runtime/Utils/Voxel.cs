using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain {
    // CPU representation of what a voxel is. The most important value here is the density value
    [StructLayout(LayoutKind.Sequential)]
    public struct Voxel {
        public const int size = sizeof(int);

        // Density of the voxel as a half to save some memory
        public half density;

        // Material of the voxel that depicts its color and other parameters
        public byte material;

        // Used for extra color data on a per vertex basis
        public byte _padding;

        // Empty voxel with the empty material
        public readonly static Voxel Empty = new Voxel {
            density = half.zero,
            material = byte.MaxValue,
            _padding = 0,
        };
    }
}