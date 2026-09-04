using System.Collections.Generic;
using jedjoud.VoxelTerrain.Segments;
using jedjoud.VoxelTerrain.Occlusion;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace jedjoud.VoxelTerrain {
    public class ManagedTerrainDebugger : MonoBehaviour {
        public bool debugGui;
        public bool debugChunkBounds;
        public bool debugSegmentBounds;
        public bool debugOcclusionCulling;
        public bool debugPropData;

        [Range(0f, 1f)]
        public float debugOcclusionTextureOverlay;
        private World world;
        private Texture2D occlusionTexture;

        private void Start() {
            world = World.DefaultGameObjectInjectionWorld;
        }

        private void OnGUI() {
            if (!debugGui)
                return;

            var offset = 0;
            List<string> cachedLabels = new List<string>();
            void Label(string text) {
                cachedLabels.Add(text);
                offset += 15;
            }

            void MakeMyShitFuckingOpaqueHolyShitUnityWhyCantYouSupportThisByDefaultThisIsStupid() {
                for (int i = 0; i < 5; i++) {
                    GUI.Box(new Rect(0, 0, 300, offset + 20), "");
                }
            }

            EntityQuery totalChunks = world.EntityManager.CreateEntityQuery(typeof(TerrainChunk));
            EntityQuery meshedChunks = world.EntityManager.CreateEntityQuery(typeof(TerrainChunk), typeof(TerrainChunkMesh));
            EntityQuery chunksAwaitingReadback = world.EntityManager.CreateEntityQuery(typeof(TerrainChunk), typeof(TerrainChunkRequestReadbackTag));
            EntityQuery chunksAwaitingMeshing = world.EntityManager.CreateEntityQuery(typeof(TerrainChunk), typeof(TerrainChunkRequestMeshingTag));
            EntityQuery occlusionCulledChunks = world.EntityManager.CreateEntityQuery(typeof(TerrainChunk), typeof(OccludableTag));
            EntityQuery chunksEndOfPipe = world.EntityManager.CreateEntityQuery(typeof(TerrainChunk), typeof(TerrainChunkEndOfPipeTag));
            EntityQuery segmentsAwaitingDispatch = world.EntityManager.CreateEntityQuery(typeof(TerrainSegment), typeof(TerrainSegmentRequestVoxelsTag));

            TerrainSegmentPropStuffSystem system = world.GetExistingSystemManaged<TerrainSegmentPropStuffSystem>();

            GUI.contentColor = Color.white;
            Label($"# of total chunk entities: {totalChunks.CalculateEntityCount()}");
            Label($"# of chunks pending GPU voxel data: {chunksAwaitingReadback.CalculateEntityCount()}");
            Label($"# of segments pending GPU dispatch: {segmentsAwaitingDispatch.CalculateEntityCount()}");
            Label($"# of chunks pending meshing: {chunksAwaitingMeshing.CalculateEntityCount()}");
            Label($"# of chunk entities with a mesh: {meshedChunks.CalculateEntityCount()}");
            Label($"# of occlusion culled chunks: {occlusionCulledChunks.CalculateEntityCount()}");
            Label($"# of chunk entities in the \"End of Pipe\" stage: {chunksEndOfPipe.CalculateEntityCount()}");

            if (system.initialized && debugPropData) {
                TerrainPropPermBuffers.DebugCounts[] counts = system.perm.GetCounts(system.config, system.temp, system.render);
                for (int i = 0; i < counts.Length; i++) {
                    TerrainPropPermBuffers.DebugCounts debug = counts[i];
                    Label($"--- Prop Type {i}: {system.config.props[i].name} ---");
                    Label($"Perm buffer count: {debug.maxPerm}");
                    Label($"Perm buffer offset: {debug.permOffset}");
                    Label($"Temp buffer count: {debug.maxTemp}");
                    Label($"Temp buffer offset: {debug.tempOffset}");

                    Label($"In-use perm props: {debug.currentInUse}");
                    Label($"Visible instances: {debug.visibleInstances}");
                    Label($"Visible impostors: {debug.visibleImpostors}");
                    Label($"");
                }
            }

            EntityQuery readySystems = world.EntityManager.CreateEntityQuery(typeof(TerrainReadySystems));
            TerrainReadySystems ready = readySystems.GetSingleton<TerrainReadySystems>();
            Label($"Manager System Ready: " + ready.manager);
            Label($"Readback System Ready: " + ready.readback);
            Label($"Mesher System Ready: " + ready.mesher);
            Label($"Segment Manager System Ready: " + ready.segmentManager);
            Label($"Segment Voxels System Ready: " + ready.segmentVoxels);
            Label($"Segment Props System Ready: " + ready.segmentPropsDispatch);


            MakeMyShitFuckingOpaqueHolyShitUnityWhyCantYouSupportThisByDefaultThisIsStupid();

            offset = 0;
            foreach (var item in cachedLabels) {
                GUI.Label(new Rect(0, offset, 300, 30), item);
                offset += 15;
            }

        }

        private void OnDrawGizmos() {
            if (world == null)
                return;

            EntityQuery meshedChunksNotOccluded = world.EntityManager.CreateEntityQuery(typeof(TerrainChunk), typeof(TerrainChunkMesh), typeof(WorldRenderBounds));
            EntityQuery meshedChunksOccluded = world.EntityManager.CreateEntityQuery(typeof(TerrainChunk), typeof(TerrainChunkMesh), typeof(WorldRenderBounds), typeof(OccludableTag));

            EntityQuery segmentsQuery = world.EntityManager.CreateEntityQuery(typeof(TerrainSegment));

            if (debugChunkBounds) {
                Gizmos.color = Color.green;
                NativeArray<WorldRenderBounds> visibleBounds = meshedChunksNotOccluded.ToComponentDataArray<WorldRenderBounds>(Allocator.Temp);
                foreach (var chunk in visibleBounds) {
                    Gizmos.DrawWireCube(chunk.Value.Center, chunk.Value.Extents * 2);
                }

                Gizmos.color = Color.red;
                NativeArray<WorldRenderBounds> occludedBounds = meshedChunksOccluded.ToComponentDataArray<WorldRenderBounds>(Allocator.Temp);
                foreach (var chunk in occludedBounds) {
                    Gizmos.DrawWireCube(chunk.Value.Center, chunk.Value.Extents * 2);
                }
            }

            if (debugSegmentBounds) {

                NativeArray<TerrainSegment> segments = segmentsQuery.ToComponentDataArray<TerrainSegment>(Allocator.Temp);
                foreach (var segment in segments) {
                    float3 worldPosition = ((float3)(segment.position) + 0.5f) * SegmentUtils.PHYSICAL_SEGMENT_SIZE;
                    float3 worldSize = new float3(1) * SegmentUtils.PHYSICAL_SEGMENT_SIZE;

                    if (segment.lod == TerrainSegment.LevelOfDetail.Low) {
                        Gizmos.color = Color.green;
                    } else {
                        Gizmos.color = Color.red;
                    }

                    Gizmos.DrawWireCube(worldPosition, worldSize);
                }
            }

#if UNITY_EDITOR
            EntityQuery singletonQuery = world.EntityManager.CreateEntityQuery(typeof(TerrainOcclusionConfig));
            if (debugOcclusionCulling && Camera.current.cameraType == CameraType.Game && singletonQuery.TryGetSingleton(out TerrainOcclusionConfig config)) {
                if (occlusionTexture == null) {
                    occlusionTexture = new Texture2D(config.width, config.height, TextureFormat.RFloat, false);
                    occlusionTexture.filterMode = FilterMode.Point;
                    occlusionTexture.wrapMode = TextureWrapMode.Clamp;
                }
                

                NativeArray<float> depth = world.EntityManager.CreateEntityQuery(typeof(TerrainOcclusionScreenData)).GetSingleton<TerrainOcclusionScreenData>().rasterizedDdaDepth;
                occlusionTexture.SetPixelData(depth, 0, 0);
                occlusionTexture.Apply();

                UnityEditor.Handles.BeginGUI();
                Rect fullScreenRect = new Rect(0, 0, Screen.width, Screen.height);
                GUI.DrawTexture(fullScreenRect, occlusionTexture, ScaleMode.StretchToFill, true, 0f, new Color(1, 1, 1, debugOcclusionTextureOverlay), 0f, 0f);
                UnityEditor.Handles.EndGUI();
            }
#endif
        }
    }
}