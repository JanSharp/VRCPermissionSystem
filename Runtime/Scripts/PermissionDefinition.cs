using UdonSharp;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PermissionDefinition : UdonSharpBehaviour
    {
        [System.NonSerialized] public int index;
        public string internalName;
        public string displayName;
        public bool defaultValue;
    }
}
