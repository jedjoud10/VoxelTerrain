using UnityEditor;
using UnityEngine;


namespace jedjoud.VoxelTerrain.Editor {
    [CustomEditor(typeof(ManagedTerrain), true)]
    public class ManagedTerrainEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            base.OnInspectorGUI();

            GUILayout.Label("Supports Async Compute: " + SystemInfo.supportsAsyncCompute);
            GUILayout.Label("Supports Async Readback: " + SystemInfo.supportsAsyncGPUReadback);
        }
    }
}
