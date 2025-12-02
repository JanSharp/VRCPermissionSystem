using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;

namespace JanSharp.Internal
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [LockstepGameStateDependency(typeof(PlayerDataManagerAPI))]
    [CustomRaisedEventsDispatcher(typeof(PermissionsEventAttribute), typeof(PermissionsEventType))]
    public class PermissionManager : PermissionManagerAPI
    {
        public override string GameStateInternalName => "jansharp.permission-system";
        public override string GameStateDisplayName => "Permission System";
        public override bool GameStateSupportsImportExport => true;
        public override uint GameStateDataVersion => 0u;
        public override uint GameStateLowestSupportedDataVersion => 0u;
        public override LockstepGameStateOptionsUI ExportUI => null;
        public override LockstepGameStateOptionsUI ImportUI => null;

        [HideInInspector][SerializeField][SingletonReference] private WannaBeClassesManager wannaBeClasses;
        [HideInInspector][SerializeField][SingletonReference] private PlayerDataManagerAPI playerDataManager;

        #region GameState
        private uint nextGroupId = 1u;
        private PermissionGroup defaultPermissionGroup;
        public override PermissionGroup DefaultPermissionGroup => defaultPermissionGroup;
        private PermissionGroup[] permissionGroups = new PermissionGroup[ArrList.MinCapacity];
        private DataDictionary groupsById = new DataDictionary();
        private DataDictionary groupsByName = new DataDictionary();
        private int permissionGroupsCount = 0;

        [HideInInspector] public PermissionDefinition[] permissionDefs;
        public override PermissionDefinition[] PermissionDefinitions => permissionDefs;
        private int permissionDefsCount;
        /// <summary><see cref="string"/> internalName => <see cref="PermissionDefinition"/> def</summary>
        private DataDictionary permissionDefsByInternalName = new DataDictionary();

        private int playerDataClassNameIndex;
        #endregion

        private VRCPlayerApi localPlayer;
        private uint localPlayerId;
        private PermissionsPlayerData localPlayerData;
        private PermissionGroup viewWorldAsGroup;
        /// <summary><see cref="PermissionResolver"/> resolver => <see langword="true"/></summary>
        private DataDictionary visitedResolvers = new DataDictionary();

        public override PermissionGroup[] PermissionGroups
        {
            get
            {
                PermissionGroup[] result = new PermissionGroup[permissionGroupsCount];
                System.Array.Copy(permissionGroups, result, permissionGroupsCount);
                return result;
            }
        }
        public override PermissionGroup GetPermissionGroup(int index) => permissionGroups[index];
        public override int PermissionGroupsCount => permissionGroupsCount;

        private DataDictionary groupsByImportedId;

        private PermissionsPlayerData GetPlayerDataForPlayerId(uint playerId)
        {
            return (PermissionsPlayerData)playerDataManager
                .GetCorePlayerDataForPlayerId(playerId)
                .customPlayerData[playerDataClassNameIndex];
        }

        private void Start()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  Start");
#endif
            localPlayer = Networking.LocalPlayer;
            localPlayerId = (uint)localPlayer.playerId;
            playerDataManager.RegisterCustomPlayerData<PermissionsPlayerData>(nameof(PermissionsPlayerData));
            permissionDefsCount = permissionDefs.Length;
            for (int i = 0; i < permissionDefsCount; i++)
            {
                PermissionDefinition permissionDef = permissionDefs[i];
                permissionDef.index = i;
                permissionDefsByInternalName.Add(permissionDef.internalName, permissionDef);
            }
        }

        // Must initialize before PlayerDataManager in order for the PermissionsPlayerData to init properly.
        [LockstepEvent(LockstepEventType.OnInit, Order = -9000)]
        public void OnInit()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  OnInit");
