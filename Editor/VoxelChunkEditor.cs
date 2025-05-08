using System;
using System.Linq;
using jedjoud.VoxelTerrain.Meshing;
using UnityEditor;
using UnityEngine;


namespace jedjoud.VoxelTerrain.Generation {
    [CustomEditor(typeof(VoxelChunk), true)]
    public class VoxelChunkEditor : Editor {

        public override void OnInspectorGUI() {
            base.OnInspectorGUI();

            VoxelChunk src = (VoxelChunk)target;
            VoxelStitch stitch = src.stitch;

            EditorGUILayout.LabelField($"CanStitch: {stitch.CanStitch()}");
            EditorGUILayout.LabelField("Planes");
            string t = "";
            for (int i = 0; i < 3; i++) {
                EditorGUI.indentLevel++;

                bool anyValidChunk = false;
                if (stitch.planes[i] == null) {
                    t = "Null";
                } else if (stitch.planes[i] is VoxelStitch.UniformPlane a) {
                    t = "Uniform";
                    anyValidChunk = a.neighbour != null;
                } else if (stitch.planes[i] is VoxelStitch.HiToLoPlane b) {
                    t = "HiToLo";
                    anyValidChunk = b.lod1Neighbour != null;
                } else if (stitch.planes[i] is VoxelStitch.LoToHiPlane c) {
                    t = "LoToHi";
                    anyValidChunk = c.lod0Neighbours.Any(x => x != null);
                }

                EditorGUILayout.LabelField($"Plane {i}: {t}, {anyValidChunk}");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.LabelField("Edges");
            for (int i = 0; i < 3; i++) {
                EditorGUI.indentLevel++;

                bool anyValidChunk = false;
                if (stitch.edges[i] == null) {
                    t = "Null";
                } else if (stitch.edges[i] is VoxelStitch.UniformEdge a) {
                    t = "Uniform";
                    anyValidChunk = a.neighbour != null;
                } else if (stitch.edges[i] is VoxelStitch.HiToLoEdge b) {
                    t = "HiToLo";
                    anyValidChunk = b.lod1Neighbour != null;
                } else if (stitch.edges[i] is VoxelStitch.LoToHiEdge c) {
                    t = "LoToHi";
                    anyValidChunk = c.lod0Neighbours.Any(x => x != null);
                }

                EditorGUILayout.LabelField($"Edge {i}: {t}, {anyValidChunk}");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.LabelField("Corner");
            EditorGUI.indentLevel++;
            {
                bool anyValidChunk = false;
                if (stitch.corner == null) {
                    t = "Null";
                } else if (stitch.corner is VoxelStitch.UniformCorner a) {
                    t = "Uniform";
                    anyValidChunk = a.neighbour != null;
                } else if (stitch.corner is VoxelStitch.HiToLoCorner b) {
                    t = "HiToLo";
                    anyValidChunk = b.lod1Neighbour != null;
                } else if (stitch.corner is VoxelStitch.LoToHiCorner c) {
                    t = "LoToHi";
                    anyValidChunk = c.lod0Neighbour != null;
                }
                EditorGUILayout.LabelField($"Corner: {t}, {anyValidChunk}");
            }

            EditorGUI.indentLevel--;

            /*
            EditorGUILayout.LabelField($"Sigma: ");
            EditorGUI.indentLevel++;

            for (int i = 0; i < 3; i++) {
                EditorGUILayout.LabelField($"Plane {i}: {stitch.planes[i].HasVoxelData()}");
            }

            for (int i = 0; i < 3; i++) {
                EditorGUILayout.LabelField($"Edge {i}: {stitch.edges[i].HasVoxelData()}");
            }

            EditorGUILayout.LabelField($"Corner: {stitch.corner.HasVoxelData()}");
            */
            EditorGUI.indentLevel--;
        }
    }
}
