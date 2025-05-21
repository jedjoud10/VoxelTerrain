using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain {
    [UpdateInGroup(typeof(FixedStepTerrainSystemGroup), OrderLast = true)]
    [BurstCompile]
    public partial struct TerrainTickSystem : ISystem {
        public struct Singleton : IComponentData {
            public uint tick;
        }

        public void OnCreate(ref SystemState state) {
            state.EntityManager.CreateSingleton<Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            ref Singleton singleton = ref SystemAPI.GetSingletonRW<Singleton>().ValueRW;
            singleton.tick++;
        }
    }
}
