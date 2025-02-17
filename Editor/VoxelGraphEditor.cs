using UnityEditor;
using UnityEngine;


namespace jedjoud.VoxelTerrain.Generation {
    [CustomEditor(typeof(VoxelGraph), true)]
    public class VoxelGraphEditor : Editor {
        public override void OnInspectorGUI() {
            base.OnInspectorGUI();

            var script = (VoxelGraph)target;

            if (GUILayout.Button("Recompile")) {
                script.Compile(true);
                script.OnPropertiesChanged();
            }
        }
    }
}
