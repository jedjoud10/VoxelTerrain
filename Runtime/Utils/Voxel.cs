using System.Runtime.InteropServices;
using Unity.Collections;
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

        // Not used
        public byte _padding;

        // Empty voxel with the empty material
        public readonly static Voxel Empty = new Voxel {
            density = half.zero,
            material = byte.MaxValue,
            _padding = 0
        };
    }

    // SoA type representation for the voxel data
    public struct VoxelData {
        public NativeArray<half> densities;
        public NativeArray<byte> material;

        public static VoxelData Init() {
            return new VoxelData {
                densities = new NativeArray<half>(VoxelUtils.VOLUME, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
                material = new NativeArray<byte>(VoxelUtils.VOLUME, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
            };
        }

        public void Dispose() {
            densities.Dispose();
            material.Dispose();
        }
    }
}