using System.Collections.Generic;
using System.Linq;
using jedjoud.VoxelTerrain.Meshing;
using jedjoud.VoxelTerrain.Octree;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Generation {
    [UpdateInGroup(typeof(FixedStepTerrainSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(TerrainMeshingSystem))]
    public partial struct TerrainArchetypeChunkBoundsSystem : ISystem {
        private ComponentTypeHandle<WorldRenderBounds> instanceBoundsTypeHandle;
        private ComponentTypeHandle<ChunkWorldRenderBounds> chunkBoundsTypeHandle;
        private EntityQuery query;

        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            query = SystemAPI.QueryBuilder().WithAll<TerrainChunk, RenderMeshArray, WorldRenderBounds>().WithAllChunkComponentRW<ChunkWorldRenderBounds>().Build();
            instanceBoundsTypeHandle = state.GetComponentTypeHandle<WorldRenderBounds>(true);
            chunkBoundsTypeHandle = state.GetComponentTypeHandle<ChunkWorldRenderBounds>();
            state.RequireForUpdate(query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            instanceBoundsTypeHandle.Update(ref state);
            chunkBoundsTypeHandle.Update(ref state);

            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainChunk, RenderMeshArray, WorldRenderBounds>().WithAllChunkComponentRW<ChunkWorldRenderBounds>().Build();
            NativeArray<ArchetypeChunk> archetypeChunks = query.ToArchetypeChunkArray(Allocator.Temp);
            
            for (int i = 0; i < archetypeChunks.Length; i++) {
                ArchetypeChunk archetypeChunk = archetypeChunks[i];
                NativeArray<WorldRenderBounds> terrainChunkWorldBounds = archetypeChunk.GetNativeArray<WorldRenderBounds>(ref instanceBoundsTypeHandle);

                float3 min = 1000000;
                float3 max = -1000000;

                for (int j = 0; j < terrainChunkWorldBounds.Length; j++) {
                    AABB bounds = terrainChunkWorldBounds[j].Value;
                    min = math.min(min, bounds.Min);
                    max = math.max(max, bounds.Max);
                }

                archetypeChunk.SetChunkComponentData(ref chunkBoundsTypeHandle, new ChunkWorldRenderBounds {
                    Value = new MinMaxAABB { Max = max, Min = min },
                });
            }

            archetypeChunks.Dispose();
        }
    }
}
