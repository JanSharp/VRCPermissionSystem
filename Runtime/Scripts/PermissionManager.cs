using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [LockstepGameStateDependency(typeof(PlayerDataManagerAPI))]
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
        [HideInInspector][SerializeField][SingletonReference] private PlayerDataManagerAPI playerDataManager;

        #region GameState
        private uint nextGroupId = 1u;
        [System.NonSerialized] public PermissionGroup defaultPermissionGroup;
        private PermissionGroup[] permissionGroups = new PermissionGroup[ArrList.MinCapacity];
        private DataDictionary groupsById = new DataDictionary();
        private DataDictionary groupsByName = new DataDictionary();
        private int permissionGroupsCount = 0;
        [BuildTimeIdAssignment(nameof(permissionDefIds), nameof(highestPermissionDefId))]
        [HideInInspector][SerializeField] private PermissionDefinition[] permissionDefs;
        [HideInInspector][SerializeField] private uint[] permissionDefIds; // TODO: unused
        [HideInInspector][SerializeField] private uint highestPermissionDefId; // TODO: unused
        private int permissionDefsCount;
        private DataDictionary permissionDefsByInternalName = new DataDictionary();
        #endregion

        private DataDictionary groupsByImportedId;

        private void Start()
        {
            permissionDefsCount = permissionDefs.Length;
            for (int i = 0; i < permissionDefsCount; i++)
            {
                PermissionDefinition permissionDef = permissionDefs[i];
                permissionDef.index = i;
                permissionDefsByInternalName.Add(permissionDef.internalName, permissionDef);
            }
        }

        [LockstepEvent(LockstepEventType.OnInit)]
        public void OnInit()
        {
            defaultPermissionGroup = wannaBeClasses.New<PermissionGroup>(nameof(PermissionGroup));
            defaultPermissionGroup.isDefault = true;
            defaultPermissionGroup.groupName = PermissionGroup.DefaultGroupName;
            defaultPermissionGroup.id = nextGroupId++;
            bool[] permissionValues = new bool[permissionDefsCount];
            defaultPermissionGroup.permissionValues = permissionValues;
            int length = permissionDefsCount;
            for (int i = 0; i < length; i++)
                permissionValues[i] = permissionDefs[i].defaultValue;
            RegisterCreatedPermissionGroup(defaultPermissionGroup);
        }

        private PermissionGroup CreatePermissionGroup(string groupName)
        {
            PermissionGroup group = wannaBeClasses.New<PermissionGroup>(nameof(PermissionGroup));
            group.groupName = groupName;
            defaultPermissionGroup.id = nextGroupId++;
            bool[] permissionValues = new bool[permissionDefsCount];
            group.permissionValues = permissionValues;
            defaultPermissionGroup.permissionValues.CopyTo(permissionValues, index: 0);
            RegisterCreatedPermissionGroup(group);
            return group;
        }

        private void RegisterCreatedPermissionGroup(PermissionGroup group)
        {
            ArrList.Add(ref permissionGroups, ref permissionGroupsCount, group);
            groupsById.Add(group.id, defaultPermissionGroup);
            groupsByName.Add(group.groupName, group);
        }

        private void DeletePermissionGroup(PermissionGroup group)
        {
            ArrList.Remove(ref permissionGroups, ref permissionGroupsCount, group);
            groupsById.Remove(group.id);
            groupsByName.Remove(group.groupName);
            group.DecrementRefsCount();
        }

        private void WritePermissionGroup(PermissionGroup group)
        {
            lockstep.WriteString(group.groupName);
            lockstep.WriteFlags(group.permissionValues);
        }

        private PermissionGroup ReadPermissionGroup()
        {
            PermissionGroup group = wannaBeClasses.New<PermissionGroup>(nameof(PermissionGroup));
            string groupName = lockstep.ReadString();
            group.groupName = groupName;
            group.isDefault = groupName == PermissionGroup.DefaultGroupName;
            group.permissionValues = new bool[permissionDefsCount];
            lockstep.ReadFlags(group.permissionValues);
            return group;
        }

        private void WritePermissionGroups()
        {
            lockstep.WriteSmallUInt((uint)permissionGroupsCount);
            for (int i = 0; i < permissionGroupsCount; i++)
                WritePermissionGroup(permissionGroups[i]);
        }

        private void ReadPermissionGroups()
        {
            permissionGroupsCount = (int)lockstep.ReadSmallUInt();
            ArrList.EnsureCapacity(ref permissionGroups, permissionGroupsCount);
            for (int i = 0; i < permissionGroupsCount; i++)
                permissionGroups[i] = ReadPermissionGroup();
            defaultPermissionGroup = permissionGroups[0];
        }

        private void ExportPermissionDefinitionsMetadata()
        {
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
            foreach (PermissionGroup group in permissionGroups)
            {
                lockstep.WriteString(group.groupName);
                lockstep.WriteSmallUInt(group.id);
            }
        }

        private void ImportPermissionGroupNamesAndIds(int count)
        {
            int capacity = ArrList.MinCapacity;
            while (capacity < count)
                capacity *= 2;
            PermissionGroup[] importedGroups = new PermissionGroup[capacity];
            groupsByImportedId = new DataDictionary();
            int originalPermissionGroupsCount = permissionGroupsCount; // To skip redundantly processing the new ones later.

            for (int i = 0; i < count; i++)
            {
                string groupName = lockstep.ReadString();
                uint importedId = lockstep.ReadSmallUInt();
                PermissionGroup group = groupsByName.TryGetValue(groupName, out DataToken groupToken)
                    ? (PermissionGroup)groupToken.Reference
                    : CreatePermissionGroup(groupName);
                importedGroups[i] = group;
                groupsByImportedId.Add(importedId, group);
            }

            // For the sake of the api, delete existing groups even though they could've been reused by renaming them.
            for (int i = originalPermissionGroupsCount - 1; i >= 0; i--)
            {
                PermissionGroup group = permissionGroups[i];
                if (!groupsByImportedId.ContainsKey(group.id))
                    DeletePermissionGroup(group);
            }

            permissionGroups = importedGroups; // To make the order match what was imported.
        }

        private void ExportPermissionGroupFlags()
        {
            foreach (PermissionGroup group in permissionGroups)
                lockstep.WriteFlags(group.permissionValues);
        }

        private void ImportPermissionGroupFlags(int importedDefsCount, int[] defsCorrespondingImportedIndex)
        {
            bool[] importedFlags = new bool[importedDefsCount];
            foreach (PermissionGroup group in permissionGroups)
            {
                lockstep.ReadFlags(importedFlags);
                bool[] permissionValues = group.permissionValues;
                for (int i = 0; i < permissionDefsCount; i++)
                {
                    int correspondingImportedIndex = defsCorrespondingImportedIndex[i];
                    if (correspondingImportedIndex != -1)
                        permissionValues[i] = importedFlags[correspondingImportedIndex];
                }
            }
        }

        private void ResolvePlayerPermissionData(DataDictionary groupsById)
        {
            foreach (PermissionsPlayerData playerData in playerDataManager.GetAllPlayerData<PermissionsPlayerData>(nameof(PermissionsPlayerData)))
                playerData.permissionGroup = (PermissionGroup)groupsById[playerData.deserializedId].Reference;
        }

        private void Export()
        {
            lockstep.WriteSmallUInt((uint)permissionDefsCount);
            ExportPermissionDefinitionsMetadata();
            lockstep.WriteSmallUInt((uint)permissionGroupsCount);
            ExportPermissionGroupNamesAndIds();
            ExportPermissionGroupFlags();
        }

        private void Import()
        {
            int importedDefsCount = (int)lockstep.ReadSmallUInt();
            int[] correspondingImportedDefIndexMap = ImportPermissionDefinitionsMetadata(importedDefsCount);
            int importedGroupsCount = (int)lockstep.ReadSmallUInt();
            ImportPermissionGroupNamesAndIds(importedGroupsCount);
            ImportPermissionGroupFlags(importedDefsCount, correspondingImportedDefIndexMap);

            ResolvePlayerPermissionData(groupsByImportedId);
        }

        [LockstepEvent(LockstepEventType.OnImportFinished)]
        public void OnImportFinished()
        {
            groupsByImportedId = null;
        }

        public override void SerializeGameState(bool isExport, LockstepGameStateOptionsData exportOptions)
        {
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
            if (isImport)
            {
                Import();
                return null;
            }
            nextGroupId = lockstep.ReadSmallUInt();
            ReadPermissionGroups();

            ResolvePlayerPermissionData(groupsById);
            return null;
        }
    }
}
