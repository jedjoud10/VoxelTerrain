using Unity.Burst;
using Unity.Entities;

namespace jedjoud.VoxelTerrain {
    [UpdateInGroup(typeof(TerrainFixedStepSystemGroup), OrderLast = true)]
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
