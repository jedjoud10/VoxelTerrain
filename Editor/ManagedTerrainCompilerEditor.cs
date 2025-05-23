using System;
using jedjoud.VoxelTerrain.Generation;
using UnityEditor;
using UnityEngine;


namespace jedjoud.VoxelTerrain.Editor {
    [CustomEditor(typeof(ManagedTerrainCompiler), true)]
    public class ManagedTerrainCompilerEditor : UnityEditor.Editor {
        bool dispatchFoldout;
        bool scopeFoldout;

        public override void OnInspectorGUI() {
            base.OnInspectorGUI();

            var script = (ManagedTerrainCompiler)target;

            if (Application.isPlaying) {
                EditorGUILayout.LabelField($"Running in play mode... don't do anything...", EditorStyles.boldLabel);
                return;
            }

            
            if (GUILayout.Button("Recompile")) {
                script.Compile(true);
                script.OnPropertiesChanged();
            }

            if (GUILayout.Button("Retranspile")) {
                script.ParsedTranspilation();
                script.OnPropertiesChanged();
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
                    EditorGUILayout.LabelField($"Name: {scope.name}", EditorStyles.boldLabel);
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
                    EditorGUI.indentLevel--;
                    EditorGUI.indentLevel--;
                }
            }
        }
    }
}
