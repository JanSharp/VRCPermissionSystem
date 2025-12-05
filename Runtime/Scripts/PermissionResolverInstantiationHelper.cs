using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PermissionResolverInstantiationHelper : UdonSharpBehaviour
    {
        [Tooltip("When enabled, the list of Resolvers gets populated upon entering play mode or when building "
            + "the world. Note that if this script is inside of a prefab it will not update the prefab "
            + "automatically, only objects and prefab instances in the scene are affected.")]
        [SerializeField] private bool autoPopulateFromChildren;
#if UNITY_EDITOR && !COMPILER_UDONSHARP
        public bool AutoPopulateFromChildren => autoPopulateFromChildren;
#endif
        [Tooltip("A list of resolvers to ensure are initialized upon the creation of this object.")]
        public PermissionResolver[] resolvers;

        public void Start() => InitializeInstantiated();

        public void InitializeInstantiated()
        {
            foreach (var resolver in resolvers)
                if (resolver != null)
                    resolver.InitializeInstantiated();
        }
    }
}
