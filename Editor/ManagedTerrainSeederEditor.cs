using jedjoud.VoxelTerrain.Generation;
using UnityEditor;
using UnityEngine;


namespace jedjoud.VoxelTerrain.Editor {
    [CustomEditor(typeof(ManagedTerrainSeeder), true)]
    public class ManagedTerrainSeederEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            base.OnInspectorGUI();
            var script = (ManagedTerrainSeeder)target;

            if (GUILayout.Button("Randomize Seed")) {
                script.RandomizeSeed();
            }
        }
    }
}
