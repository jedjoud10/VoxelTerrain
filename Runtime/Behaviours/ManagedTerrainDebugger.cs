using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Generation {
    public class ManagedTerrainDebugger : MonoBehaviour {
        public bool debugGui;
        private World world;

        private void Start() {
            world = World.DefaultGameObjectInjectionWorld;
        }

        private void OnGUI() {
            if (!debugGui)
                return;

            var offset = 0;

            GUI.contentColor = Color.black;
            void Label(string text) {
                GUI.Label(new Rect(0, offset, 300, 30), text);
                offset += 15;
            }

            EntityQuery chunks = world.EntityManager.CreateEntityQuery(typeof(TerrainChunk));
            EntityQuery meshed = world.EntityManager.CreateEntityQuery(typeof(TerrainChunk), typeof(TerrainChunkMeshReady));
            EntityQuery readback = world.EntityManager.CreateEntityQuery(typeof(TerrainChunk), typeof(TerrainChunkRequestReadbackTag));
            EntityQuery meshing = world.EntityManager.CreateEntityQuery(typeof(TerrainChunk), typeof(TerrainChunkRequestMeshingTag));
            EntityQuery end = world.EntityManager.CreateEntityQuery(typeof(TerrainChunk), typeof(TerrainChunkEndOfPipeTag));

            Label($"# of total chunk entities: {chunks.CalculateEntityCount()}");
            Label($"# of chunks pending GPU voxel data: {readback.CalculateEntityCount()}");
            Label($"# of chunks pending meshing: {meshing.CalculateEntityCount()}");
            Label($"# of chunk entities with a mesh: {meshed.CalculateEntityCount()}");
            Label($"# of chunk entities in the \"End of Pipe\" stage: {end.CalculateEntityCount()}");
        }
    }
}