#endif
            playerDataClassNameIndex = playerDataManager.GetPlayerDataClassNameIndex<PermissionsPlayerData>(nameof(PermissionsPlayerData));

            defaultPermissionGroup = wannaBeClasses.New<PermissionGroup>(nameof(PermissionGroup));
            defaultPermissionGroup.isDefault = true;
            defaultPermissionGroup.groupName = PermissionGroup.DefaultGroupName;
            defaultPermissionGroup.id = nextGroupId++;
            bool[] permissionValues = new bool[permissionDefsCount];
            defaultPermissionGroup.permissionValues = permissionValues;
            for (int i = 0; i < permissionDefsCount; i++)
                permissionValues[i] = permissionDefs[i].defaultValue;
            RegisterCreatedPermissionGroup(defaultPermissionGroup);

            localPlayerData = GetPlayerDataForPlayerId(localPlayerId);
            localPlayerData.permissionGroup = defaultPermissionGroup;
            SetGroupToViewWorldAs(defaultPermissionGroup);
        }

        private void SetGroupToViewWorldAs(PermissionGroup group)
        {
            if (viewWorldAsGroup == group)
                return;
            viewWorldAsGroup = group;
            bool[] permissionValues = group.permissionValues;
            for (int i = 0; i < permissionDefsCount; i++)
                permissionDefs[i].valueForLocalPlayer = permissionValues[i];
            // TODO: Just have a list of all resolvers on the manager itself, which removes the need of a dictionary here.
            for (int i = 0; i < permissionDefsCount; i++)
                foreach (PermissionResolver resolver in permissionDefs[i].resolvers)
                {
                    if (visitedResolvers.TryGetValue(resolver, out DataToken discard)) // Cannot use _.
                        continue;
                    visitedResolvers.Add(resolver, true);
                    resolver.Resolve();
                }
            visitedResolvers.Clear();
        }

        public override PermissionGroup GetPermissionGroup(string groupName)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  GetPermissionGroup");
#endif
            return groupsByName.TryGetValue(groupName, out DataToken groupToken)
                ? (PermissionGroup)groupToken.Reference
                : null;
        }

        public override void SendDuplicatePermissionGroupIA(string groupName, PermissionGroup toDuplicate)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  SendDuplicatePermissionGroupIA");
#endif
            groupName = groupName.Trim();
            if (groupName == "")
                return;
            lockstep.WriteString(groupName);
            lockstep.WriteSmallUInt(toDuplicate.id);
            lockstep.SendInputAction(duplicatePermissionGroupIAId);
        }

        [HideInInspector][SerializeField] private uint duplicatePermissionGroupIAId;
        [LockstepInputAction(nameof(duplicatePermissionGroupIAId))]
        public void OnDuplicatePermissionGroupIA()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  OnDuplicatePermissionGroupIA");
#endif
            string groupName = lockstep.ReadString();
            uint groupId = lockstep.ReadSmallUInt();
            if (!groupsById.TryGetValue(groupId, out DataToken groupToken))
                return;
            DuplicatePermissionGroupInGS(groupName, (PermissionGroup)groupToken.Reference);
        }

        public override PermissionGroup DuplicatePermissionGroupInGS(string groupName, PermissionGroup toDuplicate)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  DuplicatePermissionGroupInGS");
#endif
            groupName = groupName.Trim();
            if (groupName == "" || groupsByName.ContainsKey(groupName))
                return null;
            PermissionGroup group = wannaBeClasses.New<PermissionGroup>(nameof(PermissionGroup));
            group.groupName = groupName;
            group.id = nextGroupId++;
            bool[] permissionValues = new bool[permissionDefsCount];
            group.permissionValues = permissionValues;
            toDuplicate.permissionValues.CopyTo(permissionValues, index: 0);
            RegisterCreatedPermissionGroup(group);
            if (!lockstep.IsDeserializingForImport)
                RaiseOnPermissionGroupDuplicated(group, toDuplicate);
            return group;
        }

        private void RegisterCreatedPermissionGroup(PermissionGroup group)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  RegisterCreatedPermissionGroup");
#endif
            ArrList.Add(ref permissionGroups, ref permissionGroupsCount, group);
            groupsById.Add(group.id, group);
            groupsByName.Add(group.groupName, group);
        }

        public override void SendDeletePermissionGroupIA(PermissionGroup group, PermissionGroup groupToMovePlayersTo)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  SendDeletePermissionGroupIA");
#endif
            if (group.isDefault || group.isDeleted || group == groupToMovePlayersTo || groupToMovePlayersTo.isDeleted)
                return;
            lockstep.WriteSmallUInt(group.id);
            lockstep.WriteSmallUInt(groupToMovePlayersTo.id);
            lockstep.SendInputAction(deletePermissionGroupIAId);
        }

        [HideInInspector][SerializeField] private uint deletePermissionGroupIAId;
        [LockstepInputAction(nameof(deletePermissionGroupIAId))]
        public void OnDeletePermissionGroupIA()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  OnDeletePermissionGroupIA");
