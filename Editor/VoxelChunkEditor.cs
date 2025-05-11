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
            VoxelSkirt stitch = src.skirt;
            EditorGUI.indentLevel--;
        }
    }
}
