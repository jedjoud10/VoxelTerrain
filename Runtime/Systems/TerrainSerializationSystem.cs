using jedjoud.VoxelTerrain.Octree;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Serialization {
    [UpdateInGroup(typeof(TerrainFixedStepSystemGroup))]
    [UpdateAfter(typeof(TerrainOctreeSystem))]
    public partial struct TerreainSerializationSystem : ISystem {
        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<TerrainSerializeTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            Entity e = SystemAPI.GetSingletonEntity<TerrainSerializeTag>();
            state.EntityManager.DestroyEntity(e);

            // serialize seed
            // serialize deleted segment entities
            // serialize edit stuff
        }
    }
}