#endif
            uint groupId = lockstep.ReadSmallUInt();
            uint groupToMovePlayersToId = lockstep.ReadSmallUInt();
            if (!groupsById.TryGetValue(groupId, out DataToken groupToken))
                return;
            if (!groupsById.TryGetValue(groupToMovePlayersToId, out DataToken groupToMovePlayersToToken))
                return;
            DeletePermissionGroupInGS(
                (PermissionGroup)groupToken.Reference,
                (PermissionGroup)groupToMovePlayersToToken.Reference);
        }

        public override void DeletePermissionGroupInGS(PermissionGroup group, PermissionGroup groupToMovePlayersTo)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  DeletePermissionGroupInGS");
#endif
            if (group.isDefault || group.isDeleted || group == groupToMovePlayersTo || groupToMovePlayersTo.isDeleted)
                return;
            group.isDeleted = true;
            PermissionsPlayerData[] allPlayerData = playerDataManager.GetAllPlayerData<PermissionsPlayerData>(nameof(PermissionsPlayerData));
            foreach (PermissionsPlayerData playerData in allPlayerData)
                if (playerData.permissionGroup == group)
                    SetPlayerDataPermissionGroup(playerData, groupToMovePlayersTo, group);
            ArrList.Remove(ref permissionGroups, ref permissionGroupsCount, group);
            groupsById.Remove(group.id);
            groupsByName.Remove(group.groupName);
            RaiseOnPermissionGroupDeleted(group);
            group.DecrementRefsCount();
        }

        private void DeletePermissionGroupWithoutCleanup(PermissionGroup group)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  DeletePermissionGroupWithoutCleanup");
#endif
            group.isDeleted = true;
            ArrList.Remove(ref permissionGroups, ref permissionGroupsCount, group);
            groupsById.Remove(group.id);
            groupsByName.Remove(group.groupName);
            group.DecrementRefsCount();
        }

        public override void SendRenamePermissionGroupIA(PermissionGroup group, string newGroupName)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  SendRenamePermissionGroupIA");
#endif
            if (group.isDefault)
                return;
            newGroupName = newGroupName.Trim();
            // Intentionally not checking if the group already has the same name, because by the time the IA
            // runs the group may have a different name, making it valid to change it back to the current name.
            if (newGroupName == "")
                return;
            lockstep.WriteSmallUInt(group.id);
            lockstep.WriteString(newGroupName);
            lockstep.SendInputAction(renamePermissionGroupIAId);
        }

        [HideInInspector][SerializeField] private uint renamePermissionGroupIAId;
        [LockstepInputAction(nameof(renamePermissionGroupIAId))]
        public void OnRenamePermissionGroupIA()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  OnRenamePermissionGroupIA");
#endif
            uint groupId = lockstep.ReadSmallUInt();
            if (!groupsById.TryGetValue(groupId, out DataToken groupToken))
                return;
            string newGroupName = lockstep.ReadString();
            RenamePermissionGroupInGS((PermissionGroup)groupToken.Reference, newGroupName);
        }

        public override void RenamePermissionGroupInGS(PermissionGroup group, string newGroupName)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  RenamePermissionGroupInGS");
#endif
            if (group.isDefault)
                return;
            newGroupName = newGroupName.Trim();
            if (newGroupName == "" || group.groupName == newGroupName || groupsByName.ContainsKey(newGroupName))
                return;
            string prevGroupName = group.groupName;
            group.groupName = newGroupName;
            groupsByName.Remove(prevGroupName);
            groupsByName.Add(newGroupName, group);
            RaiseOnPermissionGroupRenamed(group, prevGroupName);
        }

        public override void SendSetPlayerPermissionGroupIA(CorePlayerData corePlayerData, PermissionGroup group)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  SendSetPlayerPermissionGroupIA");
#endif
            lockstep.WriteSmallUInt(corePlayerData.persistentId); // playerId would not work for offline players.
            lockstep.WriteSmallUInt(group.id);
            lockstep.SendInputAction(setPlayerPermissionGroupIAId);
        }

        [HideInInspector][SerializeField] private uint setPlayerPermissionGroupIAId;
        [LockstepInputAction(nameof(setPlayerPermissionGroupIAId))]
        public void OnSetPlayerPermissionGroupIA()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  OnSetPlayerPermissionGroupIA");
