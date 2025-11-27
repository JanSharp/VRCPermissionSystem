using System.Collections.Generic;
using System.Linq;
using JanSharp.Internal;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

namespace JanSharp
{
    [InitializeOnLoad]
    public static class PermissionDefinitionOnBuild
    {
        private static HashSet<string> internalNamesLut = new();
        private static List<(PermissionDefinition def, PermissionDefinitionAsset asset)> permissionDefsWithAsset = new();
        private static PermissionManager permissionManager;

        static PermissionDefinitionOnBuild()
        {
            OnBuildUtil.RegisterAction(OnPreBuild, order: -13);
            OnBuildUtil.RegisterType<PermissionManager>(OnFetchPermissionManager, order: -12);
            OnBuildUtil.RegisterTypeCumulative<PermissionDefinition>(OnBuildCumulative, order: -11);
            OnBuildUtil.RegisterAction(OnPostBuild, order: -10);
        }

        private static bool OnPreBuild()
        {
            internalNamesLut.Clear();
            permissionDefsWithAsset.Clear();
            return true;
        }

        private static bool OnPostBuild()
        {
            // Cleanup.
            internalNamesLut.Clear();
            permissionDefsWithAsset.Clear();
            return true;
        }

        private static bool OnFetchPermissionManager(PermissionManager permissionManager)
        {
            PermissionDefinitionOnBuild.permissionManager = permissionManager;
            return true;
        }

        private static bool OnBuildCumulative(IEnumerable<PermissionDefinition> permissionDefs)
        {
            bool result = true;
            foreach (PermissionDefinition permissionDef in permissionDefs)
                result &= OnBuild(permissionDef);
            if (!result)
                return false;

            SerializedObject so = new SerializedObject(permissionManager);
            EditorUtil.SetArrayProperty(
                so.FindProperty("permissionDefs"),
                permissionDefsWithAsset
                    .OrderBy(d => d.asset.order)
                    .ThenBy(d => d.asset.displayName)
                    .ThenBy(d => d.asset.internalName)
                    .ToList(),
                (p, v) => p.objectReferenceValue = v.def);
            so.ApplyModifiedProperties();

            return true;
        }

        private static bool OnBuild(PermissionDefinition permissionDef)
        {
            if (!Validate(permissionDef, out PermissionDefinitionAsset permissionDefAsset))
                return false;
            permissionDefsWithAsset.Add((permissionDef, permissionDefAsset));

            SerializedObject so = new SerializedObject(permissionDef);
            MirrorTheDefinition(permissionDefAsset, so);
            so.ApplyModifiedProperties();
            EnsureGameObjectNameMatchesInternalName(permissionDef, permissionDefAsset);
            return true;
        }

        private static bool Validate(
            PermissionDefinition permissionDef,
            out PermissionDefinitionAsset permissionDefAsset)
        {
            string definitionAssetGuid = permissionDef.DefinitionAssetGuid;
            if (string.IsNullOrEmpty(definitionAssetGuid)
                || !PermissionSystemEditorUtil.TryGetPermissionDefAsset(definitionAssetGuid, out permissionDefAsset))
            {
                Debug.LogError($"[PermissionSystem] Invalid permission definition, missing Permission Definition Asset.", permissionDef);
                permissionDefAsset = null;
                return false;
            }

            if (internalNamesLut.Contains(permissionDefAsset.internalName))
            {
                Debug.LogError($"[PermissionSystem] There are multiple permission definitions with the internal "
                    + $"name '{permissionDefAsset.internalName}'. A permission definition can only be used once in a "
                    + $"scene, and every permission definition must have a unique internal name.", permissionDef);
                return false;
            }
            internalNamesLut.Add(permissionDefAsset.internalName);

            return true;
        }

        private static void MirrorTheDefinition(
            PermissionDefinitionAsset permissionDefAsset,
            SerializedObject so)
        {
            so.FindProperty(nameof(PermissionDefinition.internalName)).stringValue = permissionDefAsset.internalName;
            so.FindProperty(nameof(PermissionDefinition.displayName)).stringValue = permissionDefAsset.displayName;
        }

