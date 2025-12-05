using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ShowObjectByPermission : PermissionResolver
    {
        [HideInInspector][SingletonReference] public PermissionManagerAPI permissionManager;

        public WhenConditionsAreMetType whenConditionsAreMet = WhenConditionsAreMetType.Show;
        [Tooltip("Should this object be shown while lockstep and with it the permission system is not yet initialized?\n"
            + "In other words, while the system is not yet aware whether the local player has permissions to see the object.")]
        public bool showWhileLoading = false;

        public bool[] logicalAnds;
        [SerializeField] private string[] assetGuids;
#if UNITY_EDITOR && !COMPILER_UDONSHARP
        public string[] AssetGuids => assetGuids;
#endif
        public PermissionDefinition[] permissionDefs;
        private bool isInitialized;

        public void Start()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] ShowObjectByPermission  Start");
#endif
            InitializeInstantiated();
        }

        public override void InitializeInstantiated()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] ShowObjectByPermission  InitializeInstantiated");
#endif
            if (isInitialized)
                return;
            isInitialized = true;
            if (permissionManager == null)
                permissionManager = SingletonsUtil.GetSingleton<PermissionManagerAPI>(nameof(PermissionManagerAPI));
            else if (permissionManager.ExistedAtSceneLoad(this)) // permissionManager is never null if this script existed at scene load.
            {
                if (!permissionManager.lockstep.IsInitialized)
                    gameObject.SetActive(showWhileLoading);
                return;
            }

            foreach (PermissionDefinition permissionDef in permissionDefs)
                permissionDef.RegisterResolver(this);

            if (permissionManager.lockstep.IsInitialized)
                Resolve();
            else
                gameObject.SetActive(showWhileLoading);
        }

        public void OnDestroy()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] ShowObjectByPermission  OnDestroy");
#endif
            foreach (PermissionDefinition permissionDef in permissionDefs)
            {
#if UNITY_EDITOR
                if (permissionDef == null)
                    return; // Prevent errors when exiting play mode.
#endif
                permissionDef.DeregisterResolver(this);
            }
        }

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