#endif
            uint persistentId = lockstep.ReadSmallUInt();
            if (!playerDataManager.TryGetCorePlayerDataForPersistentId(persistentId, out CorePlayerData corePlayerData))
                return;
            uint groupId = lockstep.ReadSmallUInt();
            if (!groupsById.TryGetValue(groupId, out DataToken groupToken))
                return;
            SetPlayerPermissionGroupInGS(corePlayerData, (PermissionGroup)groupToken.Reference);
        }

        public override void SetPlayerPermissionGroupInGS(CorePlayerData corePlayerData, PermissionGroup group)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  SetPlayerPermissionGroupInGS");
#endif
            if (group.isDeleted)
                return;
            PermissionsPlayerData playerData = (PermissionsPlayerData)corePlayerData.customPlayerData[playerDataClassNameIndex];
            PermissionGroup prevGroup = playerData.permissionGroup;
            if (prevGroup != group)
                SetPlayerDataPermissionGroup(playerData, group, prevGroup);
        }

        private void SetPlayerDataPermissionGroup(PermissionsPlayerData playerData, PermissionGroup group, PermissionGroup prevGroup)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  SetPlayerDataPermissionGroup");
#endif
            playerData.permissionGroup = group;
            if (playerData.core.playerId == localPlayerId)
                SetGroupToViewWorldAs(group);
            RaiseOnPlayerPermissionGroupChanged(playerData, prevGroup);
        }

        public override void SendSetPermissionValueIA(PermissionGroup group, string permissionInternalName, bool value)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  SendSetPermissionValueIA");
#endif
            if (!permissionDefsByInternalName.TryGetValue(permissionInternalName, out DataToken defToken))
                return;
            lockstep.WriteSmallUInt(group.id);
            lockstep.WriteSmallUInt((uint)((PermissionDefinition)defToken.Reference).index);
            lockstep.WriteFlags(value);
            lockstep.SendInputAction(setPermissionValueIAId);
        }

        [HideInInspector][SerializeField] private uint setPermissionValueIAId;
        [LockstepInputAction(nameof(setPermissionValueIAId))]
        public void OnSetPermissionValueIA()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  OnSetPermissionValueIA");
#endif
            uint groupId = lockstep.ReadSmallUInt();
            if (!groupsById.TryGetValue(groupId, out DataToken groupToken))
                return;
            int defIndex = (int)lockstep.ReadSmallUInt();
            lockstep.ReadFlags(out bool value);
            SetPermissionValueInGS((PermissionGroup)groupToken.Reference, permissionDefs[defIndex], value);
        }

        public override void SetPermissionValueInGS(PermissionGroup group, string permissionInternalName, bool value)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  SetPermissionValueInGS");
#endif
            if (!permissionDefsByInternalName.TryGetValue(permissionInternalName, out DataToken defToken))
                return;
            SetPermissionValueInGS(group, (PermissionDefinition)defToken.Reference, value);
        }

        private void SetPermissionValueInGS(PermissionGroup group, PermissionDefinition permissionDef, bool value)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  SetPermissionValueInGS");
#endif
            int index = permissionDef.index;
            bool[] permissionValues = group.permissionValues;
            bool prevValue = permissionValues[index];
            if (prevValue == value)
                return;
            permissionValues[index] = value;
            if (group == viewWorldAsGroup)
            {
                permissionDef.valueForLocalPlayer = value;
                foreach (PermissionResolver resolver in permissionDef.resolvers)
                    resolver.Resolve();
            }
            RaiseOnPermissionValueChanged(group, permissionDefs[index]);
        }

        #region Serialization

        private void WritePermissionGroup(PermissionGroup group)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  WritePermissionGroup");
#endif
            lockstep.WriteSmallUInt(group.id);
            lockstep.WriteString(group.groupName);
            lockstep.WriteFlags(group.permissionValues);
        }

        private PermissionGroup ReadPermissionGroup()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  ReadPermissionGroup");
#endif
            PermissionGroup group = wannaBeClasses.New<PermissionGroup>(nameof(PermissionGroup));
            group.id = lockstep.ReadSmallUInt();
            string groupName = lockstep.ReadString();
            group.groupName = groupName;
            group.isDefault = groupName == PermissionGroup.DefaultGroupName;
            group.permissionValues = new bool[permissionDefsCount];
            lockstep.ReadFlags(group.permissionValues);
            return group;
        }

        private void WritePermissionGroups()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  WritePermissionGroups");
