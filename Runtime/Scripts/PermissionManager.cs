using System.Text.RegularExpressions;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;

namespace JanSharp.Internal
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [SingletonDependency(typeof(SingletonManager))]
    [LockstepGameStateDependency(typeof(PlayerDataManagerAPI), SelfLoadsBeforeDependency = true)]
    [CustomRaisedEventsDispatcher(typeof(PermissionsEventAttribute), typeof(PermissionsEventType))]
    public class PermissionManager : PermissionManagerAPI
    {
        public override string GameStateInternalName => "jansharp.permission-system";
        public override string GameStateDisplayName => "Permission System";
        public override bool GameStateSupportsImportExport => true;
        public override uint GameStateDataVersion => 0u;
        public override uint GameStateLowestSupportedDataVersion => 0u;
        [SerializeField] private PermissionImportExportOptionsUI exportUI;
        [SerializeField] private PermissionImportExportOptionsUI importUI;
        public override LockstepGameStateOptionsUI ExportUI => exportUI;
        public override LockstepGameStateOptionsUI ImportUI => importUI;

        [HideInInspector][SerializeField][SingletonReference] private WannaBeClassesManager wannaBeClasses;
        [HideInInspector][SerializeField][SingletonReference] private PlayerDataManagerAPI playerDataManager;

        public override PermissionImportExportOptions ExportOptions => (PermissionImportExportOptions)OptionsForCurrentExport;
        public override PermissionImportExportOptions ImportOptions => (PermissionImportExportOptions)OptionsForCurrentImport;
        private PermissionImportExportOptions optionsFromExport;
        public override PermissionImportExportOptions OptionsFromExport => optionsFromExport;

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

        [HideInInspector][SerializeField] private PermissionResolver[] allPermissionResolvers;
        [HideInInspector][SerializeField] private int allPermissionResolversCount;
        /// <summary><see cref="PermissionResolver"/> resolver => <see langword="int"/> index</summary>
        private DataDictionary allPermissionResolversLut = new DataDictionary();

        private Regex groupNameRegex; // Cannot assign it here, Udon cannot do that. Have to do it in Start.

        private bool isInitialized = false;
        public override bool IsInitialized => isInitialized;

        public override PermissionGroup[] PermissionGroups
        {
            get
            {
                PermissionGroup[] result = new PermissionGroup[permissionGroupsCount];
                System.Array.Copy(permissionGroups, result, permissionGroupsCount);
                return result;
            }
        }
        public override PermissionGroup[] PermissionGroupsRaw => permissionGroups;
        public override int PermissionGroupsCount => permissionGroupsCount;
        public override PermissionGroup GetPermissionGroup(int index) => permissionGroups[index];
        public override PermissionGroup GetPermissionGroup(uint groupId) => (PermissionGroup)groupsById[groupId].Reference;
        public override bool TryGetPermissionGroup(uint groupId, out PermissionGroup group)
        {
            if (groupsById.TryGetValue(groupId, out DataToken groupToken))
            {
                group = (PermissionGroup)groupToken.Reference;
                return true;
            }
            group = null;
            return false;
        }

        private DataDictionary groupsByImportedId;
        public override PermissionGroup GetPermissionGroupFromImportedId(uint importedGroupId)
            => (PermissionGroup)groupsByImportedId[importedGroupId].Reference;

        private PermissionsPlayerData GetPlayerDataForPlayerId(uint playerId)
        {
            return (PermissionsPlayerData)playerDataManager
                .GetCorePlayerDataForPlayerId(playerId)
                .customPlayerData[playerDataClassNameIndex];
        }

        public override bool PlayerHasPermission(CorePlayerData corePlayerData, string permissionInternalName)
        {
            if (!permissionDefsByInternalName.TryGetValue(permissionInternalName, out DataToken defToken))
                return false;
            return ((PermissionsPlayerData)corePlayerData.customPlayerData[playerDataClassNameIndex])
                .permissionGroup.permissionValues[((PermissionDefinition)defToken.Reference).index];
        }

        public override bool PlayerHasPermission(CorePlayerData corePlayerData, PermissionDefinition permissionDef)
        {
            return ((PermissionsPlayerData)corePlayerData.customPlayerData[playerDataClassNameIndex])
                .permissionGroup.permissionValues[permissionDef.index];
        }

        public override PermissionsPlayerData GetPermissionsPlayerData(CorePlayerData corePlayerData)
        {
            return (PermissionsPlayerData)corePlayerData.customPlayerData[playerDataClassNameIndex];
        }

        private void Start()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  Start");
#endif
            groupNameRegex = new Regex(@"^(.*?)(\s+\d+)?$", RegexOptions.Singleline | RegexOptions.Compiled);
            localPlayer = Networking.LocalPlayer;
            localPlayerId = (uint)localPlayer.playerId;
            permissionDefsCount = permissionDefs.Length;
            for (int i = 0; i < permissionDefsCount; i++)
            {
                PermissionDefinition permissionDef = permissionDefs[i];
                permissionDef.index = i;
                permissionDefsByInternalName.Add(permissionDef.internalName, permissionDef);
            }
            if (allPermissionResolversLut == null)
                PopulateAllPermissionResolversLut();
            SendCustomEventDelayedFrames(nameof(PrePopulatePermissionDefResolverIndexLutLoop), 1);
        }

        [PlayerDataEvent(PlayerDataEventType.OnRegisterCustomPlayerData)]
        public void OnRegisterCustomPlayerData()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  OnRegisterCustomPlayerData");
