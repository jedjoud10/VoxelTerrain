using Unity.Collections;
using Unity.Jobs;

namespace jedjoud.VoxelTerrain.Octree {
    public static class OctreeUtils {
        public struct RecruseResults {
            public JobHandle handle;
            public NativeList<int> intersecting;
        }

        public static bool TryRecurseCheckMultipleAABB(ref TerrainOctree octree, NativeArray<Unity.Mathematics.Geometry.MinMaxAABB> boundsArray, out RecruseResults results) {
            results = default;

            if (octree.pending || !octree.handle.IsCompleted)
                return false;


            NativeList<int> intersecting = new NativeList<int>(Allocator.Persistent);
            RecurseBoundsIntersectJob job = new RecurseBoundsIntersectJob {
                boundsArray = boundsArray,
                intersecting = intersecting,
                nodes = octree.nodes,
            };

            JobHandle handle = job.Schedule();

            results = new RecruseResults {
                intersecting = intersecting,
                handle = handle,
            };

            return true;
        }
    }
}