#endif
            lockstep.WriteSmallUInt((uint)permissionGroupsCount);
            for (int i = 0; i < permissionGroupsCount; i++)
                WritePermissionGroup(permissionGroups[i]);
        }

        private void ReadPermissionGroups()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  ReadPermissionGroups");
#endif
            int count = (int)lockstep.ReadSmallUInt();
            ArrList.EnsureCapacity(ref permissionGroups, count);
            for (int i = 0; i < count; i++)
                RegisterCreatedPermissionGroup(ReadPermissionGroup());
            defaultPermissionGroup = permissionGroups[0];
        }

        private void ExportPermissionDefinitionsMetadata()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  ExportPermissionDefinitionsMetadata");
#endif
            foreach (PermissionDefinition def in permissionDefs)
                lockstep.WriteString(def.internalName);
        }

        /// <returns>A map from live permission definitions to their imported index equivalent.</returns>
        private int[] ImportPermissionDefinitionsMetadata(int importedDefsCount)
        {
            int[] correspondingImportedDefIndexMap = new int[permissionDefsCount];
            for (int i = 0; i < permissionDefsCount; i++)
                correspondingImportedDefIndexMap[i] = -1;
            for (int i = 0; i < importedDefsCount; i++)
                if (permissionDefsByInternalName.TryGetValue(lockstep.ReadString(), out DataToken defToken))
                    correspondingImportedDefIndexMap[((PermissionDefinition)defToken.Reference).index] = i;
            return correspondingImportedDefIndexMap;
        }

        private void ExportPermissionGroupNamesAndIds()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  ExportPermissionGroupNamesAndIds");
#endif
            for (int i = 0; i < permissionGroupsCount; i++)
            {
                PermissionGroup group = permissionGroups[i];
                lockstep.WriteString(group.groupName);
                lockstep.WriteSmallUInt(group.id);
            }
        }

        private void ImportPermissionGroupNamesAndIds(int count)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  ImportPermissionGroupNamesAndIds");
#endif
            int capacity = ArrList.MinCapacity;
            while (capacity < count)
                capacity *= 2;
            PermissionGroup[] importedGroups = new PermissionGroup[capacity];
            groupsByImportedId = new DataDictionary();
            int originalPermissionGroupsCount = permissionGroupsCount; // To skip redundantly processing the new ones later.
            DataDictionary groupsToKeepLut = new DataDictionary();

            for (int i = 0; i < count; i++)
            {
                string groupName = lockstep.ReadString();
                uint importedId = lockstep.ReadSmallUInt();
                PermissionGroup group = groupsByName.TryGetValue(groupName, out DataToken groupToken)
                    ? (PermissionGroup)groupToken.Reference
                    : DuplicatePermissionGroupInGS(groupName, defaultPermissionGroup);
                importedGroups[i] = group;
                groupsByImportedId.Add(importedId, group);
                groupsToKeepLut.Add(group, true);
#if PERMISSION_SYSTEM_DEBUG
                Debug.Log($"[PermissionSystemDebug] Manager  ImportPermissionGroupNamesAndIds (inner) - importedId: {importedId}, group.id: {group.id}");
#endif
            }

            // For the sake of the api, delete existing groups even though they could've been reused by renaming them.
            for (int i = originalPermissionGroupsCount - 1; i >= 0; i--)
            {
                PermissionGroup group = permissionGroups[i];
                if (!groupsToKeepLut.ContainsKey(group))
                    DeletePermissionGroupWithoutCleanup(group);
            }

            permissionGroups = importedGroups; // To make the order match what was imported.
        }

        private void ExportPermissionGroupFlags()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  ExportPermissionGroupFlags");
#endif
            for (int i = 0; i < permissionGroupsCount; i++)
            {
                PermissionGroup group = permissionGroups[i];
                lockstep.WriteFlags(group.permissionValues);
            }
        }

        private void ImportPermissionGroupFlags(int importedDefsCount, int[] defsCorrespondingImportedIndex)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  ImportPermissionGroupFlags");
