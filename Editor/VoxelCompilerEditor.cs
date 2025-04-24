using UnityEditor;
using UnityEngine;


namespace jedjoud.VoxelTerrain.Generation {
    [CustomEditor(typeof(VoxelCompiler), true)]
    public class VoxelCompilerEditor : Editor {
        public override void OnInspectorGUI() {
            base.OnInspectorGUI();

            var script = (VoxelCompiler)target;

            if (GUILayout.Button("Recompile")) {
                script.Compile(true);
                script.OnPropertiesChanged();
            }
        }
    }
}
