using Unity.Entities;
using Unity.Rendering;

namespace jedjoud.VoxelTerrain.Meshing {
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(TerrainMeshingSystem))]
    public partial class TerrainUnregisterMeshSystem : SystemBase {
        private EntitiesGraphicsSystem graphics;
        
        protected override void OnCreate() {
            graphics = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
        }

        private void Amogus() {
            if (!SystemAPI.HasSingleton<TerrainUnregisterMeshBuffer>()) {
                return;
            }

            var buffer = SystemAPI.GetSingletonBuffer<TerrainUnregisterMeshBuffer>();

            foreach (var cleanup in buffer) {
                if (graphics.GetMesh(cleanup.meshId) != null) {
                    graphics.UnregisterMesh(cleanup.meshId);
                }
            }

            buffer.Clear();
        }

        protected override void OnUpdate() {
            Amogus();
        }

        protected override void OnStopRunning() {
            Amogus();
        }
    }
}