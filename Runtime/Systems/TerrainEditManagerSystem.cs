using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using MinMaxAABB = Unity.Mathematics.Geometry.MinMaxAABB;

namespace jedjoud.VoxelTerrain.Edits {
    [UpdateInGroup(typeof(TerrainFixedStepSystemGroup))]
    [UpdateBefore(typeof(TerrainEditIncrementalModifySystem))]
    [UpdateBefore(typeof(TerrainEditApplySystem))]
    public partial struct TerrainEditManagerSystem : ISystem {
        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            state.EntityManager.CreateSingleton<TerrainEdits>(new TerrainEdits {
                chunkPositionsToChunkEditIndices = new NativeHashMap<int3, int>(0, Allocator.Persistent),
                chunkEdits = new NativeList<VoxelData>(Allocator.Persistent),
                modifiedChunkEditPositions = new NativeList<int3>(Allocator.Persistent),
                applySystemHandle = default,
            });
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
            TerrainEdits backing = SystemAPI.GetSingleton<TerrainEdits>();
            backing.applySystemHandle.Complete();

            backing.chunkPositionsToChunkEditIndices.Dispose();

            foreach (var editData in backing.chunkEdits) {
                editData.Dispose();
            }

            backing.chunkEdits.Dispose();
            backing.modifiedChunkEditPositions.Dispose();
        }

        public void OnUpdate(ref SystemState state) {
            ref TerrainEdits backing = ref SystemAPI.GetSingletonRW<TerrainEdits>().ValueRW;
            backing.modifiedChunkEditPositions.Clear();
        }
    }
}