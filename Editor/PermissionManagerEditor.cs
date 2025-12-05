using System.Collections.Generic;
using System.Linq;
using JanSharp.Internal;
using UnityEditor;

namespace JanSharp
{
    [InitializeOnLoad]
    public static class PermissionManagerOnBuild
    {
        private static List<PermissionResolver> resolvers;

        static PermissionManagerOnBuild()
        {
            OnBuildUtil.RegisterTypeCumulative<PermissionResolver>(OnResolversBuild, order: -1);
            OnBuildUtil.RegisterType<PermissionManager>(OnBuild, order: 0);
        }

        private static bool OnResolversBuild(IEnumerable<PermissionResolver> resolvers)
        {
            PermissionManagerOnBuild.resolvers = resolvers.ToList();
            return true;
        }

        private static bool OnBuild(PermissionManager permissionManager)
        {
            SerializedObject so = new SerializedObject(permissionManager);
            EditorUtil.SetArrayProperty(
                so.FindProperty("permissionResolversExistingAtSceneLoad"),
                resolvers,
                (p, v) => p.objectReferenceValue = v);
            so.ApplyModifiedProperties();
            return true;
        }
    }
}