        private static void EnsureGameObjectNameMatchesInternalName(
            PermissionDefinition permissionDef,
            PermissionDefinitionAsset permissionDefAsset)
        {
            if (permissionDef.name == permissionDefAsset.internalName)
                return;
            SerializedObject goSo = new SerializedObject(permissionDef.gameObject);
            goSo.FindProperty("m_Name").stringValue = permissionDefAsset.internalName;
            goSo.ApplyModifiedProperties();
        }
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(PermissionDefinition))]
    public class PermissionDefinitionEditor : Editor
    {
        private SerializedObject so;
        private SerializedProperty definitionAssetGuidProp;
        private SerializedProperty defaultValueProp;
        private string[] permissionDefAssetGuids;
        private SerializedObject defAssetsSo;
        private SerializedProperty internalNameProp;
        private SerializedProperty displayNameProp;
        private SerializedProperty orderProp;
        private SerializedProperty defaultDefaultValueProp;
        private PermissionDefinitionAsset shownPermissionDefAsset;

        private void OnEnable()
        {
            so = serializedObject;
            definitionAssetGuidProp = so.FindProperty("definitionAssetGuid");
            defaultValueProp = so.FindProperty(nameof(PermissionDefinition.defaultValue));
            permissionDefAssetGuids = GetCurrentPermissionDefAssetGuids();
            FetchPermissionDefAsset();
        }

        private string[] GetCurrentPermissionDefAssetGuids()
        {
            return targets.Select(t => ((PermissionDefinition)t).DefinitionAssetGuid).ToArray();
        }

        private void FetchPermissionDefAsset()
        {
            SetPermissionDefAssets(permissionDefAssetGuids
                .Where(g => !string.IsNullOrEmpty(g))
                .Select(g => PermissionSystemEditorUtil.TryGetPermissionDefAsset(g, out var defAsset) ? defAsset : null)
                .Where(d => d != null)
                .Distinct()
                .ToArray());
        }

        private void SetPermissionDefAssets(PermissionDefinitionAsset[] permissionDefAssets)
        {
            shownPermissionDefAsset = permissionDefAssets.FirstOrDefault();
            defAssetsSo = permissionDefAssets.Length == 0
                ? null
                : new SerializedObject(permissionDefAssets);
            internalNameProp = defAssetsSo == null ? null : defAssetsSo.FindProperty(nameof(PermissionDefinitionAsset.internalName));
            displayNameProp = defAssetsSo == null ? null : defAssetsSo.FindProperty(nameof(PermissionDefinitionAsset.displayName));
            orderProp = defAssetsSo == null ? null : defAssetsSo.FindProperty(nameof(PermissionDefinitionAsset.order));
            defaultDefaultValueProp = defAssetsSo == null ? null : defAssetsSo.FindProperty(nameof(PermissionDefinitionAsset.defaultDefaultValue));
        }

        private bool CompareStringArrays(string[] left, string[] right)
        {
            if (left.Length != right.Length)
                return false;
            for (int i = 0; i < left.Length; i++)
                if (left[i] != right[i])
                    return false;
            return true;
        }

        private void SetPermissionDefAsset(PermissionDefinitionAsset permissionDefAsset)
        {
            string permissionDefGuid = PermissionSystemEditorUtil.GetAssetGuid(permissionDefAsset);
            SetPermissionDefAssets(permissionDefGuid == ""
                ? new PermissionDefinitionAsset[0]
                : new PermissionDefinitionAsset[1] { permissionDefAsset });
            definitionAssetGuidProp.stringValue = permissionDefGuid;
            defaultValueProp.boolValue = permissionDefAsset.defaultDefaultValue;

            if (shownPermissionDefAsset == null)
                return;
            SerializedObject goSo = new SerializedObject(targets.Select(t => ((PermissionDefinition)t).gameObject).ToArray());
            goSo.FindProperty("m_Name").stringValue = permissionDefAsset.internalName;
            goSo.ApplyModifiedProperties();
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(targets))
                return;

            so.Update();

            string[] newPermissionDefAssetGuids = GetCurrentPermissionDefAssetGuids();
            if (!CompareStringArrays(permissionDefAssetGuids, newPermissionDefAssetGuids))
            {
                permissionDefAssetGuids = newPermissionDefAssetGuids;
                FetchPermissionDefAsset();
            }

            EditorGUI.showMixedValue = definitionAssetGuidProp.hasMultipleDifferentValues;
            PermissionDefinitionAsset newShownPermissionDefAsset = (PermissionDefinitionAsset)EditorGUILayout.ObjectField(
                new GUIContent("Permission Definition"),
                shownPermissionDefAsset,
                typeof(PermissionDefinitionAsset),
                allowSceneObjects: false);
            EditorGUI.showMixedValue = false;
            if (newShownPermissionDefAsset != shownPermissionDefAsset)
                SetPermissionDefAsset(newShownPermissionDefAsset);

            if (defAssetsSo != null)
                EditorGUILayout.PropertyField(defaultValueProp);

            so.ApplyModifiedProperties();

            if (defAssetsSo == null)
                return;

            EditorGUILayout.Space();
            if (permissionDefAssetGuids.Any(g => g == ""))
                using (new GUILayout.VerticalScope(EditorStyles.helpBox))
                    GUILayout.Label("Some of the selected Permission Definitions do not have a Permission Definition Asset set.", EditorStyles.wordWrappedLabel);
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Inlined Permission Definition Asset", EditorStyles.boldLabel);
                defAssetsSo.Update();
                EditorGUILayout.PropertyField(internalNameProp);
                EditorGUILayout.PropertyField(displayNameProp);
                EditorGUILayout.PropertyField(orderProp);
                EditorGUILayout.PropertyField(defaultDefaultValueProp);
                defAssetsSo.ApplyModifiedProperties();
            }
        }
    }
}
