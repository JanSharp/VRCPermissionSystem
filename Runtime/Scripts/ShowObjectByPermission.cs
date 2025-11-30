using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ShowObjectByPermission : PermissionResolver
    {
        public WhenConditionsAreMetType whenConditionsAreMet;

        public bool[] logicalAnds;
        [SerializeField] private string[] assetGuids;
#if UNITY_EDITOR && !COMPILER_UDONSHARP
        public string[] AssetGuids => assetGuids;
#endif
        public PermissionDefinition[] permissionDefs;

        public override void Resolve()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] ShowObjectByPermission  Resolve");
#endif
            bool conditionsMatching = PermissionsUtil.ResolveConditionsList(logicalAnds, permissionDefs);
            gameObject.SetActive((whenConditionsAreMet == WhenConditionsAreMetType.Show) == conditionsMatching);
        }
    }
}
