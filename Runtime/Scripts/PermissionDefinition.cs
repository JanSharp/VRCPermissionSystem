using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PermissionDefinition : UdonSharpBehaviour
    {
        [SerializeField] private string definitionAssetGuid;
#if UNITY_EDITOR && !COMPILER_UDONSHARP
        public string DefinitionAssetGuid
        {
            get => definitionAssetGuid;
            set => definitionAssetGuid = value;
        }
#endif
        [System.NonSerialized] public int index;
        public string internalName;
        public string displayName;
        public bool defaultValue;

        public PermissionResolver[] resolvers;
        public int resolversCount;
        private DataDictionary resolverIndexLut;

        [System.NonSerialized] public bool valueForLocalPlayer;

        private void CreateResolverIndexLut()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] PermissionDefinition {this.name}  CreateResolverIndexLut");
#endif
            resolverIndexLut = new DataDictionary();
            for (int i = 0; i < resolversCount; i++)
                resolverIndexLut.Add(resolvers[i], i);
        }

        public void PrePopulateResolverIndexLut()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] PermissionDefinition {this.name}  PrePopulateResolverIndexLut");
#endif
            if (resolverIndexLut == null)
                CreateResolverIndexLut();
        }

        public void RegisterResolver(PermissionResolver resolver)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] PermissionDefinition {this.name}  RegisterResolver");
#endif
            if (resolverIndexLut == null)
                CreateResolverIndexLut();
            if (resolverIndexLut.ContainsKey(resolver))
                return;
            resolverIndexLut.Add(resolver, resolversCount);
            ArrList.Add(ref resolvers, ref resolversCount, resolver);
        }

        public void DeregisterResolver(PermissionResolver resolver)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] PermissionDefinition {this.name}  DeregisterResolver");
#endif
            if (resolverIndexLut == null)
                CreateResolverIndexLut();
            if (!resolverIndexLut.Remove(resolver, out DataToken indexToken))
                return;
            int index = indexToken.Int;
            resolversCount--;
            if (index == resolversCount)
                return;
            PermissionResolver topResolver = resolvers[resolversCount];
            resolvers[index] = topResolver;
            resolverIndexLut[topResolver] = index;
        }
    }
}
