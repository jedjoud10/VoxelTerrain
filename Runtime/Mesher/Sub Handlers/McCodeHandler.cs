using static jedjoud.VoxelTerrain.VoxelUtils;
using static jedjoud.VoxelTerrain.BatchUtils;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;

namespace jedjoud.VoxelTerrain.Meshing {
    internal struct McCodeHandler : ISubHandler {
        public NativeArray<byte> enabled;
        public NativeArray<uint> bits;
        public JobHandle jobHandle;

        public void Init() {
            int packedCount = (int)math.ceil((float)VOLUME / (8 * sizeof(uint)));
            bits = new NativeArray<uint>(packedCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            enabled = new NativeArray<byte>(VOLUME, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        public void Schedule(NativeArray<Voxel> voxels, JobHandle dependency) {
            // The check job converts each of the voxels into simple bits.
            // If a voxel has a positive or zero density value then the bit that corresponds to that voxel is set
            // This is helps the next job fetch data more quickly since it doesn't need to look at each of the original voxel values
            // (which is slow considering it needs to fetch 8 indices that are far apart in memor)
            // by converting the voxels into bits, we allow the CPU to fit more important data (which is whether it has a negative density or not) into the same cache line as before
            CheckJob checkJob = new CheckJob {
                voxels = voxels,
                bits = bits,
            };

            // Calculates a marching cube value that corresponds to each cell of 2x2x2 voxels
            // I *think* this helps us later on in the vertex / quad jobs but I haven't profiled that yet...
            // Everything that depends on this job (vertex / quad) needs to be properly optimized.
            CornerJob cornerJob = new CornerJob {
                bits = bits,
                enabled = enabled,
            };

            JobHandle checkJobHandle = checkJob.Schedule(bits.Length, SMALLEST_BATCH, dependency);
            JobHandle cornerJobHandle = cornerJob.Schedule(VOLUME, SMALLEST_BATCH, checkJobHandle);
            jobHandle = cornerJobHandle;
        }

        public void Dispose() {
            enabled.Dispose();
            bits.Dispose();
        }
    }
}