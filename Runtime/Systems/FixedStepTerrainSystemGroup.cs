using Unity.Burst;
using Unity.Entities;
using UnityEngine.Scripting;

namespace jedjoud.VoxelTerrain {
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
    public partial class FixedStepTerrainSystemGroup : ComponentSystemGroup {
        [Preserve]
        public FixedStepTerrainSystemGroup() {
            float defaultFixedTimestep = 1.0f / 32.0f;
            SetRateManagerCreateAllocator(new RateUtils.FixedRateCatchUpManager(defaultFixedTimestep));
            RateManager.Timestep = defaultFixedTimestep;
        }
    }
}