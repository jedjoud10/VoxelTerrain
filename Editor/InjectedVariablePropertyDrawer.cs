using UnityEditor;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Generation {
    // GPT-ed btw...
    [CustomPropertyDrawer(typeof(Inject<>))]
    public class InjectedVariablePropertyDrawer : PropertyDrawer {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            SerializedProperty xProperty = property.FindPropertyRelative("value");
            EditorGUI.PropertyField(position, xProperty, label, true);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            SerializedProperty xProperty = property.FindPropertyRelative("value");
            return EditorGUI.GetPropertyHeight(xProperty, true);
        }
    }
}
