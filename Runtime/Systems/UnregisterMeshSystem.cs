using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace jedjoud.VoxelTerrain.Meshing {
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(MeshingSystem))]
    public partial class UnregisterMeshSystem : SystemBase {
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
                graphics.UnregisterMesh(cleanup.meshId);
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