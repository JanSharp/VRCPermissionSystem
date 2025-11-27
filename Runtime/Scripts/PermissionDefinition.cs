using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PermissionDefinition : UdonSharpBehaviour
    {
        [SerializeField] private string definitionAssetGuid;
        public string DefinitionAssetGuid => definitionAssetGuid;
        [System.NonSerialized] public int index;
        [HideInInspector] public string internalName;
        [HideInInspector] public string displayName;
        [HideInInspector] public bool defaultValue;
    }
}
