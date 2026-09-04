using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static jedjoud.VoxelTerrain.VoxelUtils;
using static jedjoud.VoxelTerrain.BatchUtils;

namespace jedjoud.VoxelTerrain.Meshing {
    internal struct NormalsHandler: ISubHandler {
        public NativeArray<half>[] normalPrefetchedVals;
        public NativeArray<float3> voxelNormals;
        public JobHandle jobHandle;

        const int BASE_COUNT = VOLUME;
        const int OFFSET_X_COUNT = VOLUME - 1;
        const int OFFSET_Y_COUNT = VOLUME - SIZE * SIZE;
        const int OFFSET_Z_COUNT = VOLUME - SIZE;

        const int BASE_OFFSET = 0;
        const int OFFSET_X_OFFSET = 1;
        const int OFFSET_Y_OFFSET = SIZE * SIZE;
        const int OFFSET_Z_OFFSET = SIZE;


        public void Init() {
            voxelNormals = new NativeArray<float3>(VOLUME, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            normalPrefetchedVals = new NativeArray<half>[4];
            for (int i = 0; i < 4; i++) {
                normalPrefetchedVals[i] = new NativeArray<half>(VOLUME, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }
        }

        public void Schedule(ref VoxelData voxels, JobHandle dependency) {
            // Normalize my shi dawg | Part 1
            // Prefetch the voxel values as to avoid fetching them in the same job
            // Helps a bit cache hit wise, since all of these jobs will read from 
            NativeArray<JobHandle> deps = new NativeArray<JobHandle>(4, Allocator.Temp);
            deps[0] = normalPrefetchedVals[0].GetSubArray(0, BASE_COUNT).CopyFromAsync(voxels.densities.GetSubArray(BASE_OFFSET, BASE_COUNT), dependency);
            deps[1] = normalPrefetchedVals[1].GetSubArray(0, OFFSET_X_COUNT).CopyFromAsync(voxels.densities.GetSubArray(OFFSET_X_OFFSET, OFFSET_X_COUNT), dependency);
            deps[2] = normalPrefetchedVals[2].GetSubArray(0, OFFSET_Y_COUNT).CopyFromAsync(voxels.densities.GetSubArray(OFFSET_Y_OFFSET, OFFSET_Y_COUNT), dependency);
            deps[3] = normalPrefetchedVals[3].GetSubArray(0, OFFSET_Z_COUNT).CopyFromAsync(voxels.densities.GetSubArray(OFFSET_Z_OFFSET, OFFSET_Z_COUNT), dependency);
            JobHandle combined = JobHandle.CombineDependencies(deps);

            // Normalize my shi dawg | Part 2
            NormalsCalculateJob normalsCalculateJob = new NormalsCalculateJob {
                normals = voxelNormals,
                baseVal = normalPrefetchedVals[0],
                xVal = normalPrefetchedVals[1],
                yVal = normalPrefetchedVals[2],
                zVal = normalPrefetchedVals[3]
            };
                        
            // Calculate the normals at EACH voxel
            // This gives pretty results compared to something like numerical approach but whatever
            // Maybe when I eventually write a proper hermite data system I can scratch all of this... (will be very hard considering I also need to support terrain edits of any kind)
            jobHandle = normalsCalculateJob.Schedule(VOLUME, QUARTER_BATCH, combined);
        }

        public void Dispose() {
            voxelNormals.Dispose();
            foreach (var item in normalPrefetchedVals) {
                item.Dispose();
            }
        }
    }
}