using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, OptimizeFor = OptimizeFor.Performance)]
    public struct FaceVoxelsUpsampleJob : IJobParallelFor {
        // Source voxels from LOD1.
        [ReadOnly]
        public NativeArray<Voxel> lod1Voxels;

        // morton encoded.
        public NativeArray<Voxel> dstFace;

        public uint2 relativeLod1Offset;

        public void Execute(int index) {
            uint2 srcPosFlat = Morton.DecodeMorton2D_32((uint)(index)) / 2;
            uint3 srcPos = new uint3(0, srcPosFlat + relativeLod1Offset * VoxelUtils.SIZE / 2);
            Voxel upsampled = lod1Voxels[VoxelUtils.PosToIndexMorton(srcPos)];
            dstFace[index] = upsampled;
        }
    }
}