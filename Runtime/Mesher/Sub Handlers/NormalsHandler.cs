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

        public void Schedule(NativeArray<Voxel> voxels, JobHandle dependency) {
            // Normalize my shi dawg | Part 1
            NormalsPrefetchJob prefetchBase = new NormalsPrefetchJob {
                voxels = voxels.GetSubArray(BASE_OFFSET, BASE_COUNT),
                val = normalPrefetchedVals[0],
            };
            NormalsPrefetchJob prefetchX = new NormalsPrefetchJob {
                voxels = voxels.GetSubArray(OFFSET_X_OFFSET, OFFSET_X_COUNT),
                val = normalPrefetchedVals[1],
            };
            NormalsPrefetchJob prefetchY = new NormalsPrefetchJob {
                voxels = voxels.GetSubArray(OFFSET_Y_OFFSET, OFFSET_Y_COUNT),
                val = normalPrefetchedVals[2],
            };
            NormalsPrefetchJob prefetchZ = new NormalsPrefetchJob {
                voxels = voxels.GetSubArray(OFFSET_Z_OFFSET, OFFSET_Z_COUNT),
                val = normalPrefetchedVals[3],
            };

            // Normalize my shi dawg | Part 2
            NormalsCalculateJob normalsCalculateJob = new NormalsCalculateJob {
                normals = voxelNormals,
                baseVal = normalPrefetchedVals[0],
                xVal = normalPrefetchedVals[1],
                yVal = normalPrefetchedVals[2],
                zVal = normalPrefetchedVals[3]
            };

            // Prefetch the voxel values as to avoid fetching them in the same job
            // Helps a bit cache hit wise, since all of these jobs will read from nearly sequential data (each at a different offse )
            JobHandle prefetchBaseJobHandle = prefetchBase.Schedule(VOLUME, SMALLER_BATCH, dependency);
            JobHandle prefetchXJobHandle = prefetchX.Schedule(VOLUME - 1, SMALLER_BATCH, dependency);
            JobHandle prefetchYJobHandle = prefetchY.Schedule(VOLUME - SIZE * SIZE, SMALLER_BATCH, dependency);
            JobHandle prefetchZJobHandle = prefetchZ.Schedule(VOLUME - SIZE, SMALLER_BATCH, dependency);

            // Combine the prefetch deps
            JobHandle normalsDep1 = JobHandle.CombineDependencies(prefetchXJobHandle, prefetchYJobHandle, prefetchZJobHandle);
            JobHandle normalsDep2 = JobHandle.CombineDependencies(normalsDep1, prefetchBaseJobHandle);
            
            // Calculate the normals at EACH voxel
            // This gives pretty results compared to something like numerical approach but whatever
            // Maybe when I eventually write a proper hermite data system I can scratch all of this... (will be very hard considering I also need to support terrain edits of any kind)
            JobHandle normalsJobHandle = normalsCalculateJob.Schedule(VOLUME, SMALLEST_BATCH, normalsDep2);
            jobHandle = normalsJobHandle;
        }

        public void Dispose() {
            voxelNormals.Dispose();
            foreach (var item in normalPrefetchedVals) {
                item.Dispose();
            }
        }
    }
}