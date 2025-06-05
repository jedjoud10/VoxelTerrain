using jedjoud.VoxelTerrain.Generation;
using UnityEditor;
using UnityEngine;


namespace jedjoud.VoxelTerrain.Editor {
    [CustomEditor(typeof(ManagedTerrainSeeder), true)]
    public class ManagedTerrainSeederEditor : UnityEditor.Editor {
        private bool textureFoldout;
        private bool bufferFoldout;

        public override void OnInspectorGUI() {
            base.OnInspectorGUI();
            var script = (ManagedTerrainSeeder)target;

            if (GUILayout.Button("Randomize Seed")) {
                script.RandomizeSeed();
            }

            /*
            if (GUILayout.Button("Dispose Resources")) {
                script.DisposeResources();
            }


            if (script.Textures == null || script.Buffers == null) {
                EditorGUILayout.LabelField("Interally Allocated Resources (NONE! You must recompile...)", EditorStyles.boldLabel);
                return;
            } else {
                EditorGUILayout.LabelField("Interally Allocated Resources", EditorStyles.boldLabel);
            }

            textureFoldout = EditorGUILayout.Foldout(textureFoldout, "Textures: " + script.Textures.Count);

            if (textureFoldout) {
                string[] keys = script.Textures.Keys.ToArray();
                Array.Sort(keys, StringComparer.Ordinal);
                for (int i = 0; i < script.Textures.Count; i++) {
                    ExecutorTexture texture = script.Textures[keys[i]];

                    string graphicsFormat = "";
                    string dimensions = "";
                    string mips = "";
                    string writeKernel = "";
                    string type = "";

                    if (texture is TemporaryExecutorTexture temp) {
                        type = "Temporary";
                        RenderTexture rt = (RenderTexture)texture.texture;
                        graphicsFormat = rt.graphicsFormat.ToString();
                        dimensions = $"{rt.width}x{rt.height}x{rt.volumeDepth}";
                        mips = temp.mips.ToString();
                        writeKernel = temp.writeKernel;

                    } else if (texture is OutputExecutorTexture) {
                        type = "Output";
                        RenderTexture rt = (RenderTexture)texture.texture;

                        graphicsFormat = rt.graphicsFormat.ToString();
                        dimensions = $"{rt.width}x{rt.height}x{rt.volumeDepth}";
                    } else {
                        type = "Default";
                        graphicsFormat = texture.texture.graphicsFormat.ToString();
                        dimensions = $"{texture.texture.width}x{texture.texture.height}";
                    }

                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField($"Name: {keys[i]} ({type}) ({texture.requestingNodeHash})", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField($"Format: {graphicsFormat}");
                    EditorGUILayout.LabelField($"Dimensions: {dimensions}");

                    if (mips != "") {
                        EditorGUILayout.LabelField($"Mips: {mips}");
                    }

                    if (writeKernel != "") {
                        EditorGUILayout.LabelField($"Write Kernel: {writeKernel}");
                    }
                    EditorGUI.indentLevel--;
                    EditorGUI.indentLevel--;
                }
            }


            bufferFoldout = EditorGUILayout.Foldout(bufferFoldout, "Buffers: " + script.Buffers.Count);

            if (bufferFoldout) {
                string[] keys = script.Buffers.Keys.ToArray();
                Array.Sort(keys, StringComparer.Ordinal);
                for (int i = 0; i < script.Buffers.Count; i++) {
                    ExecutorBuffer buffer = script.Buffers[keys[i]];

                    int count = 0;
                    int stride = 0;
                    string type = "";

                    count = buffer.buffer.count;
                    stride = buffer.buffer.stride;
                    if (buffer is ExecutorBufferCounter) {
                        type = "Counter";
                    } else {
                        type = "Default";
                    }

                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField($"Name: {keys[i]} ({type})", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField($"Count: {count}");
                    EditorGUILayout.LabelField($"Stride: {stride}");
                    EditorGUI.indentLevel--;
                    EditorGUI.indentLevel--;
                }
            }
            */
        }
    }
}
