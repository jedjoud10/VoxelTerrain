using UnityEditor;
using UnityEngine;


namespace jedjoud.VoxelTerrain.Editor {
    [CustomEditor(typeof(ManagedTerrainEditor), true)]
    public class ManagedTerrainEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            base.OnInspectorGUI();

            EditorGUILayout.LabelField("Compute Features: ", EditorStyles.boldLabel);
            GUILayout.Label("Supports Async Compute: " + SystemInfo.supportsAsyncCompute);
            GUILayout.Label("Supports Async Readback: " + SystemInfo.supportsAsyncGPUReadback);
        }
    }
}
