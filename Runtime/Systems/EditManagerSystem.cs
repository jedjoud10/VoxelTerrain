using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Edits {
    [UpdateInGroup(typeof(FixedStepTerrainSystemGroup))]
    [UpdateAfter(typeof(EditStoreSystem))]
    [UpdateAfter(typeof(EditApplySystem))]
    public partial struct EditManagerSystem : ISystem {

        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            state.EntityManager.CreateSingleton<TerrainEdits>(new TerrainEdits {
                chunkPositionsToChunkEditIndices = new NativeHashMap<int3, int>(0, Allocator.Persistent),
                chunkEdits = new UnsafeList<NativeArray<Voxel>>(0, Allocator.Persistent),
            });
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
            TerrainEdits backing = SystemAPI.GetSingleton<TerrainEdits>();

            backing.chunkPositionsToChunkEditIndices.Dispose();

            foreach (var editData in backing.chunkEdits) {
                editData.Dispose();
            }

            backing.chunkEdits.Dispose();
        }
    }
}