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
        public bool[] inverts;
        [SerializeField] private string[] assetGuids;
#if UNITY_EDITOR && !COMPILER_UDONSHARP
        public string[] AssetGuids => assetGuids;
#endif
        public PermissionDefinition[] permissionDefs;
        private bool isInitialized;

        public void Start()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] ShowObjectByPermission {this.name}  Start");
#endif
            InitializeInstantiated();
        }

        public override void InitializeInstantiated()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] ShowObjectByPermission {this.name}  InitializeInstantiated");
#endif
            if (isInitialized)
                return;
            isInitialized = true;
            if (permissionManager == null)
                permissionManager = SingletonsUtil.GetSingleton<PermissionManagerAPI>(nameof(PermissionManagerAPI));
            else if (permissionManager.ExistedAtSceneLoad(this)) // permissionManager is never null if this script existed at scene load.
            {
#if PERMISSION_SYSTEM_DEBUG
                Debug.Log($"[PermissionSystemDebug] ShowObjectByPermission {this.name}  InitializeInstantiated (inner) - ExistedAtSceneLoad: true, permissionManager.IsInitialized: {permissionManager.IsInitialized}");
#endif
                if (!permissionManager.IsInitialized)
                    gameObject.SetActive(showWhileLoading);
                return;
            }
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] ShowObjectByPermission {this.name}  InitializeInstantiated (inner) - ExistedAtSceneLoad: false, permissionManager.IsInitialized: {permissionManager.IsInitialized}");
#endif

            foreach (PermissionDefinition permissionDef in permissionDefs)
                permissionDef.RegisterResolver(this);

            if (permissionManager.IsInitialized)
                Resolve();
            else
                gameObject.SetActive(showWhileLoading);
        }

        public void OnDestroy()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] ShowObjectByPermission {this.name}  OnDestroy");
#endif
            // Null checking just to prevent errors when exiting play mode or leaving the world in VRChat.
            // Technically also to prevent errors when something put null into the array at runtime.
            // And technically these errors aren't a problem, but they are annoying.
            foreach (PermissionDefinition permissionDef in permissionDefs)
                if (permissionDef != null)
                    permissionDef.DeregisterResolver(this);
        }

        public override void Resolve()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] ShowObjectByPermission {this.name}  Resolve");
#endif
            bool conditionsMatching = PermissionsUtil.ResolveConditionsList(logicalAnds, inverts, permissionDefs);
            gameObject.SetActive((whenConditionsAreMet == WhenConditionsAreMetType.Show) == conditionsMatching);
        }
    }
}