#endif
            bool[] importedFlags = new bool[importedDefsCount];
            for (int i = 0; i < permissionGroupsCount; i++)
            {
                PermissionGroup group = permissionGroups[i];
                lockstep.ReadFlags(importedFlags);
                bool[] permissionValues = group.permissionValues;
                for (int j = 0; j < permissionDefsCount; j++)
                {
                    int correspondingImportedIndex = defsCorrespondingImportedIndex[j];
                    if (correspondingImportedIndex != -1)
                        permissionValues[j] = importedFlags[correspondingImportedIndex];
                }
            }
        }

        private void ResolvePlayerPermissionData(DataDictionary groupsById)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  ResolvePlayerPermissionData");
#endif
            foreach (PermissionsPlayerData playerData in playerDataManager.GetAllPlayerData<PermissionsPlayerData>(nameof(PermissionsPlayerData)))
            {
#if PERMISSION_SYSTEM_DEBUG
                Debug.Log($"[PermissionSystemDebug] Manager  ResolvePlayerPermissionData (inner) - playerData.core.displayName: {playerData.core.displayName}, playerData.deserializedId: {playerData.deserializedId}");
#endif
                if (playerData.deserializedId == 0u) // Did not get imported.
                    continue;
                playerData.permissionGroup = (PermissionGroup)groupsById[playerData.deserializedId].Reference;
                playerData.deserializedId = 0u;
            }
        }

        private void Export()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  Export");
#endif
            lockstep.WriteSmallUInt((uint)permissionDefsCount);
            ExportPermissionDefinitionsMetadata();
            lockstep.WriteSmallUInt((uint)permissionGroupsCount);
            ExportPermissionGroupNamesAndIds();
            ExportPermissionGroupFlags();
        }

        private void Import()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  Import");
#endif
            int importedDefsCount = (int)lockstep.ReadSmallUInt();
            int[] correspondingImportedDefIndexMap = ImportPermissionDefinitionsMetadata(importedDefsCount);
            int importedGroupsCount = (int)lockstep.ReadSmallUInt();
            ImportPermissionGroupNamesAndIds(importedGroupsCount);
            ImportPermissionGroupFlags(importedDefsCount, correspondingImportedDefIndexMap);

            ResolvePlayerPermissionData(groupsByImportedId);
            viewWorldAsGroup = null; // Force refresh even if the group is the same.
            SetGroupToViewWorldAs(localPlayerData.permissionGroup);
        }

        [LockstepEvent(LockstepEventType.OnImportFinished)]
        public void OnImportFinished()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  OnImportFinished");
#endif
            groupsByImportedId = null;
        }

        public override void SerializeGameState(bool isExport, LockstepGameStateOptionsData exportOptions)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  SerializeGameState");
#endif
            if (isExport)
            {
                Export();
                return;
            }
            lockstep.WriteSmallUInt(nextGroupId);
            WritePermissionGroups();
        }

        public override string DeserializeGameState(bool isImport, uint importedDataVersion, LockstepGameStateOptionsData importOptions)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  DeserializeGameState");
#endif
            if (isImport)
            {
                Import();
                return null;
            }

            localPlayerData = GetPlayerDataForPlayerId(localPlayerId);

            nextGroupId = lockstep.ReadSmallUInt();
            ReadPermissionGroups();

            ResolvePlayerPermissionData(groupsById);
            SetGroupToViewWorldAs(localPlayerData.permissionGroup);
            return null;
        }

        #endregion

        #region EventDispatcher

        [HideInInspector][SerializeField] private UdonSharpBehaviour[] onPermissionGroupDuplicatedListeners;
        [HideInInspector][SerializeField] private UdonSharpBehaviour[] onPermissionGroupDeletedListeners;
        [HideInInspector][SerializeField] private UdonSharpBehaviour[] onPermissionGroupRenamedListeners;
        [HideInInspector][SerializeField] private UdonSharpBehaviour[] onPlayerPermissionGroupChangedListeners;
        [HideInInspector][SerializeField] private UdonSharpBehaviour[] onPermissionValueChangedListeners;

        private PermissionGroup createdPermissionGroup;
        public override PermissionGroup CreatedPermissionGroup => createdPermissionGroup;
        private PermissionGroup createdPermissionGroupDuplicationSource;
        public override PermissionGroup CreatedPermissionGroupDuplicationSource => createdPermissionGroupDuplicationSource;

        private PermissionGroup deletedPermissionGroup;
        public override PermissionGroup DeletedPermissionGroup => deletedPermissionGroup;

        private PermissionGroup renamedPermissionGroup;
        public override PermissionGroup RenamedPermissionGroup => renamedPermissionGroup;
        private string previousPermissionGroupName;
        public override string PreviousPermissionGroupName => previousPermissionGroupName;

        private PermissionsPlayerData playerDataForEvent;
        public override PermissionsPlayerData PlayerDataForEvent => playerDataForEvent;
        private PermissionGroup previousPlayerPermissionGroup;
        public override PermissionGroup PreviousPlayerPermissionGroup => previousPlayerPermissionGroup;

        private PermissionGroup changedPermissionGroup;
        public override PermissionGroup ChangedPermissionGroup => changedPermissionGroup;
        private PermissionDefinition changedPermission;
        public override PermissionDefinition ChangedPermission => changedPermission;

        private void RaiseOnPermissionGroupDuplicated(PermissionGroup createdPermissionGroup, PermissionGroup createdPermissionGroupDuplicationSource)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  RaiseOnPermissionGroupDuplicated");
