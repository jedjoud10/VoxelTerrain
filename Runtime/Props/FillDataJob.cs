using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;

namespace jedjoud.VoxelTerrain.Props {
    [BurstCompile(CompileSynchronously = true)]
    public struct FillDataJob : IJobParallelFor {
        [NativeDisableParallelForRestriction]
        public ComponentLookup<LocalTransform> dstTransforms;
        [NativeDisableParallelForRestriction]
        public ComponentLookup<TerrainPropCleanup> dstCleanup;

        [ReadOnly]
        public NativeArray<Entity> entities;
        [ReadOnly]
        public NativeArray<LocalTransform> srcTransforms;
        [ReadOnly]
        public NativeArray<TerrainPropCleanup> srcCleanup;

        public void Execute(int index) {
            Entity entity = entities[index];
            dstTransforms[entity] = srcTransforms[index];
            dstCleanup[entity] = srcCleanup[index];
        }
    }
}