#endif
            playerDataManager.RegisterCustomPlayerData<PermissionsPlayerData>(nameof(PermissionsPlayerData));
        }

        [PlayerDataEvent(PlayerDataEventType.OnAllCustomPlayerDataRegistered)]
        public void OnAllCustomPlayerDataRegistered()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  OnAllCustomPlayerDataRegistered");
#endif
            playerDataClassNameIndex = playerDataManager.GetPlayerDataClassNameIndex<PermissionsPlayerData>(nameof(PermissionsPlayerData));
        }

        [PlayerDataEvent(PlayerDataEventType.OnPrePlayerDataManagerInit)]
        public void OnPrePlayerDataManagerInit()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  OnPrePlayerDataManagerInit");
#endif
            defaultPermissionGroup = wannaBeClasses.New<PermissionGroup>(nameof(PermissionGroup));
            defaultPermissionGroup.isDefault = true;
            defaultPermissionGroup.groupName = PermissionGroup.DefaultGroupName;
            defaultPermissionGroup.id = nextGroupId++;
            bool[] permissionValues = new bool[permissionDefsCount];
            defaultPermissionGroup.permissionValues = permissionValues;
            for (int i = 0; i < permissionDefsCount; i++)
                permissionValues[i] = permissionDefs[i].defaultValue;
            RegisterCreatedPermissionGroup(defaultPermissionGroup);
        }

        [PlayerDataEvent(PlayerDataEventType.OnLocalPlayerDataAvailable)]
        public void OnLocalPlayerDataAvailable()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  OnLocalPlayerDataAvailable");
#endif
            localPlayerData = (PermissionsPlayerData)playerDataManager.LocalPlayerData.customPlayerData[playerDataClassNameIndex];
        }

        [LockstepEvent(LockstepEventType.OnInit, Order = -9500)] // After player data which is -10000
        public void OnInit()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  OnInit");
#endif
            isInitialized = true;
            SetGroupToViewWorldAs(defaultPermissionGroup);
        }

        [LockstepEvent(LockstepEventType.OnClientBeginCatchUp, Order = -9500)]
        public void OnClientBeginCatchUp()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  OnClientBeginCatchUp");
#endif
            isInitialized = true;
            SetGroupToViewWorldAs(localPlayerData.permissionGroup);
        }

        private void SetGroupToViewWorldAs(PermissionGroup group)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  SetGroupToViewWorldAs");
#endif
            if (viewWorldAsGroup == group)
                return;
            viewWorldAsGroup = group;
            bool[] permissionValues = group.permissionValues;
            for (int i = 0; i < permissionDefsCount; i++)
                permissionDefs[i].valueForLocalPlayer = permissionValues[i];
            for (int i = allPermissionResolversCount - 1; i >= 0; i--)
            {
                PermissionResolver resolver = allPermissionResolvers[i];
                if (resolver != null)
                {
                    resolver.Resolve();
                    continue;
                }
                if (i == allPermissionResolversCount - 1)
                    continue;
                PermissionResolver top = allPermissionResolvers[--allPermissionResolversCount];
                allPermissionResolvers[i] = top;
                allPermissionResolversLut[top] = i;
            }
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

        public override string GetFirstUnusedGroupName(string desiredName)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  GetFirstNonCollidingGroupName");
