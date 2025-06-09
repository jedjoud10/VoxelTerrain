using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;

namespace jedjoud.VoxelTerrain.Octree {
    public static class MultiLoaderUtils {
        public static bool ShouldUpdateDueToChangedTransforms(NativeArray<LocalToWorld> transforms, NativeList<float3> values) {
            if (transforms.Length != values.Length) {
                return true;
            }

            for (int i = 0; i < transforms.Length; i++) {
                if (math.distance(transforms[i].Position, values[i]) > 2) {
                    return true;
                }
            }

            return false;
        }
    }
}