// ============================================
// ReadOnly Drawer
// ============================================
// Custom property drawer that disables GUI for [ReadOnly] fields.
// Must live in an Editor folder.
// ============================================

using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        GUI.enabled = false;
        EditorGUI.PropertyField(position, property, label);
        GUI.enabled = true;
    }
}
