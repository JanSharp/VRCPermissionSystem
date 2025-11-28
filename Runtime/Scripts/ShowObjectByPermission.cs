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

        public string[] assetGuids;
        public string[] internalNames;
        public bool[] logicalAnds;
    }
}
