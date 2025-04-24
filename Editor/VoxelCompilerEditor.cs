using System;
using UnityEditor;
using UnityEngine;


namespace jedjoud.VoxelTerrain.Generation {
    [CustomEditor(typeof(VoxelCompiler), true)]
    public class VoxelCompilerEditor : Editor {
        bool dispatchFoldout;
        bool scopeFoldout;

        public override void OnInspectorGUI() {
            base.OnInspectorGUI();

            var script = (VoxelCompiler)target;

            if (GUILayout.Button("Recompile")) {
                script.Compile(true);
                script.OnPropertiesChanged();
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
                    EditorGUILayout.LabelField($"Depth: {scope.depth}");
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
                    EditorGUILayout.LabelField($"Scope Index: {dispatch.scopeIndex}");
                    EditorGUILayout.LabelField($"Morton Encoding: {dispatch.mortonate}");
                    EditorGUILayout.LabelField($"Thread Group Dimensions: {dispatch.numThreads}");
                    EditorGUI.indentLevel--;
                    EditorGUI.indentLevel--;
                }
            }
        }
    }
}
