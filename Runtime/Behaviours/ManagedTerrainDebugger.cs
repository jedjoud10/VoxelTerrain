using jedjoud.VoxelTerrain.Segments;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Generation {
    public class ManagedTerrainDebugger : MonoBehaviour {
        public bool debugGui;
        public bool debugChunkBounds;
        public bool debugSegmentBounds;
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

            EntityQuery totalChunks = world.EntityManager.CreateEntityQuery(typeof(TerrainChunk));
            EntityQuery meshedChunks = world.EntityManager.CreateEntityQuery(typeof(TerrainChunk), typeof(TerrainChunkMeshReady));
            EntityQuery chunksAwaitingReadback = world.EntityManager.CreateEntityQuery(typeof(TerrainChunk), typeof(TerrainChunkRequestReadbackTag));
            EntityQuery chunksAwaitingMeshing = world.EntityManager.CreateEntityQuery(typeof(TerrainChunk), typeof(TerrainChunkRequestMeshingTag));
            EntityQuery chunksEndOfPipe = world.EntityManager.CreateEntityQuery(typeof(TerrainChunk), typeof(TerrainChunkEndOfPipeTag));
            EntityQuery segmentsAwaitingDispatch = world.EntityManager.CreateEntityQuery(typeof(TerrainSegment), typeof(TerrainSegmentRequestVoxelsTag));

            Label($"# of total chunk entities: {totalChunks.CalculateEntityCount()}");
            Label($"# of chunks pending GPU voxel data: {chunksAwaitingReadback.CalculateEntityCount()}");
            Label($"# of segments pending GPU dispatch: {segmentsAwaitingDispatch.CalculateEntityCount()}");
            Label($"# of chunks pending meshing: {chunksAwaitingMeshing.CalculateEntityCount()}");
            Label($"# of chunk entities with a mesh: {meshedChunks.CalculateEntityCount()}");
            Label($"# of chunk entities in the \"End of Pipe\" stage: {chunksEndOfPipe.CalculateEntityCount()}");

            EntityQuery readySystems = world.EntityManager.CreateEntityQuery(typeof(TerrainReadySystems));
            TerrainReadySystems ready = readySystems.GetSingleton<TerrainReadySystems>();
            Label($"Manager System Ready: " + ready.manager);
            Label($"Readback System Ready: " + ready.readback);
            Label($"Mesher System Ready: " + ready.mesher);
            Label($"Segment Voxels System Ready: " + ready.segmentVoxels);
            Label($"Segment Props System Ready: " + ready.segmentProps);
        }

        private void OnDrawGizmos() {
            if (world == null)
                return;

            EntityQuery meshedChunks = world.EntityManager.CreateEntityQuery(typeof(TerrainChunk), typeof(TerrainChunkMeshReady), typeof(WorldRenderBounds));
            EntityQuery segmentsQuery = world.EntityManager.CreateEntityQuery(typeof(TerrainSegment));

            if (debugChunkBounds) {
                Gizmos.color = Color.red;
                NativeArray<WorldRenderBounds> chunkBounds = meshedChunks.ToComponentDataArray<WorldRenderBounds>(Allocator.Temp);
                foreach (var chunk in chunkBounds) {
                    Gizmos.DrawWireCube(chunk.Value.Center, chunk.Value.Extents * 2);
                }
            }

            if (debugSegmentBounds) {
                Gizmos.color = Color.green;
                NativeArray<TerrainSegment> segments = segmentsQuery.ToComponentDataArray<TerrainSegment>(Allocator.Temp);
                foreach (var segment in segments) {
                    float3 worldPosition = segment.position * SegmentUtils.PHYSICAL_SEGMENT_SIZE;
                    float3 worldSize = new float3(1) * SegmentUtils.PHYSICAL_SEGMENT_SIZE;

                    Gizmos.DrawWireCube(worldPosition, worldSize);
                }
            }
        }
    }
}