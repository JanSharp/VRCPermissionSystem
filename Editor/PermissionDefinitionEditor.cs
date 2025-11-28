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
        private static Dictionary<PermissionDefinitionAsset, PermissionDefinition> defsInSceneByDefAsset = new();
        private static List<(PermissionDefinition def, PermissionDefinitionAsset asset)> permissionDefsWithAsset = new();
        private static PermissionManager permissionManager;
        private static bool searchedForCommonDefParent = false;
        private static Transform commonDefParent = null;
        private static bool markForRerunDueToScriptInstantiationInPostBuild = false;

        static PermissionDefinitionOnBuild()
        {
            OnBuildUtil.RegisterAction(OnPreBuild, order: -1003);
            OnBuildUtil.RegisterType<PermissionManager>(OnFetchPermissionManager, order: -1002);
            OnBuildUtil.RegisterTypeCumulative<PermissionDefinition>(OnBuildCumulative, order: -1001);
            OnBuildUtil.RegisterAction(OnPostBuild, order: 1000);
        }

        private static void Reset()
        {
            defsInSceneByDefAsset.Clear();
            permissionDefsWithAsset.Clear();
            searchedForCommonDefParent = false;
            commonDefParent = null;
            markForRerunDueToScriptInstantiationInPostBuild = false;
        }

        private static bool OnPreBuild()
        {
            Reset();
            return true;
        }

        private static bool OnPostBuild()
        {
            if (markForRerunDueToScriptInstantiationInPostBuild)
                OnBuildUtil.MarkForRerunDueToScriptInstantiation();
            // Cleanup.
            Reset();
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
                || !PermissionSystemEditorUtil.TryGetDefAssetByGuid(definitionAssetGuid, out permissionDefAsset))
            {
                Debug.LogError($"[PermissionSystem] Invalid permission definition, missing Permission Definition Asset.", permissionDef);
                permissionDefAsset = null;
                return false;
            }

            if (defsInSceneByDefAsset.ContainsKey(permissionDefAsset))
            {
                Debug.LogError($"[PermissionSystem] There are multiple permission definitions using the "
                    + $"{permissionDefAsset.name} (Internal Name: {permissionDefAsset.internalName}) "
                    + $"Permission Definition Asset in the scene. They can only be used once in a scene.", permissionDef);
                return false;
            }
            defsInSceneByDefAsset.Add(permissionDefAsset, permissionDef);

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
            if (permissionDef.name == permissionDefAsset.name)
                return;
            SerializedObject goSo = new SerializedObject(permissionDef.gameObject);
            goSo.FindProperty("m_Name").stringValue = permissionDefAsset.name;
            goSo.ApplyModifiedProperties();
        }



        private static void FindCommonDefParent()
        {
            if (searchedForCommonDefParent)
                return;
            searchedForCommonDefParent = true;
            commonDefParent = EditorUtil.FindCommonParent(permissionDefsWithAsset.Select(d => d.def.transform));
        }

        public static PermissionDefinition EnsurePermissionDefinitionExists(string guid)
        {
            if (string.IsNullOrEmpty(guid))
                return null;
            if (PermissionSystemEditorUtil.TryGetDefAssetByGuid(guid, out PermissionDefinitionAsset defAsset))
                return EnsurePermissionDefinitionExists(defAsset);
            return null;
        }

        public static PermissionDefinition EnsurePermissionDefinitionExists(PermissionDefinitionAsset defAsset)
        {
            if (defsInSceneByDefAsset.TryGetValue(defAsset, out PermissionDefinition permissionDef))
                return permissionDef;
            FindCommonDefParent();
            GameObject permissionDefGo = new GameObject(defAsset.name);
            Undo.RegisterCreatedObjectUndo(permissionDefGo, "Add Required Permission Definition To Scene");
            if (commonDefParent != null)
                permissionDefGo.transform.SetParent(commonDefParent, worldPositionStays: false);
            permissionDef = UdonSharpUndo.AddComponent<PermissionDefinition>(permissionDefGo);
            permissionDef.DefinitionAssetGuid = PermissionSystemEditorUtil.GetAssetGuid(defAsset);
            defsInSceneByDefAsset.Add(defAsset, permissionDef);
            markForRerunDueToScriptInstantiationInPostBuild = true;
            return permissionDef;
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
                .Select(g => PermissionSystemEditorUtil.TryGetDefAssetByGuid(g, out var defAsset) ? defAsset : null)
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
            goSo.FindProperty("m_Name").stringValue = permissionDefAsset.name;
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