#endif
            if (!groupsByName.ContainsKey(desiredName))
                return desiredName;
            // Group 0 is the entire matching string. 1 is the first user defined regex group.
            string prefix = groupNameRegex.Match(desiredName).Groups[1].Value + " ";
            int postfix = 1;
            desiredName = prefix + postfix;
            while (groupsByName.ContainsKey(desiredName))
                desiredName = prefix + ++postfix;
            return desiredName;
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
            DuplicatePermissionGroupInGS(groupName, (PermissionGroup)groupToken.Reference, playerDataManager.SendingPlayerData);
        }

        public override PermissionGroup DuplicatePermissionGroupInGS(string groupName, PermissionGroup toDuplicate, CorePlayerData playerInitiatingCreation)
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
                RaiseOnPermissionGroupDuplicated(group, toDuplicate, playerInitiatingCreation);
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
            DeletePermissionGroupInGSInternal(group, groupToMovePlayersTo, suppressEventsAndLeaveWorldViewUnchanged: false);
        }

        private void DeletePermissionGroupInGSInternal(
            PermissionGroup group,
            PermissionGroup groupToMovePlayersTo,
            bool suppressEventsAndLeaveWorldViewUnchanged)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  DeletePermissionGroupInGS");
#endif
            group.isDeleted = true;
            PermissionsPlayerData[] players = group.playersInGroup;
            int count = group.playersInGroupCount;
            if (suppressEventsAndLeaveWorldViewUnchanged)
                for (int i = count - 1; i >= 0; i--)
                    PlayerDataPermissionGroupSetter(players[i], groupToMovePlayersTo);
            else
                for (int i = count - 1; i >= 0; i--)
                    SetPlayerDataPermissionGroup(players[i], groupToMovePlayersTo, group);
            ArrList.Remove(ref permissionGroups, ref permissionGroupsCount, group);
            groupsById.Remove(group.id);
            groupsByName.Remove(group.groupName);
            if (!suppressEventsAndLeaveWorldViewUnchanged)
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
            playerDataManager.WriteCorePlayerDataRef(corePlayerData);
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
            CorePlayerData corePlayerData = playerDataManager.ReadCorePlayerDataRef();
            if (corePlayerData == null)
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
            PlayerDataPermissionGroupSetter(playerData, group);
            if (playerData.core.playerId == localPlayerId)
                SetGroupToViewWorldAs(group);
            RaiseOnPlayerPermissionGroupChanged(playerData, prevGroup);
        }

        public override void WritePermissionGroupRef(PermissionGroup permissionGroup)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  WritePermissionGroupRef");
#endif
            lockstep.WriteSmallUInt(permissionGroup.id);
        }

        public override PermissionGroup ReadPermissionGroupRef()
        {
            return ReadPermissionGroupRef(lockstep.IsDeserializingForImport);
        }

        public override PermissionGroup ReadPermissionGroupRef(bool isImport)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  ReadPermissionGroupRef");
#endif
            return isImport ? GetPermissionGroupFromImportedId(lockstep.ReadSmallUInt())
                : groupsById.TryGetValue(lockstep.ReadSmallUInt(), out DataToken groupToken) ? (PermissionGroup)groupToken.Reference
                : null;
        }

        /// <summary>
        /// <para>Internal API.</para>
        /// </summary>
        /// <param name="playerData"></param>
        /// <param name="group"></param>
        public void PlayerDataPermissionGroupSetter(PermissionsPlayerData playerData, PermissionGroup group)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  SetPlayerPermissionGroup");
#endif
            PermissionGroup prevGroup = playerData.permissionGroup;
            if (prevGroup == group)
                return;
            playerData.permissionGroup = group;

            int indexInGroup = playerData.indexInPlayersInGroup;
            if (indexInGroup != -1 && prevGroup != null && !prevGroup.isDeleted)
            {
                PermissionsPlayerData[] players = prevGroup.playersInGroup;
                int count = prevGroup.playersInGroupCount - 1;
                prevGroup.playersInGroupCount = count;
                if (count != indexInGroup) // An if just for optimization.
                {
                    PermissionsPlayerData otherPlayerData = players[count];
                    players[indexInGroup] = otherPlayerData;
                    otherPlayerData.indexInPlayersInGroup = indexInGroup;
                }

                indexInGroup = playerData.indexInOnlinePlayersInGroup;
                if (indexInGroup != -1)
                    RemoveFromOnlinePlayersInGroup(prevGroup, indexInGroup);
            }

            if (group == null)
            {
                playerData.indexInOnlinePlayersInGroup = -1;
                playerData.indexInPlayersInGroup = -1;
                return;
            }

            ArrList.Add(ref group.playersInGroup, ref group.playersInGroupCount, playerData);
            playerData.indexInPlayersInGroup = group.playersInGroupCount - 1;
            if (playerData.core.isOffline)
                return;
            ArrList.Add(ref group.onlinePlayersInGroup, ref group.onlinePlayersInGroupCount, playerData);
            playerData.indexInOnlinePlayersInGroup = group.onlinePlayersInGroupCount - 1;
        }

        private void RemoveFromOnlinePlayersInGroup(PermissionGroup group, int index)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  RemoveFromOnlinePlayersInGroup - group.groupName: {group.groupName}, index: {index}");
