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
        [System.NonSerialized] public int indexInPlayersInGroup = -1;
        [System.NonSerialized] public int indexInOnlinePlayersInGroup = -1;
        #endregion

        public bool HasPermission(PermissionDefinition permissionDef)
        {
            return permissionGroup.permissionValues[permissionDef.index];
        }

        public override void OnPlayerDataInit(bool isAboutToBeImported)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] PermissionsPlayerData  OnPlayerDataInit");
#endif
            if (isAboutToBeImported
                && permissionManager.OptionsFromExport.includePlayerPermissionGroups
                && permissionManager.ImportOptions.includePlayerPermissionGroups)
            {
                return;
            }
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

        private void WritePermissionGroup()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] PermissionsPlayerData  WritePermissionGroup");
#endif
            permissionManager.WritePermissionGroupRef(permissionGroup);
        }

        private void ReadPermissionGroup(bool isImport, bool discard)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] PermissionsPlayerData  WritePermissionGroup - discard: {discard}");
#endif
            if (discard)
            {
                permissionManager.ReadPermissionGroupRef(isImport);
                return;
            }
            PermissionGroup group = permissionManager.ReadPermissionGroupRef(isImport); // Impossible to be null.
            ((Internal.PermissionManager)permissionManager).PlayerDataPermissionGroupSetter(this, group);
        }

        public override void Serialize(bool isExport)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] PermissionsPlayerData  Serialize");
#endif
            if (!isExport || permissionManager.ExportOptions.includePlayerPermissionGroups)
                WritePermissionGroup();
        }

        public override void Deserialize(bool isImport, uint importedDataVersion)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] PermissionsPlayerData  Deserialize");
#endif
            if (!isImport)
                ReadPermissionGroup(isImport, discard: false);
            else if (permissionManager.OptionsFromExport.includePlayerPermissionGroups)
                ReadPermissionGroup(isImport, discard: !permissionManager.ImportOptions.includePlayerPermissionGroups);
        }

        public override void OnNotPartOfImportedData()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] PermissionsPlayerData  OnNotPartOfImportedData");
#endif
            if (permissionGroup != null && !permissionGroup.isDeleted)
                return;
            // If a player was not part of the imported data, the group the player was apart of could have
            // been deleted through the import. Reset to the default group if that is the case.
            ((Internal.PermissionManager)permissionManager).PlayerDataPermissionGroupSetter(this, permissionManager.DefaultPermissionGroup);
        }
    }
}
