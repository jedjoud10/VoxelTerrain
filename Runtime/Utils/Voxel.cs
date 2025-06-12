using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain {
    // SoA packed voxel data coming from the GPU
    // Packed so that each array is at least the size of a uint, since we don't have "half" in shaders
    public struct GpuVoxelData {
        [StructLayout(LayoutKind.Sequential)]
        public struct PaddedDensity {
            public half density;
            public ushort _padding;
        }

        public NativeArray<PaddedDensity> paddedDensities;

        public GpuVoxelData(Allocator allocator) {
            paddedDensities = new NativeArray<PaddedDensity>(VoxelUtils.VOLUME, allocator, NativeArrayOptions.UninitializedMemory);
        }

        public void Dispose() {
            paddedDensities.Dispose();
        }
    }


    // SoA voxel data
    public struct VoxelData {
        public NativeArray<half> densities;


        public VoxelData(Allocator allocator) {
            densities = new NativeArray<half>(VoxelUtils.VOLUME, allocator, NativeArrayOptions.UninitializedMemory);
        }

        public void Dispose() {
            densities.Dispose();
        }
    }
}