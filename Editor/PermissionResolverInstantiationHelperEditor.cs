using System.Collections.Generic;
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

namespace JanSharp
{
    [InitializeOnLoad]
    public static class PermissionResolverInstantiationHelperOnBuild
    {
        static PermissionResolverInstantiationHelperOnBuild()
        {
            OnBuildUtil.RegisterTypeCumulative<PermissionResolverInstantiationHelper>(OnBuild);
        }

        private static bool OnBuild(IEnumerable<PermissionResolverInstantiationHelper> helpers)
        {
            using (new EditorUtil.BatchedEditorOnlyChecksScope())
            {
                foreach (var helper in helpers)
                    if (helper.AutoPopulateFromChildren)
                        PopulateFromChildren(helper);
            }
            return true;
        }

        public static void PopulateFromChildren(PermissionResolverInstantiationHelper helper, bool includeEditorOnly = false)
        {
            SerializedObject so = new(helper);
            EditorUtil.SetArrayProperty(
                so.FindProperty("resolvers"),
                helper.GetComponentsInChildren<PermissionResolver>(includeInactive: true)
                    .Where(r => !EditorUtil.IsEditorOnly(r))
                    .ToList(),
                (p, v) => p.objectReferenceValue = v);
            so.ApplyModifiedProperties();
        }
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(PermissionResolverInstantiationHelper))]
    public class PermissionResolverInstantiationHelperEditor : Editor
    {
        private SerializedProperty autoPopulateFromChildrenProp;
        private SerializedProperty resolversProp;
        private bool infoFoldoutState;

        public void OnEnable()
        {
            autoPopulateFromChildrenProp = serializedObject.FindProperty("autoPopulateFromChildren");
            resolversProp = serializedObject.FindProperty("resolvers");
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(targets))
                return;

            if (infoFoldoutState = EditorGUILayout.Foldout(infoFoldoutState, "Info", toggleOnLabelClick: true))
                using (new GUILayout.VerticalScope(EditorStyles.helpBox))
                    GUILayout.Label("Permission Resolvers which support being instantiated at runtime - such as "
                        + "the Show Object By Permission script - require being initialized upon instantiation.\n\n"
                        + "Unity only provides events to newly created objects when they are active in the hierarchy, "
                        + "which makes putting this helper on one of the parents of these Permission Resolvers "
                        + "more reliable, if not putting it on the root of the object to be instantiated itself.\n\n"
                        + "In cases where the instantiated object is inactive and remains as such, send the "
                        + "'InitializeInstantiated' custom event to this helper, which is more convenient than "
                        + "sending it to every Permission Resolver.", EditorStyles.wordWrappedLabel);

            serializedObject.Update();
            EditorGUILayout.PropertyField(autoPopulateFromChildrenProp);
            if (serializedObject.ApplyModifiedProperties())
                foreach (var target in targets.Cast<PermissionResolverInstantiationHelper>())
                    if (target.AutoPopulateFromChildren)
                        PermissionResolverInstantiationHelperOnBuild.PopulateFromChildren(target);

            serializedObject.Update();
            using (new EditorGUI.DisabledScope(
                !autoPopulateFromChildrenProp.hasMultipleDifferentValues
                    && autoPopulateFromChildrenProp.boolValue))
            {
                EditorGUILayout.PropertyField(resolversProp);
            }
            serializedObject.ApplyModifiedProperties();

            if (GUILayout.Button(new GUIContent("Populate Resolvers From Children", "Overwrites the Resolvers list.")))
                foreach (var target in targets.Cast<PermissionResolverInstantiationHelper>())
                    PermissionResolverInstantiationHelperOnBuild.PopulateFromChildren(target);
            if (GUILayout.Button(new GUIContent("Populate Resolvers From Children (including EditorOnly)", "Overwrites the Resolvers list.")))
                foreach (var target in targets.Cast<PermissionResolverInstantiationHelper>())
                    PermissionResolverInstantiationHelperOnBuild.PopulateFromChildren(target, includeEditorOnly: true);
        }
    }
}
