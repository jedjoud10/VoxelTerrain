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
        private SerializedProperty materialProp;

        private UnityEditorInternal.ReorderableList variantsList;

        private void OnEnable() {
            spawnEntities = serializedObject.FindProperty("spawnEntities");
            renderInstances = serializedObject.FindProperty("renderInstances");
            renderInstancesShadow = serializedObject.FindProperty("renderInstancesShadow");
            instanceMaxDistance = serializedObject.FindProperty("instanceMaxDistance");
            maxPropsPerSegmentProp = serializedObject.FindProperty("maxPropsPerSegment");
            maxPropsInTotalProp = serializedObject.FindProperty("maxPropsInTotal");
            instancedMeshProp = serializedObject.FindProperty("instancedMesh");
            materialProp = serializedObject.FindProperty("material");

            SerializedProperty variantsProp = serializedObject.FindProperty("variants");

            variantsList = new UnityEditorInternal.ReorderableList(serializedObject, variantsProp, true, true, true, true);
            variantsList.drawHeaderCallback = (Rect rect) => {
                EditorGUI.LabelField(rect, "Variants");
            };

            variantsList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
                SerializedProperty element = variantsProp.GetArrayElementAtIndex(index);
                SerializedProperty prefabProp = element.FindPropertyRelative("prefab");

                PropType propType = (PropType)target;

                float lineHeight = EditorGUIUtility.singleLineHeight;
                float spacing = EditorGUIUtility.standardVerticalSpacing;

                Rect r = new Rect(rect.x, rect.y, rect.width, lineHeight);

                if (propType.spawnEntities) {
                    EditorGUI.PropertyField(r, prefabProp, new GUIContent("Prefab"));
                    r.y += lineHeight + spacing;
                }
            };

            variantsList.elementHeightCallback = (index) => {
                PropType propType = (PropType)target;

                float lineHeight = EditorGUIUtility.singleLineHeight;
                float spacing = EditorGUIUtility.standardVerticalSpacing;
                int lines = 2; // cullingCenter and cullingRadius

                if (propType.spawnEntities)
                    lines++; // prefab field

                return lines * (lineHeight + spacing) + spacing;
            };
        }

        public override void OnInspectorGUI() {
            PropType propType = (PropType)target;
            serializedObject.Update();

            EditorGUILayout.PropertyField(spawnEntities);
            if (propType.spawnEntities) {
                // Draw Variants as a reorderable list
                variantsList.DoLayoutList();
            }

            EditorGUILayout.PropertyField(renderInstances);
            if (propType.renderInstances) {
                EditorGUILayout.PropertyField(renderInstancesShadow);
                EditorGUILayout.PropertyField(instancedMeshProp);
                EditorGUILayout.PropertyField(materialProp);
                EditorGUILayout.PropertyField(instanceMaxDistance);
            }

            EditorGUILayout.PropertyField(maxPropsPerSegmentProp);
            EditorGUILayout.PropertyField(maxPropsInTotalProp);

            serializedObject.ApplyModifiedProperties();
        }
    }
}