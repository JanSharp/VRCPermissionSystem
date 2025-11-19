using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [LockstepGameStateDependency(typeof(PlayerDataManager))]
    [SingletonScript("fe2230975d59bf380865519bc62b6a3a")] // Runtime/Prefabs/PermissionManager.prefab
    public class PermissionManager : LockstepGameState
    {
        public override string GameStateInternalName => "jansharp.permission-system";
        public override string GameStateDisplayName => "Permission System";
        public override bool GameStateSupportsImportExport => true;
        public override uint GameStateDataVersion => 0u;
        public override uint GameStateLowestSupportedDataVersion => 0u;
        public override LockstepGameStateOptionsUI ExportUI => null;
        public override LockstepGameStateOptionsUI ImportUI => null;

        [HideInInspector][SerializeField][SingletonReference] private WannaBeClassesManager wannaBeClasses;
        [HideInInspector][SerializeField][SingletonReference] private PlayerDataManager playerDataManager;

        #region GameState
        private uint nextGroupId = 1u;
        [System.NonSerialized] public PermissionGroup defaultPermissionGroup;
        private PermissionGroup[] permissionGroups = new PermissionGroup[ArrList.MinCapacity];
        private DataDictionary groupsById = new DataDictionary();
        private int permissionGroupsCount = 0;
        [BuildTimeIdAssignment(nameof(permissionDefIds), nameof(highestPermissionDefId))]
        [HideInInspector][SerializeField] private PermissionDefinition[] permissionDefs;
        [HideInInspector][SerializeField] private uint[] permissionDefIds;
        [HideInInspector][SerializeField] private uint highestPermissionDefId;
        public int PermissionDefinitionsCount => permissionDefs.Length;
        #endregion

        private void Start()
        {
            int length = permissionDefs.Length;
            for (int i = 0; i < length; i++)
                permissionDefs[i].id = permissionDefIds[i];
        }

        [LockstepEvent(LockstepEventType.OnInit)]
        public void OnInit()
        {
            defaultPermissionGroup = wannaBeClasses.New<PermissionGroup>(nameof(PermissionGroup));
            ArrList.Add(ref permissionGroups, ref permissionGroupsCount, defaultPermissionGroup);
            defaultPermissionGroup.isDefault = true;
            defaultPermissionGroup.groupName = PermissionGroup.DefaultGroupName;
            defaultPermissionGroup.id = nextGroupId++;
            groupsById.Add(defaultPermissionGroup.id, defaultPermissionGroup);
            bool[] permissionValues = new bool[permissionDefs.Length];
            defaultPermissionGroup.permissionValues = permissionValues;
            int length = permissionDefs.Length;
            for (int i = 0; i < length; i++)
                permissionValues[i] = permissionDefs[i].defaultValue;
        }

        public override void SerializeGameState(bool isExport, LockstepGameStateOptionsData exportOptions)
        {
            if (isExport)
            {
                // TODO: Scream!
                return;
            }
            lockstep.WriteSmallUInt(nextGroupId);
            lockstep.WriteSmallUInt((uint)permissionGroupsCount);
            for (int i = 0; i < permissionGroupsCount; i++)
                lockstep.WriteCustomClass(permissionGroups[i]);
        }

        public override string DeserializeGameState(bool isImport, uint importedDataVersion, LockstepGameStateOptionsData importOptions)
        {
            if (isImport)
            {
                // TODO: Scream!
                return null;
            }
            nextGroupId = lockstep.ReadSmallUInt();
            permissionGroupsCount = (int)lockstep.ReadSmallUInt();
            ArrList.EnsureCapacity(ref permissionGroups, permissionGroupsCount);
            for (int i = 0; i < permissionGroupsCount; i++)
                permissionGroups[i] = (PermissionGroup)lockstep.ReadCustomClass(nameof(PermissionGroup));
            defaultPermissionGroup = permissionGroups[0];

            foreach (PermissionsPlayerData playerData in playerDataManager.GetAllPlayerData<PermissionsPlayerData>(nameof(PermissionsPlayerData)))
                playerData.permissionGroup = (PermissionGroup)groupsById[playerData.deserializedId].Reference;
            return null;
        }
    }
}
