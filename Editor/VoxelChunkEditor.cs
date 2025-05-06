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

            EditorGUILayout.LabelField($"CanSampleExtraVoxels: {stitch.CanSampleExtraVoxels()}");
            EditorGUILayout.LabelField($"CanStitch: {stitch.CanStitch()}");
            EditorGUILayout.LabelField("Planes");
            string t = "";
            for (int i = 0; i < 3; i++) {
                EditorGUI.indentLevel++;

                if (stitch.planes[i] == null) {
                    t = "Null";
                } else if (stitch.planes[i] is VoxelStitch.UniformPlane) {
                    t = "Uniform";
                } else if (stitch.planes[i] is VoxelStitch.HiToLoPlane) {
                    t = "HiToLo";
                } else if (stitch.planes[i] is VoxelStitch.LoToHiPlane) {
                    t = "LoToHi";
                }

                EditorGUILayout.LabelField($"Plane {i}: {t}");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.LabelField("Edges");
            for (int i = 0; i < 3; i++) {
                EditorGUI.indentLevel++;

                if (stitch.edges[i] == null) {
                    t = "Null";
                } else if (stitch.edges[i] is VoxelStitch.UniformEdge) {
                    t = "Uniform";
                } else if (stitch.edges[i] is VoxelStitch.HiToLoEdge) {
                    t = "HiToLo";
                } else if (stitch.edges[i] is VoxelStitch.LoToHiEdge) {
                    t = "LoToHi";
                }

                EditorGUILayout.LabelField($"Edge {i}: {t}");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.LabelField("Corner");
            EditorGUI.indentLevel++;

            if (stitch.corner == null) {
                t = "Null";
            } else if (stitch.corner is VoxelStitch.UniformCorner) {
                t = "Uniform";
            } else if (stitch.corner is VoxelStitch.HiToLoCorner) {
                t = "HiToLo";
            } else if (stitch.corner is VoxelStitch.LoToHiCorner) {
                t = "LoToHi";
            }

            EditorGUILayout.LabelField($"Corner: {t}");
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
