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
            int count = resolvers.Count;
            AddNullToMeetCapacity(resolvers);

            SerializedObject so = new SerializedObject(permissionManager);
            EditorUtil.SetArrayProperty(
                so.FindProperty("allPermissionResolvers"),
                resolvers,
                (p, v) => p.objectReferenceValue = v);
            so.FindProperty("allPermissionResolversCount").intValue = count;
            so.ApplyModifiedProperties();
            return true;
        }

        public static void AddNullToMeetCapacity<T>(List<T> list)
            where T : class
        {
            int count = list.Count;
            int capacity = ArrList.MinCapacity;
            while (capacity < count)
                capacity *= 2;
            for (int i = count; i < capacity; i++)
                list.Add(null);
        }
    }
}
