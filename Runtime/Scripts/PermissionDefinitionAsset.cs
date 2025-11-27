using UnityEngine;

namespace JanSharp
{
    [CreateAssetMenu(fileName = "Permission", menuName = "Permission Definition", order = 1000)]
    public class PermissionDefinitionAsset : ScriptableObject
    {
        public string internalName;
        public string displayName;
        public string order;
        public bool defaultDefaultValue;
    }
}
