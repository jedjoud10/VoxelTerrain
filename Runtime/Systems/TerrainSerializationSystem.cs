using jedjoud.VoxelTerrain.Edits;
using jedjoud.VoxelTerrain.Octree;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Serialization {
    [UpdateInGroup(typeof(TerrainFixedStepSystemGroup))]
    [UpdateAfter(typeof(TerrainOctreeSystem))]
    public partial struct TerrainSerializationSystem : ISystem {
        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<TerrainSerializeTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            Entity e = SystemAPI.GetSingletonEntity<TerrainSerializeTag>();
            state.EntityManager.DestroyEntity(e);
            SerializationWriter writer = new SerializationWriter(Allocator.Temp);

            // serialize seed
            TerrainSeed seed = SystemAPI.GetSingleton<TerrainSeed>();
            writer.WriteInt(seed.seed);

            // serialize edit stuff
            TerrainEdits edits = SystemAPI.GetSingleton<TerrainEdits>();
            writer.WriteInt(edits.chunkPositionsToChunkEditIndices.Count);
            foreach (var kvp in edits.chunkPositionsToChunkEditIndices) {
                writer.WriteInt3(kvp.Key);
                writer.WriteInt(kvp.Value);
            }
            foreach (var data in edits.chunkEdits) {
                data.Serialize(ref writer);
            }

            Debug.LogWarning($"saved... {writer.Written}bytes");

            // serialize deleted segment entities
        }
    }
}