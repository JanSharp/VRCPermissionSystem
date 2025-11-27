using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace JanSharp
{
    public static class PermissionSystemEditorUtil
    {
        private static Dictionary<string, PermissionDefinitionAsset> guidToPermissionDefAssetLut = new();

        public static bool TryGetPermissionDefAsset(string permissionDefinitionAssetGuid, out PermissionDefinitionAsset defAsset)
        {
            if (guidToPermissionDefAssetLut.TryGetValue(permissionDefinitionAssetGuid, out defAsset) && defAsset != null)
                return true;
            FindAllPermissionDefAssets();
            return guidToPermissionDefAssetLut.TryGetValue(permissionDefinitionAssetGuid, out defAsset);
        }

        public static void FindAllPermissionDefAssets()
        {
            string[] guids = AssetDatabase.FindAssets("t:PermissionDefinitionAsset");
            guidToPermissionDefAssetLut = guids.ToDictionary(
                guid => guid,
                guid => AssetDatabase.LoadAssetAtPath<PermissionDefinitionAsset>(AssetDatabase.GUIDToAssetPath(guid)));
        }

        public static string GetAssetGuid(Object obj)
            => obj != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out string guid, out long _)
                ? guid
                : "";
    }
}
