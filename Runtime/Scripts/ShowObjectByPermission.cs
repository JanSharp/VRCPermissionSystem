using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    public enum WhenConditionsAreMetType
    {
        Show,
        Hide,
    }

    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ShowObjectByPermission : PermissionResolver
    {
        public WhenConditionsAreMetType whenConditionsAreMet;

        public bool[] logicalAnds;
        public string[] assetGuids;
        public PermissionDefinition[] permissionDefs;

        public override void Resolve()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] ShowObjectByPermission  Resolve");
#endif
            int length = permissionDefs.Length;
            bool conditionsMatching = true;
            for (int i = 0; i < length; i++)
            {
                bool logicalAnd = logicalAnds[i];
                if (!conditionsMatching && logicalAnd)
                    continue;
                if (!logicalAnd && conditionsMatching && i != 0)
                    break;
                conditionsMatching = permissionDefs[i].valueForLocalPlayer;
            }
            gameObject.SetActive((whenConditionsAreMet == WhenConditionsAreMetType.Show) == conditionsMatching);
        }
    }
}
