using System;
using System.Collections;
using System.Linq;
using jedjoud.VoxelTerrain.Generation;
using jedjoud.VoxelTerrain.Props;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;


namespace jedjoud.VoxelTerrain.Editor {
    // ChatGPT came in clutch

    [CustomEditor(typeof(PropType))]
    public class PropTypeEditor : UnityEditor.Editor {
        private SerializedProperty spawnEntities;
        private SerializedProperty renderInstances;
        private SerializedProperty instanceMaxDistance;
        private SerializedProperty renderInstancesShadow;
        private SerializedProperty maxPropsPerSegmentProp;
        private SerializedProperty maxPropsInTotalProp;
        private SerializedProperty instancedMeshProp;
        private SerializedProperty overrideInstancedIndirectMaterial;
        private SerializedProperty instancedIndirectMaterial;

        private UnityEditorInternal.ReorderableList variantsList;

        private void OnEnable() {
            spawnEntities = serializedObject.FindProperty("spawnEntities");
            renderInstances = serializedObject.FindProperty("renderInstances");
            renderInstancesShadow = serializedObject.FindProperty("renderInstancesShadow");
            instanceMaxDistance = serializedObject.FindProperty("instanceMaxDistance");
            maxPropsPerSegmentProp = serializedObject.FindProperty("maxPropsPerSegment");
            maxPropsInTotalProp = serializedObject.FindProperty("maxPropsInTotal");
            instancedMeshProp = serializedObject.FindProperty("instancedMesh");
            overrideInstancedIndirectMaterial = serializedObject.FindProperty("overrideInstancedIndirectMaterial");
            instancedIndirectMaterial = serializedObject.FindProperty("instancedIndirectMaterial");

            SerializedProperty variantsProp = serializedObject.FindProperty("variants");

            variantsList = new UnityEditorInternal.ReorderableList(serializedObject, variantsProp, true, true, true, true);
            variantsList.drawHeaderCallback = (Rect rect) => {
                EditorGUI.LabelField(rect, "Variants");
            };

            variantsList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
                SerializedProperty element = variantsProp.GetArrayElementAtIndex(index);
                SerializedProperty prefabProp = element.FindPropertyRelative("prefab");
                SerializedProperty diffuse = element.FindPropertyRelative("diffuse");
                SerializedProperty normal = element.FindPropertyRelative("normal");
                SerializedProperty mask = element.FindPropertyRelative("mask");

                PropType propType = (PropType)target;

                float lineHeight = EditorGUIUtility.singleLineHeight;
                float spacing = EditorGUIUtility.standardVerticalSpacing;

                Rect r = new Rect(rect.x, rect.y, rect.width, lineHeight);

                if (propType.spawnEntities) {
                    EditorGUI.PropertyField(r, prefabProp, new GUIContent("Prefab"));
                    r.y += lineHeight + spacing;
                }

                if (propType.renderInstances && !propType.spawnEntities) {
                    EditorGUI.PropertyField(r, diffuse, new GUIContent("Diffuse Map"));
                    r.y += lineHeight + spacing;
                    EditorGUI.PropertyField(r, normal, new GUIContent("Normal Map"));
                    r.y += lineHeight + spacing;
                    EditorGUI.PropertyField(r, mask, new GUIContent("Mask Map"));
                    r.y += lineHeight + spacing;
                }
            };

            variantsList.elementHeightCallback = (index) => {
                PropType propType = (PropType)target;

                float lineHeight = EditorGUIUtility.singleLineHeight;
                float spacing = EditorGUIUtility.standardVerticalSpacing;
                int lines = 0;

                if (propType.renderInstances && !propType.spawnEntities)
                    lines += 3;

                if (propType.spawnEntities)
                    lines += 1;

                return lines * (lineHeight + spacing) + spacing;
            };
        }

        public override void OnInspectorGUI() {
            PropType propType = (PropType)target;
            serializedObject.Update();

            EditorGUILayout.PropertyField(spawnEntities);

            if (propType.spawnEntities || propType.renderInstances) {
                // Draw Variants as a reorderable list
                variantsList.DoLayoutList();
            }

            EditorGUILayout.PropertyField(renderInstances);
            if (propType.renderInstances) {
                EditorGUILayout.PropertyField(renderInstancesShadow);
                EditorGUILayout.PropertyField(instancedMeshProp);
                EditorGUILayout.PropertyField(instanceMaxDistance);
                EditorGUILayout.PropertyField(overrideInstancedIndirectMaterial);

                if (propType.overrideInstancedIndirectMaterial) {
                    EditorGUILayout.PropertyField(instancedIndirectMaterial);
                }
            }

            EditorGUILayout.PropertyField(maxPropsPerSegmentProp);
            EditorGUILayout.PropertyField(maxPropsInTotalProp);

            serializedObject.ApplyModifiedProperties();
        }
    }
}