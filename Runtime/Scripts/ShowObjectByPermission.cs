using UdonSharp;

namespace JanSharp
{
    public enum WhenConditionsAreMetType
    {
        Show,
        Hide,
    }

    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ShowObjectByPermission : UdonSharpBehaviour
    {
        public WhenConditionsAreMetType whenConditionsAreMet;

        public bool[] logicalAnds;
        public string[] assetGuids;
        public PermissionDefinition[] permissionDefs;
    }
}
