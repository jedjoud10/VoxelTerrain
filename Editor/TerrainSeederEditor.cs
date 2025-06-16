using jedjoud.VoxelTerrain.Generation;
using UnityEditor;
using UnityEngine;


namespace jedjoud.VoxelTerrain.Editor {
    [CustomEditor(typeof(TerrainSeeder), true)]
    public class TerrainSeederEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            base.OnInspectorGUI();
            var script = (TerrainSeeder)target;

            if (GUILayout.Button("Randomize Seed")) {
                script.RandomizeSeed();
            }
        }
    }
}
