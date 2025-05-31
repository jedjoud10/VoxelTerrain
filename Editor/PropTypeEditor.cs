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
        private SerializedProperty propSpawnBehaviorProp;
        private SerializedProperty maxPropsPerSegmentProp;
        private SerializedProperty maxPropsInTotalProp;
        private SerializedProperty instancedMeshProp;
        private SerializedProperty materialProp;

        private UnityEditorInternal.ReorderableList variantsList;

        private void OnEnable() {
            propSpawnBehaviorProp = serializedObject.FindProperty("propSpawnBehavior");
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
                SerializedProperty cullingCenterProp = element.FindPropertyRelative("cullingCenter");
                SerializedProperty cullingRadiusProp = element.FindPropertyRelative("cullingRadius");

                PropType propType = (PropType)target;

                float lineHeight = EditorGUIUtility.singleLineHeight;
                float spacing = EditorGUIUtility.standardVerticalSpacing;

                Rect r = new Rect(rect.x, rect.y, rect.width, lineHeight);

                if (propType.SpawnEntities) {
                    EditorGUI.PropertyField(r, prefabProp, new GUIContent("Prefab"));
                    r.y += lineHeight + spacing;
                }

                EditorGUI.PropertyField(r, cullingCenterProp, new GUIContent("Culling Center"));
                r.y += lineHeight + spacing;

                EditorGUI.PropertyField(r, cullingRadiusProp, new GUIContent("Culling Radius"));
            };

            variantsList.elementHeightCallback = (index) => {
                PropType propType = (PropType)target;

                float lineHeight = EditorGUIUtility.singleLineHeight;
                float spacing = EditorGUIUtility.standardVerticalSpacing;
                int lines = 2; // cullingCenter and cullingRadius

                if (propType.SpawnEntities)
                    lines++; // prefab field

                return lines * (lineHeight + spacing) + spacing;
            };
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();

            // Draw Variants as a reorderable list
            variantsList.DoLayoutList();

            // Draw propSpawnBehavior
            EditorGUILayout.PropertyField(propSpawnBehaviorProp);

            // Draw other fields
            EditorGUILayout.PropertyField(maxPropsPerSegmentProp);
            EditorGUILayout.PropertyField(maxPropsInTotalProp);

            PropType propType = (PropType)target;

            EditorGUILayout.PropertyField(instancedMeshProp);
            EditorGUILayout.PropertyField(materialProp);

            serializedObject.ApplyModifiedProperties();
        }
    }
}