#endif
            PermissionsPlayerData[] players = group.onlinePlayersInGroup;
            int count = group.onlinePlayersInGroupCount - 1;
            group.onlinePlayersInGroupCount = count;
            if (count != index) // An if just for optimization.
            {
                PermissionsPlayerData otherPlayerData = players[count];
                players[index] = otherPlayerData;
                otherPlayerData.indexInOnlinePlayersInGroup = index;
            }
        }

        [PlayerDataEvent(PlayerDataEventType.OnPlayerDataWentOffline, Order = -10000)]
        public void OnPlayerDataWentOffline()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  OnPlayerDataWentOffline");
#endif
            PermissionsPlayerData playerData = (PermissionsPlayerData)playerDataManager.PlayerDataForEvent.customPlayerData[playerDataClassNameIndex];
            RemoveFromOnlinePlayersInGroup(playerData.permissionGroup, playerData.indexInOnlinePlayersInGroup);
            playerData.indexInOnlinePlayersInGroup = -1;
        }

        [PlayerDataEvent(PlayerDataEventType.OnPlayerDataWentOnline, Order = -10000)]
        public void OnPlayerDataWentOnline()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  OnPlayerDataWentOnline");
#endif
            PermissionsPlayerData playerData = (PermissionsPlayerData)playerDataManager.PlayerDataForEvent.customPlayerData[playerDataClassNameIndex];
            PermissionGroup group = playerData.permissionGroup;
            ArrList.Add(ref group.onlinePlayersInGroup, ref group.onlinePlayersInGroupCount, playerData);
            playerData.indexInOnlinePlayersInGroup = group.onlinePlayersInGroupCount - 1;
        }

        public override void SendSetPermissionValueIA(PermissionGroup group, string permissionInternalName, bool value)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  SendSetPermissionValueIA - permissionInternalName: {permissionInternalName}");
#endif
            if (!permissionDefsByInternalName.TryGetValue(permissionInternalName, out DataToken defToken))
                return;
            SendSetPermissionValueIA(group, (PermissionDefinition)defToken.Reference, value);
        }

        public override void SendSetPermissionValueIA(PermissionGroup group, PermissionDefinition permissionDef, bool value)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  SendSetPermissionValueIA");
#endif
            lockstep.WriteSmallUInt(group.id);
            lockstep.WriteSmallUInt((uint)permissionDef.index);
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
            Debug.Log($"[PermissionSystemDebug] Manager  SetPermissionValueInGS - permissionInternalName: {permissionInternalName}");
