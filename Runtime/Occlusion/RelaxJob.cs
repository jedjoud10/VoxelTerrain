using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Occlusion {
    [BurstCompile(CompileSynchronously = true)]
    public struct RelaxJob : IJobParallelFor {
        [ReadOnly]
        public NativeArray<uint> preRelaxationBits;

        [WriteOnly]
        public NativeArray<bool> postRelaxationBools;

        public void Execute(int index) {
            int3 pos = (int3)VoxelUtils.IndexToPos(index, OcclusionUtils.SIZE);
            for (int dz = -1; dz <= 1; dz++) {
                for (int dy = -1; dy <= 1; dy++) {
                    for (int dx = -1; dx <= 1; dx++) {
                        if (!IsVoxelSolid(pos + new int3(dx, dy, dz))) {
                            postRelaxationBools[index] = false;
                            return;
                        }
                    }
                }
            }

            postRelaxationBools[index] = true;
        }

        private bool IsVoxelSolid(int3 position) {
            if (VoxelUtils.CheckPositionInsideVolume(position, OcclusionUtils.SIZE)) {
                int index = VoxelUtils.PosToIndex((uint3)position, OcclusionUtils.SIZE);
                int component = index / 32;
                int shift = index % 32;
                uint batch = preRelaxationBits[component];
                return ((batch >> shift) & 1U) == 1;
            } else {
                return false;
            }
        }
    }
}