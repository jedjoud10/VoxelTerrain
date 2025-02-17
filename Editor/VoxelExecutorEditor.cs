using UnityEditor;
using UnityEngine;


namespace jedjoud.VoxelTerrain.Generation {
    [CustomEditor(typeof(VoxelExecutor), true)]
    public class VoxelExecutorEditor : Editor {
        public override void OnInspectorGUI() {
            base.OnInspectorGUI();

            var script = (VoxelExecutor)target;

            if (GUILayout.Button("Randomize Seed")) {
                script.RandomizeSeed();
            }
        }
    }
}
