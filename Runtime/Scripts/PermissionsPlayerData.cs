using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PermissionsPlayerData : PlayerData
    {
        public override string PlayerDataInternalName => "jansharp.permissions";
        public override string PlayerDataDisplayName => "Permissions";
        public override bool SupportsImportExport => true;
        public override uint DataVersion => 0u;
        public override uint LowestSupportedDataVersion => 0u;

        [HideInInspector][SerializeField][SingletonReference] private PermissionManagerAPI permissionManager;

        #region GameState
        [System.NonSerialized] public PermissionGroup permissionGroup;
        /// <summary>
        /// <para>When this is non <c>-1</c> the permissions system is holding a strong reference to this
        /// wanna be class instance.</para>
        /// </summary>
        [System.NonSerialized] public int indexInPlayersInGroup = -1;
        [System.NonSerialized] public int indexInOnlinePlayersInGroup = -1;
        #endregion

        [System.NonSerialized] public uint deserializedId;

        public bool HasPermission(PermissionDefinition permissionDef)
        {
            return permissionGroup.permissionValues[permissionDef.index];
        }

        public override void OnPlayerDataInit(bool isAboutToBeImported)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] PermissionsPlayerData  OnPlayerDataInit");
#endif
            if (isAboutToBeImported)
                return;
            ((Internal.PermissionManager)permissionManager).PlayerDataPermissionGroupSetter(this, permissionManager.DefaultPermissionGroup);
        }

        public override void OnPlayerDataUninit(bool force)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] PermissionsPlayerData  OnPlayerDataInit");
#endif
            ((Internal.PermissionManager)permissionManager).PlayerDataPermissionGroupSetter(this, null);
        }

        public override bool PersistPlayerDataWhileOffline()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] PermissionsPlayerData  PersistPlayerDataWhileOffline");
#endif
            return !permissionGroup.isDefault;
        }

        public override void Serialize(bool isExport)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] PermissionsPlayerData  Serialize");
#endif
            lockstep.WriteSmallUInt(permissionGroup.id);
        }

        public override void Deserialize(bool isImport, uint importedDataVersion)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] PermissionsPlayerData  Deserialize");
#endif
            // Resolved in the PermissionManager later.
            deserializedId = lockstep.ReadSmallUInt();
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] PermissionsPlayerData  Deserialize (inner) - core.displayName: {core.displayName}, deserializedId: {deserializedId}");
#endif
        }
    }
}
