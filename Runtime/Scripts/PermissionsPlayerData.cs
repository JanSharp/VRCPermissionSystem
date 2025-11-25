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
        #endregion

        [System.NonSerialized] public uint deserializedId;

        public override void OnPlayerDataInit(bool isAboutToBeImported)
        {
            if (isAboutToBeImported)
                return;
            permissionGroup = permissionManager.DefaultPermissionGroup;
        }

        public override bool PersistPlayerDataWhileOffline()
        {
            return !permissionGroup.isDefault;
        }

        public override void Serialize(bool isExport)
        {
            lockstep.WriteSmallUInt(permissionGroup.id);
        }

        public override void Deserialize(bool isImport, uint importedDataVersion)
        {
            // Resolved in the PermissionManager later.
            deserializedId = lockstep.ReadSmallUInt();
        }
    }
}
