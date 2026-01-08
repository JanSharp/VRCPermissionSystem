using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class TestPermissionsUI : UdonSharpBehaviour
    {
        [HideInInspector][SerializeField][SingletonReference] private WidgetManager widgets;
        [HideInInspector][SerializeField][SingletonReference] private PermissionManagerAPI permissionManager;
        [HideInInspector][SerializeField][SingletonReference] private PlayerDataManagerAPI playerDataManager;
        public GenericValueEditor groupsEditor;
        public GenericValueEditor permissionsEditor;
        public GenericValueEditor playersEditor;
        public GenericValueEditor playerGroupSelectionEditor;
        private WidgetData[] groupsEditorWidgets;
        private GroupingWidgetData groupsEditorGrouping;

        private ButtonWidgetData deleteGroupButton;
        private StringFieldWidgetData groupNameField;
        private ToggleFieldWidgetData[] permissionToggles;

        /// <summary>Weak WannaBeClass reference.</summary>
        private PermissionGroup editingPermissionGroup;
        /// <summary>
        /// <para><see cref="PermissionGroup"/> group => <see cref="ButtonWidgetData"/> groupButton</para>
        /// </summary>
        private DataDictionary groupButtonsByGroup = new DataDictionary();

        /// <summary>Weak WannaBeClass reference.</summary>
        private PermissionsPlayerData editingPlayerData;
        /// <summary>
        /// <para><see cref="PermissionsPlayerData"/> playerData => <see cref="ButtonWidgetData"/> playerButton</para>
        /// </summary>
        private DataDictionary playerButtonsByPlayerData = new DataDictionary();

        /// <summary>
        /// <para><see cref="PermissionGroup"/> group => <see cref="ButtonWidgetData"/> playerGroupButton</para>
        /// </summary>
        private DataDictionary playerGroupButtonsByGroup = new DataDictionary();
        /// <summary>Weak WannaBeClass reference.</summary>
        private PermissionGroup selectedPlayerGroupInUI;

        private bool isInitialized = false;

        [LockstepEvent(LockstepEventType.OnInit)]
        public void OnInit()
        {
            // permissionManager.DuplicatePermissionGroupInGS("Test", permissionManager.DefaultPermissionGroup);
            // PermissionGroup foo = permissionManager.DuplicatePermissionGroupInGS("Foo", permissionManager.DefaultPermissionGroup);
            // PermissionGroup bar = permissionManager.DuplicatePermissionGroupInGS("Bar", permissionManager.DefaultPermissionGroup);

            // foo.permissionValues[1] = false;
            // foo.permissionValues[2] = false;

            // bar.permissionValues[2] = false;

            InitUI();
        }

        [LockstepEvent(LockstepEventType.OnClientBeginCatchUp)]
        public void OnClientBeginCatchUp()
        {
            InitUI();
        }

        private void InitUI()
        {
            PopulatePermissionsEditor();
            InitGroupsLists(); // Requires permissions editor to be populated.
            DrawPlayerList(); // Requires groups to be populated.
            isInitialized = true;
        }

        private void PopulatePermissionsEditor()
        {
            PermissionDefinition[] permissionDefs = permissionManager.PermissionDefinitions;
            int defsCount = permissionDefs.Length;
            permissionToggles = new ToggleFieldWidgetData[defsCount];
            WidgetData[] content = new WidgetData[defsCount + 1];
            groupNameField = (StringFieldWidgetData)widgets.NewStringField("Group Name", "")
                .SetListener(this, nameof(OnGroupNameValueChanged));
            content[0] = groupNameField;

            for (int i = 0; i < defsCount; i++)
            {
                var def = permissionDefs[i];
                var toggle = (ToggleFieldWidgetData)widgets.NewToggleField(def.displayName, false)
                    .SetCustomData(nameof(affectedPermission), def)
                    .SetListener(this, nameof(OnPermissionToggleValueChanged));
                permissionToggles[i] = toggle;
                content[i + 1] = toggle;
            }

            permissionsEditor.Draw(content);
        }

        private string FormatGroupName(PermissionGroup group)
        {
            return editingPermissionGroup == group
                ? $"<b>{group.groupName}</b>"
                : group.groupName;
        }

        private void GenerateGroupsButtonWidgets(
            out ButtonWidgetData[] groupButtons,
            out ButtonWidgetData[] playerGroupButtons)
        {
            groupButtonsByGroup.Clear();
            playerGroupButtonsByGroup.Clear();

            int count = permissionManager.PermissionGroupsCount;
            groupButtons = new ButtonWidgetData[count];
            playerGroupButtons = new ButtonWidgetData[count];
            for (int i = 0; i < count; i++)
            {
                PermissionGroup group = permissionManager.GetPermissionGroup(i);
                groupButtons[i] = (ButtonWidgetData)widgets.NewButton(FormatGroupName(group))
                    .SetCustomData(nameof(clickedPermissionGroup), group)
                    .SetListener(this, nameof(OnPermissionGroupButtonClick))
                    .StdMoveWidget();
                playerGroupButtons[i] = (ButtonWidgetData)widgets.NewButton(group.groupName)
                    .SetCustomData(nameof(clickedPlayerGroup), group)
                    .SetListener(this, nameof(OnPlayerGroupButtonClick))
                    .StdMoveWidget();
                groupButtonsByGroup.Add(group, groupButtons[i]);
                playerGroupButtonsByGroup.Add(group, playerGroupButtons[i]);
            }
        }

        private void InitGroupsLists()
        {
            GenerateGroupsButtonWidgets(
                out ButtonWidgetData[] groupButtons,
                out ButtonWidgetData[] playerGroupButtons);

            groupsEditor.Draw(groupsEditorWidgets = new WidgetData[]
            {
                widgets.NewButton("Duplicate")
                    .SetListener(this, nameof(OnDuplicateGroupClick)),
                deleteGroupButton = (ButtonWidgetData)widgets.NewButton("Delete")
                    .SetListener(this, nameof(OnDeleteGroupClick)),
                widgets.NewLine(),
                groupsEditorGrouping = (GroupingWidgetData)widgets.NewGrouping().SetChildrenChained(groupButtons),
            });
            playerGroupSelectionEditor.Draw(playerGroupButtons);

            if (editingPermissionGroup != null)
                return;
            SetEditingPermissionGroup(permissionManager.DefaultPermissionGroup);
        }

        private void RedrawGroupsLists()
        {
            GenerateGroupsButtonWidgets(
                out ButtonWidgetData[] groupButtons,
                out ButtonWidgetData[] playerGroupButtons);

            groupsEditorGrouping.SetChildren(groupButtons);
            groupsEditor.Draw(groupsEditorWidgets);
            playerGroupSelectionEditor.Draw(playerGroupButtons);
            SetEditingPermissionGroup(editingPermissionGroup);
            UpdateSelectedPlayerGroup();
        }

        private void SetEditingPermissionGroup(PermissionGroup group)
        {
            ButtonWidgetData button;
            PermissionGroup pervGroup = editingPermissionGroup;
            editingPermissionGroup = group; // Set before calling FormatGroupName.
            if (pervGroup != null)
            {
                button = (ButtonWidgetData)groupButtonsByGroup[pervGroup].Reference;
                button.Label = FormatGroupName(pervGroup);
            }
            button = (ButtonWidgetData)groupButtonsByGroup[group].Reference;
            button.Label = FormatGroupName(group);

            groupNameField.Value = group.groupName;
            groupNameField.Interactable = !group.isDefault;
            deleteGroupButton.Interactable = !group.isDefault;

            int defsCount = permissionManager.PermissionDefinitions.Length;
            bool[] groupValues = editingPermissionGroup.permissionValues;
            for (int i = 0; i < defsCount; i++)
                permissionToggles[i].SetValueWithoutNotify(groupValues[i]);
        }

        private void DrawPlayerList()
        {
            playerButtonsByPlayerData.Clear();
            PermissionsPlayerData[] allPlayerData = playerDataManager.GetAllPlayerData<PermissionsPlayerData>(nameof(PermissionsPlayerData));
            int count = allPlayerData.Length;
            ButtonWidgetData[] playerButtons = new ButtonWidgetData[count];
            for (int i = 0; i < count; i++)
            {
                PermissionsPlayerData playerData = allPlayerData[i];
                playerButtons[i] = (ButtonWidgetData)widgets.NewButton(playerData.core.displayName)
                    .SetCustomData(nameof(clickedPlayerData), playerData)
                    .SetListener(this, nameof(OnPlayerButtonClick))
                    .StdMoveWidget();
                playerButtonsByPlayerData.Add(playerData, playerButtons[i]);
            }
            playersEditor.Draw(playerButtons);

            SetEditingPlayer(editingPlayerData
                ?? playerDataManager.GetPlayerDataForPlayerId<PermissionsPlayerData>(
                    nameof(PermissionsPlayerData),
                    (uint)Networking.LocalPlayer.playerId));
        }

        private void SetEditingPlayer(PermissionsPlayerData playerData)
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] TestPermissionsUI  SetEditingPlayer - playerData.core.displayName: {playerData.core.displayName}");
#endif
            ButtonWidgetData button;
            if (editingPlayerData != null)
            {
                button = (ButtonWidgetData)playerButtonsByPlayerData[editingPlayerData].Reference;
                button.Label = editingPlayerData.core.displayName;
            }
            editingPlayerData = playerData;
            button = (ButtonWidgetData)playerButtonsByPlayerData[playerData].Reference;

            button.Label = $"<b>{playerData.core.displayName}</b>";

            UpdateSelectedPlayerGroup();
        }

        private void UpdateSelectedPlayerGroup()
        {
#if PERMISSION_SYSTEM_DEBUG
            Debug.Log($"[PermissionSystemDebug] TestPermissionsUI  UpdateSelectedPlayerGroup - selectedPlayerGroupInUI != null: {selectedPlayerGroupInUI != null}, editingPlayerData.permissionGroup.groupName: {editingPlayerData.permissionGroup.groupName}, playerGroupButtonsByGroup: {playerGroupButtonsByGroup.Count}");
#endif
            ButtonWidgetData button;
            if (selectedPlayerGroupInUI != null && !selectedPlayerGroupInUI.isDeleted)
            {
                button = (ButtonWidgetData)playerGroupButtonsByGroup[selectedPlayerGroupInUI].Reference;
                button.Label = selectedPlayerGroupInUI.groupName;
            }
            selectedPlayerGroupInUI = editingPlayerData.permissionGroup;
            button = (ButtonWidgetData)playerGroupButtonsByGroup[selectedPlayerGroupInUI].Reference;
            button.Label = $"<b>{selectedPlayerGroupInUI.groupName}</b>";
        }

        public void OnDuplicateGroupClick()
        {
            string groupName = permissionManager.GetFirstUnusedGroupName(editingPermissionGroup.groupName);
            permissionManager.SendDuplicatePermissionGroupIA(groupName, editingPermissionGroup);
        }

        public void OnDeleteGroupClick()
        {
            permissionManager.SendDeletePermissionGroupIA(editingPermissionGroup, permissionManager.DefaultPermissionGroup);
        }

        public void OnGroupNameValueChanged()
        {
            permissionManager.SendRenamePermissionGroupIA(editingPermissionGroup, groupNameField.Value);
        }

        private PermissionDefinition affectedPermission;
        public void OnPermissionToggleValueChanged()
        {
            permissionManager.SendSetPermissionValueIA(
                editingPermissionGroup,
                affectedPermission,
                permissionsEditor.GetSendingToggleField().Value);
        }

        private PermissionGroup clickedPermissionGroup;
        public void OnPermissionGroupButtonClick()
        {
            SetEditingPermissionGroup(clickedPermissionGroup);
        }

        private PermissionGroup clickedPlayerGroup;
        public void OnPlayerGroupButtonClick()
        {
            permissionManager.SendSetPlayerPermissionGroupIA(editingPlayerData.core, clickedPlayerGroup);
        }

        private PermissionsPlayerData clickedPlayerData;
        public void OnPlayerButtonClick()
        {
            SetEditingPlayer(clickedPlayerData);
        }

        [PermissionsEvent(PermissionsEventType.OnPermissionGroupDuplicated)]
        public void OnPermissionGroupDuplicated()
        {
            if (!isInitialized)
                return;
            CorePlayerData player = permissionManager.PlayerDataCreatingPermissionGroup;
            if (player == null || !player.isLocal)
                return;
            editingPermissionGroup = permissionManager.CreatedPermissionGroup;
            RedrawGroupsLists();
        }

        [PermissionsEvent(PermissionsEventType.OnPermissionGroupDeleted)]
        public void OnPermissionGroupDeleted()
        {
            if (!isInitialized)
                return;
            if (permissionManager.DeletedPermissionGroup == editingPermissionGroup)
                editingPermissionGroup = permissionManager.DefaultPermissionGroup;
            RedrawGroupsLists();
        }

        [PermissionsEvent(PermissionsEventType.OnPermissionGroupRenamed)]
        public void OnPermissionGroupRenamed()
        {
            if (!isInitialized)
                return;
            PermissionGroup renamedGroup = permissionManager.RenamedPermissionGroup;
            if (renamedGroup == editingPermissionGroup)
                groupNameField.SetValueWithoutNotify(editingPermissionGroup.groupName);
            ((ButtonWidgetData)groupButtonsByGroup[renamedGroup].Reference).Label = FormatGroupName(renamedGroup);
            ((ButtonWidgetData)playerGroupButtonsByGroup[renamedGroup].Reference).Label = renamedGroup == editingPlayerData.permissionGroup
                ? $"<b>{renamedGroup.groupName}</b>"
                : renamedGroup.groupName;
        }

        [PermissionsEvent(PermissionsEventType.OnPlayerPermissionGroupChanged)]
        public void OnPlayerPermissionGroupChanged()
        {
            if (!isInitialized || permissionManager.PlayerDataForEvent != editingPlayerData)
                return;
            UpdateSelectedPlayerGroup();
        }

        [PermissionsEvent(PermissionsEventType.OnPermissionValueChanged)]
        public void OnPermissionValueChanged()
        {
            if (!isInitialized || permissionManager.ChangedPermissionGroup != editingPermissionGroup)
                return;
            int index = permissionManager.ChangedPermission.index;
            permissionToggles[index].SetValueWithoutNotify(editingPermissionGroup.permissionValues[index]);
        }

        [LockstepEvent(LockstepEventType.OnImportFinishingUp)]
        public void OnImportFinishingUp()
        {
            if (editingPermissionGroup == null || editingPermissionGroup.isDeleted)
                editingPermissionGroup = permissionManager.DefaultPermissionGroup;
            if (editingPlayerData == null || !editingPlayerData.CheckLiveliness())
                editingPlayerData = playerDataManager.GetPlayerDataForPlayerId<PermissionsPlayerData>(
                    nameof(PermissionsPlayerData),
                    (uint)Networking.LocalPlayer.playerId);
            RedrawGroupsLists();
            DrawPlayerList();
        }

        [PlayerDataEvent(PlayerDataEventType.OnPlayerDataCreated)]
        public void OnPlayerDataCreated()
        {
            if (!isInitialized)
                return;
            // Who cares about performance!
            DrawPlayerList();
        }

        [PlayerDataEvent(PlayerDataEventType.OnPlayerDataDeleted)]
        public void OnPlayerDataDeleted()
        {
            if (!isInitialized)
                return;
            if (playerDataManager.PlayerDataForEvent == editingPlayerData.core)
                editingPlayerData = playerDataManager.GetPlayerDataForPlayerId<PermissionsPlayerData>(
                    nameof(PermissionsPlayerData),
                    (uint)Networking.LocalPlayer.playerId);
            // Who cares about performance!
            DrawPlayerList();
        }
    }
}