#endif
            if (!permissionDefsByInternalName.TryGetValue(permissionInternalName, out DataToken defToken))
                return;
            SetPermissionValueInGS(group, (PermissionDefinition)defToken.Reference, value);
        }

        public override void SetPermissionValueInGS(PermissionGroup group, PermissionDefinition permissionDef, bool value)
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
                PermissionResolver[] resolvers = permissionDef.resolvers;
                int resolversCount = permissionDef.resolversCount;
                for (int i = 0; i < resolversCount; i++)
                {
                    PermissionResolver resolver = resolvers[i];
                    if (resolver != null)
                        resolver.Resolve();
                }
            }
            RaiseOnPermissionValueChanged(group, permissionDefs[index]);
        }

        private void PopulateAllPermissionResolversLut()
        {
            allPermissionResolversLut = new DataDictionary();
            for (int i = allPermissionResolversCount - 1; i >= 0; i--)
            {
                PermissionResolver resolver = allPermissionResolvers[i];
                if (resolver != null)
                {
                    allPermissionResolversLut.Add(resolver, i);
                    continue;
                }
                if (i == allPermissionResolversCount - 1)
                    continue;
                PermissionResolver top = allPermissionResolvers[--allPermissionResolversCount];
                allPermissionResolvers[i] = top;
                allPermissionResolversLut[top] = i;
            }
        }

        public override bool IsResolverExistenceRegistered(PermissionResolver resolver)
        {
            if (allPermissionResolversLut == null)
                PopulateAllPermissionResolversLut();
            return allPermissionResolversLut.ContainsKey(resolver);
        }

        public override void RegisterResolverExistence(PermissionResolver resolver)
        {
            if (allPermissionResolversLut == null)
                PopulateAllPermissionResolversLut();
            allPermissionResolversLut.Add(resolver, allPermissionResolversCount);
            ArrList.Add(ref allPermissionResolvers, ref allPermissionResolversCount, resolver);
        }

        public override void DeregisterResolverExistence(PermissionResolver resolver)
        {
            if (!allPermissionResolversLut.Remove(resolver, out DataToken indexToken))
                return;
            int index = indexToken.Int;
            if ((--allPermissionResolversCount) == index)
                return;
            PermissionResolver top = allPermissionResolvers[index];
            allPermissionResolvers[index] = top;
            allPermissionResolversLut[top] = index;
        }

        public override void RegisterResolver(PermissionResolver resolver, PermissionDefinition[] permissionDefs)
        {
            RegisterResolver(resolver, permissionDefs, 0, permissionDefs.Length);
        }

        public override void RegisterResolver(PermissionResolver resolver, PermissionDefinition[] permissionDefs, int startIndex)
        {
            RegisterResolver(resolver, permissionDefs, startIndex, permissionDefs.Length - startIndex);
        }

        public override void RegisterResolver(PermissionResolver resolver, PermissionDefinition[] permissionDefs, int startIndex, int count)
        {
            int stop = startIndex + count;
            for (int i = startIndex; i < stop; i++)
                permissionDefs[i].RegisterResolver(resolver);
        }

        public override void DeregisterResolver(PermissionResolver resolver, PermissionDefinition[] permissionDefs)
        {
            DeregisterResolver(resolver, permissionDefs, 0, permissionDefs.Length);
        }

        public override void DeregisterResolver(PermissionResolver resolver, PermissionDefinition[] permissionDefs, int startIndex)
        {
            DeregisterResolver(resolver, permissionDefs, startIndex, permissionDefs.Length - startIndex);
        }

        public override void DeregisterResolver(PermissionResolver resolver, PermissionDefinition[] permissionDefs, int startIndex, int count)
        {
            int stop = startIndex + count;
            for (int i = startIndex; i < stop; i++)
                permissionDefs[i].DeregisterResolver(resolver);
        }

        private int nextIndexToPrePopulate;
        public void PrePopulatePermissionDefResolverIndexLutLoop()
        {
            // Doing this here rather than at the end makes handling 0 defs easier.
            if (nextIndexToPrePopulate == permissionDefsCount)
                return;
            PermissionDefinition permissionDef = permissionDefs[nextIndexToPrePopulate++];
            permissionDef.PrePopulateResolverIndexLut();
            SendCustomEventDelayedFrames(nameof(PrePopulatePermissionDefResolverIndexLutLoop), 1);
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
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  ImportPermissionDefinitionsMetadata");
#endif
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

        private void OnlyBuildGroupsByImportedId(int count)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  OnlyBuildGroupsByImportedId");
#endif
            groupsByImportedId = new DataDictionary();
            for (int i = 0; i < count; i++)
            {
                string groupName = lockstep.ReadString();
                uint importedId = lockstep.ReadSmallUInt();
                PermissionGroup group = groupsByName.TryGetValue(groupName, out DataToken groupToken)
                    ? (PermissionGroup)groupToken.Reference
                    : defaultPermissionGroup; // Players in groups not currently present in the world get set to the default group.
                groupsByImportedId.Add(importedId, group);
#if PERMISSION_SYSTEM_DEBUG
                Debug.Log($"[PermissionSystemDebug] Manager  OnlyBuildGroupsByImportedId (inner) - importedId: {importedId}, group.id: {group.id}");
#endif
            }
        }

        private void ImportPermissionGroupNamesAndIds(int count, bool movePlayersOutOfDeletedGroups)
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
                    : DuplicatePermissionGroupInGS(groupName, defaultPermissionGroup, playerInitiatingCreation: null);
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
                    if (movePlayersOutOfDeletedGroups)
                        DeletePermissionGroupInGSInternal(group, defaultPermissionGroup, suppressEventsAndLeaveWorldViewUnchanged: true);
                    else
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

        private void Export(PermissionImportExportOptions exportOptions)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  Export");
