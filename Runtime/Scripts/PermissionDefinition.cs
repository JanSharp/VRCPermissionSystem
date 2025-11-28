using UdonSharp;
using UnityEngine;

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
        [HideInInspector] public string internalName;
        [HideInInspector] public string displayName;
        [HideInInspector] public bool defaultValue;
    }
}