#endif
            this.createdPermissionGroup = createdPermissionGroup;
            this.createdPermissionGroupDuplicationSource = createdPermissionGroupDuplicationSource;
            // For some reason UdonSharp needs the 'JanSharp.' namespace name here to resolve the Raise function call.
            JanSharp.CustomRaisedEvents.Raise(ref onPermissionGroupDuplicatedListeners, nameof(PermissionsEventType.OnPermissionGroupDuplicated));
            this.createdPermissionGroup = null; // To prevent misuse of the API.
            this.createdPermissionGroupDuplicationSource = null; // To prevent misuse of the API.
        }

        private void RaiseOnPermissionGroupDeleted(PermissionGroup deletedPermissionGroup)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  RaiseOnPermissionGroupDeleted");
#endif
            this.deletedPermissionGroup = deletedPermissionGroup;
            // For some reason UdonSharp needs the 'JanSharp.' namespace name here to resolve the Raise function call.
            JanSharp.CustomRaisedEvents.Raise(ref onPermissionGroupDeletedListeners, nameof(PermissionsEventType.OnPermissionGroupDeleted));
            this.deletedPermissionGroup = null; // To prevent misuse of the API.
        }

        private void RaiseOnPermissionGroupRenamed(PermissionGroup renamedPermissionGroup, string previousPermissionGroupName)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  RaiseOnPermissionGroupRenamed");
#endif
            this.renamedPermissionGroup = renamedPermissionGroup;
            this.previousPermissionGroupName = previousPermissionGroupName;
            // For some reason UdonSharp needs the 'JanSharp.' namespace name here to resolve the Raise function call.
            JanSharp.CustomRaisedEvents.Raise(ref onPermissionGroupRenamedListeners, nameof(PermissionsEventType.OnPermissionGroupRenamed));
            this.renamedPermissionGroup = null; // To prevent misuse of the API.
            this.previousPermissionGroupName = null; // To prevent misuse of the API.
        }

        private void RaiseOnPlayerPermissionGroupChanged(PermissionsPlayerData playerDataForEvent, PermissionGroup previousPlayerPermissionGroup)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  RaiseOnPlayerPermissionGroupChanged");
#endif
            this.playerDataForEvent = playerDataForEvent;
            this.previousPlayerPermissionGroup = previousPlayerPermissionGroup;
            // For some reason UdonSharp needs the 'JanSharp.' namespace name here to resolve the Raise function call.
            JanSharp.CustomRaisedEvents.Raise(ref onPlayerPermissionGroupChangedListeners, nameof(PermissionsEventType.OnPlayerPermissionGroupChanged));
            this.playerDataForEvent = null; // To prevent misuse of the API.
            this.previousPlayerPermissionGroup = null; // To prevent misuse of the API.
        }

        private void RaiseOnPermissionValueChanged(PermissionGroup changedPermissionGroup, PermissionDefinition changedPermission)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  RaiseOnPermissionValueChanged");
#endif
            this.changedPermissionGroup = changedPermissionGroup;
            this.changedPermission = changedPermission;
            // For some reason UdonSharp needs the 'JanSharp.' namespace name here to resolve the Raise function call.
            JanSharp.CustomRaisedEvents.Raise(ref onPermissionValueChangedListeners, nameof(PermissionsEventType.OnPermissionValueChanged));
            this.changedPermissionGroup = null; // To prevent misuse of the API.
            this.changedPermission = null; // To prevent misuse of the API.
        }

        #endregion
    }
}
