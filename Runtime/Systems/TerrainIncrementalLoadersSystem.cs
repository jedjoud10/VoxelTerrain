using jedjoud.VoxelTerrain.Segments;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace jedjoud.VoxelTerrain.Octree {
    [UpdateInGroup(typeof(TerrainFixedStepSystemGroup))]
    [UpdateBefore(typeof(TerrainOctreeSystem))]
    [UpdateBefore(typeof(TerrainSegmentManagerSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct TerrainIncrementalLoadersSystem : ISystem {
        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<TerrainOctree>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            ref TerrainOctree octree = ref SystemAPI.GetSingletonRW<TerrainOctree>().ValueRW;

            foreach (var (loader, matrix) in SystemAPI.Query<RefRW<TerrainLoader>, LocalToWorld>()) {
                ref float3 pos = ref loader.ValueRW.position;
                float3 newPos = matrix.Position;
                if (math.distance(pos, newPos) > 1f) {
                    loader.ValueRW.position = newPos;
                    octree.shouldUpdate = true;
                }
            }
        }
    }
}