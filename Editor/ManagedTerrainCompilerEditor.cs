using System;
using System.Linq;
using jedjoud.VoxelTerrain.Generation;
using UnityEditor;
using UnityEngine;


namespace jedjoud.VoxelTerrain.Editor {
    [CustomEditor(typeof(ManagedTerrainCompiler), true)]
    public class ManagedTerrainCompilerEditor : UnityEditor.Editor {
        bool dispatchFoldout;
        bool scopeFoldout;
        bool textureFoldout;
        bool bufferFoldouat;

        public override void OnInspectorGUI() {
            base.OnInspectorGUI();

            var script = (ManagedTerrainCompiler)target;

            if (Application.isPlaying) {
                EditorGUILayout.LabelField($"Running in play mode... don't do anything...", EditorStyles.boldLabel);
                return;
            }

            if (script.GetComponent<ManagedTerrainGraph>() == null) {
                EditorGUILayout.LabelField($"Missing ManagedTerrainGraph on ManagedTerrain!!!", EditorStyles.boldLabel);
                return;
            }

            if (!script.SupportsDXC()) {
                EditorGUILayout.LabelField($"DXC HLSL compiler not supported. Fallbacking to FXC.");
            }

            if (GUILayout.Button("Recompile")) {
                script.Compile();
                script.GetComponent<ManagedTerrainPreview>()?.OnPropertiesChanged();
            }

            if (GUILayout.Button("Retranspile")) {
                script.Parse();
                script.GetComponent<ManagedTerrainPreview>()?.OnPropertiesChanged();
            }

            if (script.dirty) {
                EditorGUILayout.LabelField($"Compile hash changed! Recompile pls...", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"(press recompile button)", EditorStyles.boldLabel);
            }

            EditorGUILayout.LabelField($"Properties: {script.ctx.properties.Count}");

            scopeFoldout = EditorGUILayout.Foldout(scopeFoldout, "Scopes: " + script.ctx.scopes.Count);
            if (scopeFoldout) {
                var scopes = script.ctx.scopes;
                for (int i = 0; i < scopes.Count; i++) {
                    TreeScope scope = scopes[i];

                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField($"Name: {scope.name} (i={i})", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField($"Name: {scope.name}");
                    EditorGUI.indentLevel--;
                    EditorGUI.indentLevel--;
                }
            }

            dispatchFoldout = EditorGUILayout.Foldout(dispatchFoldout, "Dispatches: " + script.ctx.dispatches.Count);

            if (dispatchFoldout) {
                var dispatches = script.ctx.dispatches;
                for (int i = 0; i < dispatches.Count; i++) {
                    KernelDispatch dispatch = dispatches[i];

                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField($"Name: {dispatch.name} (i={i})", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField($"Scope Name: {dispatch.scopeName}");
                    EditorGUI.indentLevel--;
                    EditorGUI.indentLevel--;
                }
            }


            textureFoldout = EditorGUILayout.Foldout(textureFoldout, "Textures: " + script.ctx.textures.Count);

            if (textureFoldout) {
                string[] keys = script.ctx.textures.Keys.ToArray();
                Array.Sort(keys, StringComparer.Ordinal);
                for (int i = 0; i < script.ctx.textures.Count; i++) {
                    TextureDescriptor descriptor = script.ctx.textures[keys[i]];

                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField($"Name: {keys[i]}", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField($"Filter: {descriptor.filter}");
                    EditorGUILayout.LabelField($"Wrap: {descriptor.wrap}");

                    EditorGUILayout.LabelField("Read Kernels:");
                    EditorGUI.indentLevel++;
                    foreach (string kernel in descriptor.readKernels) {
                        EditorGUILayout.LabelField(kernel);
                    }

                    EditorGUI.indentLevel--;
                    EditorGUI.indentLevel--;
                    EditorGUI.indentLevel--;
                }
            }


            /*
            bufferFoldout = EditorGUILayout.Foldout(bufferFoldout, "Buffers: " + script.buffers.Count);

            if (bufferFoldout) {
                string[] keys = script.buffers.Keys.ToArray();
                Array.Sort(keys, StringComparer.Ordinal);
                for (int i = 0; i < script.buffers.Count; i++) {
                    ExecutorBuffer buffer = script.buffers[keys[i]];

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
