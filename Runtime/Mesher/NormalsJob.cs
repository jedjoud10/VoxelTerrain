using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true)]
    public struct NormalsJob : IJobParallelFor {
        [ReadOnly]
        public NativeArray<Voxel> voxels;

        [WriteOnly]
        public NativeArray<float3> normals;

        public void Execute(int index) {
            uint3 position = VoxelUtils.IndexToPos(index, VoxelUtils.SIZE);
            normals[index] = math.up();

            if (math.any(position > VoxelUtils.SIZE - 2))
                return;

            half src = Load(position);
            half x = Load(position + new uint3(1, 0, 0));
            half y = Load(position + new uint3(0, 1, 0));
            half z = Load(position + new uint3(0, 0, 1));
            float3 normal = math.normalizesafe(new float3(x - src, y - src, z - src), math.up());

            normals[index] = normal;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private half Load(uint3 position) {
            int newIndex = VoxelUtils.PosToIndex(position, VoxelUtils.SIZE);
            return voxels[newIndex].density;
        }
    }
}