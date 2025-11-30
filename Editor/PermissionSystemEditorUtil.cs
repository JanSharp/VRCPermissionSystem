using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace JanSharp
{
    [InitializeOnLoad]
    [DefaultExecutionOrder(-10)]
    public static class PermissionSystemEditorUtil
    {
        private static Dictionary<string, PermissionDefinitionAsset> guidToPermissionDefAssetLut = new();
        private static Dictionary<string, PermissionDefinitionAsset> internalNameToPermissionDefAssetLut = new();

        static PermissionSystemEditorUtil()
        {
            ValidateInternalNames();
            OnBuildUtil.RegisterAction(ValidateInternalNames, order: -10000);
        }

        private static bool ValidateInternalNames()
        {
            FindAllPermissionDefAssets();
            bool result = true;
            foreach (var group in guidToPermissionDefAssetLut.Values
                .GroupBy(d => d.internalName)
                .Where(g => g.Count() > 1)
                .OrderBy(g => g.Key))
            {
                result = false;
                Debug.LogError($"[PermissionSystem] There are {group.Count()} Permission Definition Assets with "
                    + $"the Internal Name {group.Key}. This is invalid, they each must use unique Internal Names. "
                    + $"The Permission Definition Assets in question:\n{string.Join('\n', group.Select(d => d.name))}",
                    group.First());
            }
            return result;
        }

        public static bool TryGetDefAssetByGuid(string permissionDefinitionAssetGuid, out PermissionDefinitionAsset defAsset)
        {
            if (guidToPermissionDefAssetLut.TryGetValue(permissionDefinitionAssetGuid, out defAsset) && defAsset != null)
                return true;
            FindAllPermissionDefAssets();
            return guidToPermissionDefAssetLut.TryGetValue(permissionDefinitionAssetGuid, out defAsset);
        }

        public static bool TryGetDefAssetByInternalName(string permissionDefinitionInternalName, out PermissionDefinitionAsset defAsset)
        {
            if (internalNameToPermissionDefAssetLut.TryGetValue(permissionDefinitionInternalName, out defAsset)
                && defAsset != null && defAsset.internalName == permissionDefinitionInternalName) // The internalName could have changed!
            {
                return true;
            }
            FindAllPermissionDefAssets();
            return internalNameToPermissionDefAssetLut.TryGetValue(permissionDefinitionInternalName, out defAsset);
        }

        public static void FindAllPermissionDefAssets()
        {
            string[] guids = AssetDatabase.FindAssets("t:PermissionDefinitionAsset");
            guidToPermissionDefAssetLut.Clear();
            internalNameToPermissionDefAssetLut.Clear();
            foreach (string guid in guids)
            {
                PermissionDefinitionAsset defAsset = AssetDatabase.LoadAssetAtPath<PermissionDefinitionAsset>(AssetDatabase.GUIDToAssetPath(guid));
                guidToPermissionDefAssetLut.Add(guid, defAsset);
                // Not Add, there could be duplicate internalNames.
                // That would be invalid and it is checked for on assembly load and build, but it has to not error here.
                internalNameToPermissionDefAssetLut[defAsset.internalName] = defAsset;
            }
        }
    }
}
