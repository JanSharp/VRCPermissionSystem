using UnityEditor;
using UnityEngine;

namespace JanSharp
{
    [CustomPropertyDrawer(typeof(PermissionDefinitionReferenceAttribute))]
    public class PermissionDefinitionReferenceDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            bool showMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            PermissionDefinitionAsset defAsset = null;
            if (!property.hasMultipleDifferentValues)
                PermissionSystemEditorUtil.TryGetDefAssetByGuid(property.stringValue, out defAsset);
            defAsset = (PermissionDefinitionAsset)EditorGUI.ObjectField(
                position,
                label,
                defAsset,
                typeof(PermissionDefinitionAsset),
                allowSceneObjects: false);
            EditorGUI.showMixedValue = showMixedValue;
            if (EditorGUI.EndChangeCheck())
                property.stringValue = EditorUtil.GetAssetGuidOrEmpty(defAsset);
            EditorGUI.EndProperty();
        }
    }
}