#endif
            lockstep.WriteCustomClass(exportOptions);

            lockstep.WriteSmallUInt((uint)permissionGroupsCount);
            ExportPermissionGroupNamesAndIds();

            if (!exportOptions.includePermissionGroups)
                return;
            lockstep.WriteSmallUInt((uint)permissionDefsCount);
            ExportPermissionDefinitionsMetadata();
            ExportPermissionGroupFlags();
        }

        private void Import(PermissionImportExportOptions importOptions)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  Import");
#endif
            optionsFromExport = (PermissionImportExportOptions)lockstep.ReadCustomClass(nameof(PermissionImportExportOptions));
            bool doImportPermissionGroups = optionsFromExport.includePermissionGroups && importOptions.includePermissionGroups;
            bool doImportPlayerPermissionGroups = optionsFromExport.includePlayerPermissionGroups && importOptions.includePlayerPermissionGroups;
            // Even if both permission groups and player permission groups do not get imported, still build
            // the groupsByImportedId lut to provide the guarantee to all systems that reading permission
            // group references in imports is always going to resolve properly.
            // The permission system itself relies this guarantee in the player data import actually.

            int importedGroupsCount = (int)lockstep.ReadSmallUInt();
            if (doImportPermissionGroups)
                ImportPermissionGroupNamesAndIds(importedGroupsCount, movePlayersOutOfDeletedGroups: !doImportPlayerPermissionGroups);
            else
                OnlyBuildGroupsByImportedId(importedGroupsCount);

            if (!doImportPermissionGroups)
                return;
            int importedDefsCount = (int)lockstep.ReadSmallUInt();
            int[] correspondingImportedDefIndexMap = ImportPermissionDefinitionsMetadata(importedDefsCount);
            ImportPermissionGroupFlags(importedDefsCount, correspondingImportedDefIndexMap);
        }

        [LockstepEvent(LockstepEventType.OnImportFinishingUp, Order = 10000)]
        public void OnImportFinishingUp()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  OnImportFinishingUp");
#endif
            if (!IsPartOfCurrentImport)
                return;
            if (optionsFromExport.includePermissionGroups && ImportOptions.includePermissionGroups)
                viewWorldAsGroup = null; // Force refresh even if the group is the same, permission values could have changed.
            // If the above did not happen and the group is the same this will do nothing, as it should.
            SetGroupToViewWorldAs(localPlayerData.permissionGroup);
        }

        [LockstepEvent(LockstepEventType.OnImportFinished, Order = 10000)]
        public void OnImportFinished()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  OnImportFinished");
#endif
            if (!IsPartOfCurrentImport)
                return;
            optionsFromExport.DecrementRefsCount();
            optionsFromExport = null;
            groupsByImportedId = null;
        }

        public override void SerializeGameState(bool isExport, LockstepGameStateOptionsData exportOptions)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  SerializeGameState");
#endif
            if (isExport)
            {
                Export((PermissionImportExportOptions)exportOptions);
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
                Import((PermissionImportExportOptions)importOptions);
                return null;
            }
            nextGroupId = lockstep.ReadSmallUInt();
            ReadPermissionGroups();
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
        private CorePlayerData playerDataCreatingPermissionGroup;
        public override CorePlayerData PlayerDataCreatingPermissionGroup => playerDataCreatingPermissionGroup;

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

        private void RaiseOnPermissionGroupDuplicated(PermissionGroup createdPermissionGroup, PermissionGroup createdPermissionGroupDuplicationSource, CorePlayerData playerDataCreatingPermissionGroup)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] Manager  RaiseOnPermissionGroupDuplicated");
#endif
            this.createdPermissionGroup = createdPermissionGroup;
            this.createdPermissionGroupDuplicationSource = createdPermissionGroupDuplicationSource;
            this.playerDataCreatingPermissionGroup = playerDataCreatingPermissionGroup;
            // For some reason UdonSharp needs the 'JanSharp.' namespace name here to resolve the Raise function call.
            JanSharp.CustomRaisedEvents.Raise(ref onPermissionGroupDuplicatedListeners, nameof(PermissionsEventType.OnPermissionGroupDuplicated));
            this.createdPermissionGroup = null; // To prevent misuse of the API.
            this.createdPermissionGroupDuplicationSource = null; // To prevent misuse of the API.
            this.playerDataCreatingPermissionGroup = null; // To prevent misuse of the API.
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
