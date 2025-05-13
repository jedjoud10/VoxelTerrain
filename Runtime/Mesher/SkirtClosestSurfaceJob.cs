using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public struct SkirtClosestSurfaceJob : IJobParallelFor {
        [WriteOnly]
        public NativeArray<bool> withinThreshold;

        [ReadOnly]
        public NativeArray<Voxel> voxels;

        const int PADDING_SEARCH_AREA = 2;
        public void Execute(int index) {
            withinThreshold[index] = false;
            
            int face = index / VoxelUtils.FACE;
            int direction = face % 3;
            bool negative = face < 3;
            int localIndex = index % VoxelUtils.FACE;
            uint missing = negative ? 0 : ((uint)VoxelUtils.SIZE - 1);

            {
                uint2 flattened = VoxelUtils.IndexToPos2D(localIndex, VoxelUtils.SIZE);
                uint3 position = SkirtUtils.UnflattenFromFaceRelative(flattened, direction, missing);

                // skip if this is air, we will never generate forced skirts in the air
                if (voxels[VoxelUtils.PosToIndex(position, VoxelUtils.SIZE)].density > 0) {
                    return;
                }
            }

            int2 basePosition2D = (int2)VoxelUtils.IndexToPos2D(localIndex, VoxelUtils.SIZE);
            
            bool within = false;
            for (int x = -PADDING_SEARCH_AREA; x <= PADDING_SEARCH_AREA; x++) {
                for (int y = -PADDING_SEARCH_AREA; y <= PADDING_SEARCH_AREA; y++) {
                    int2 offset = new int2(x, y);
                    int3 global = SkirtUtils.UnflattenFromFaceRelative(offset + basePosition2D, direction, (int)missing);

                    if (math.all(global >= 0 & global < VoxelUtils.SIZE)) {
                        if (voxels[VoxelUtils.PosToIndex((uint3)global, VoxelUtils.SIZE)].density >= 0) {
                            within = true;
                            break;
                        }
                    }
                }
            }

            withinThreshold[index] = within;
        }
    }
}