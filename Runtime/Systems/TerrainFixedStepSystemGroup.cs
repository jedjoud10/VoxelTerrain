using Unity.Entities;
using UnityEngine.Scripting;

namespace jedjoud.VoxelTerrain {
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
    public partial class TerrainFixedStepSystemGroup : ComponentSystemGroup {
        [Preserve]
        public TerrainFixedStepSystemGroup() {
            float defaultFixedTimestep = 1.0f / 64.0f;
            SetRateManagerCreateAllocator(new RateUtils.FixedRateCatchUpManager(defaultFixedTimestep));
            RateManager.Timestep = defaultFixedTimestep;
        }
    }
}