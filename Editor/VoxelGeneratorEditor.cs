using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(VoxelGenerator), true)]
public class VoxelGeneratorEditor : Editor {
    public override void OnInspectorGUI() {
        base.OnInspectorGUI();

        var script = (VoxelGenerator)target;

        if (GUILayout.Button("Recompile")) {
            script.Compile(true);
            script.OnPropertiesChanged();
        }

        if (GUILayout.Button("Randomize Seed")) {
            script.RandomizeSeed();
        }
    }
}