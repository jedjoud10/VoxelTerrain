using UnityEditor;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Generation {
    // GPT-ed btw...
    [CustomPropertyDrawer(typeof(Inject<>))]
    public class InjectedVariablePropertyDrawer : PropertyDrawer {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            SerializedProperty xProperty = property.FindPropertyRelative("x");
            EditorGUI.PropertyField(position, xProperty, label, true);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            SerializedProperty xProperty = property.FindPropertyRelative("x");
            return EditorGUI.GetPropertyHeight(xProperty, true);
        }
    }
}
