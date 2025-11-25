using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;

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

        private StringFieldWidgetData groupNameField;
        private ToggleFieldWidgetData[] permissionToggles;

        /// <summary>Weak WannaBeClass reference.</summary>
        private PermissionGroup editingPermissionGroup;
        private ButtonWidgetData editingPermissionGroupButton;

        /// <summary>Weak WannaBeClass reference.</summary>
        private PermissionsPlayerData editingPlayerData;
        private ButtonWidgetData editingPlayerButton;

        /// <summary>
        /// <para><see cref="PermissionGroup"/> group => <see cref="ButtonWidgetData"/> playerGroupButton</para>
        /// </summary>
        private DataDictionary playerGroupButtonsByGroup = new DataDictionary();
        private ButtonWidgetData selectedPlayerGroupButton;

        [LockstepEvent(LockstepEventType.OnInit)]
        public void OnInit()
        {
            permissionManager.CreatePermissionGroupInGS("Test");
            PermissionGroup foo = permissionManager.CreatePermissionGroupInGS("Foo");
            PermissionGroup bar = permissionManager.CreatePermissionGroupInGS("Bar");

            foo.permissionValues[1] = false;
            foo.permissionValues[2] = false;

            bar.permissionValues[2] = false;

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
            PopulatePlayerList();
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
                    .SetCustomData(nameof(affectedPermissionName), def.internalName)
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

        private void InitGroupsLists()
        {
            // TODO: Add duplicate and delete buttons as a header.
            int count = permissionManager.PermissionGroupsCount;
            ButtonWidgetData[] groupButtons = new ButtonWidgetData[count];
            ButtonWidgetData[] playerGroupButtons = new ButtonWidgetData[count];
            for (int i = 0; i < count; i++)
            {
                PermissionGroup group = permissionManager.GetPermissionGroup(i);
                groupButtons[i] = (ButtonWidgetData)widgets.NewButton(FormatGroupName(group))
                    .SetCustomData(nameof(clickedPermissionGroup), group)
                    .SetListener(this, nameof(OnPermissionGroupButtonClick));
                playerGroupButtons[i] = (ButtonWidgetData)widgets.NewButton(group.groupName)
                    .SetCustomData(nameof(clickedPlayerGroup), group)
                    .SetListener(this, nameof(OnPlayerGroupButtonClick));
                playerGroupButtonsByGroup.Add(group, playerGroupButtons[i]);
            }
            groupsEditor.Draw(groupButtons);
            playerGroupSelectionEditor.Draw(playerGroupButtons);

            if (editingPermissionGroup != null)
                return;
            SetEditingPermissionGroup(permissionManager.DefaultPermissionGroup, groupButtons[0]);
        }

        private void SetEditingPermissionGroup(PermissionGroup group, ButtonWidgetData button)
        {
            PermissionGroup pervGroup = editingPermissionGroup;
            editingPermissionGroup = group; // Set before calling FormatGroupName.
            if (pervGroup != null)
                editingPermissionGroupButton.Label = FormatGroupName(pervGroup);
            editingPermissionGroupButton = button;
            button.Label = FormatGroupName(group);

            groupNameField.Value = group.groupName;
            groupNameField.Interactable = !group.isDefault;

            int defsCount = permissionManager.PermissionDefinitions.Length;
            bool[] groupValues = editingPermissionGroup.permissionValues;
            for (int i = 0; i < defsCount; i++)
                permissionToggles[i].SetValueWithoutNotify(groupValues[i]);
        }

        private void PopulatePlayerList()
        {
            PermissionsPlayerData[] allPlayerData = playerDataManager.GetAllPlayerData<PermissionsPlayerData>(nameof(PermissionsPlayerData));
            int count = allPlayerData.Length;
            ButtonWidgetData[] playerButtons = new ButtonWidgetData[count];
            for (int i = 0; i < count; i++)
                playerButtons[i] = (ButtonWidgetData)widgets.NewButton(allPlayerData[i].core.displayName)
                    .SetCustomData(nameof(clickedPlayerData), allPlayerData[i])
                    .SetListener(this, nameof(OnPlayerButtonClick));
            playersEditor.Draw(playerButtons);

            if (editingPlayerButton != null)
                return;
            SetEditingPlayer(allPlayerData[0], playerButtons[0]);
        }

        private void SetEditingPlayer(PermissionsPlayerData playerData, ButtonWidgetData button)
        {
            if (editingPlayerButton != null)
                editingPlayerButton.Label = editingPlayerData.core.displayName;

            editingPlayerData = playerData;
            editingPlayerButton = button;

            button.Label = $"<b>{playerData.core.displayName}</b>";

            UpdateSelectedPlayerGroup();
        }

        private void UpdateSelectedPlayerGroup()
        {
            if (selectedPlayerGroupButton != null)
                selectedPlayerGroupButton.Label = ((PermissionGroup)selectedPlayerGroupButton.customData).groupName;
            selectedPlayerGroupButton = (ButtonWidgetData)playerGroupButtonsByGroup[editingPlayerData.permissionGroup].Reference;
            selectedPlayerGroupButton.Label = $"<b>{editingPlayerData.permissionGroup.groupName}</b>";
        }

        public void OnGroupNameValueChanged()
        {
            // TODO: implement
        }

        private string affectedPermissionName;
        public void OnPermissionToggleValueChanged()
        {
            // TODO: implement
        }

        private PermissionGroup clickedPermissionGroup;
        public void OnPermissionGroupButtonClick()
        {
            SetEditingPermissionGroup(clickedPermissionGroup, groupsEditor.GetSendingButton());
        }

        private PermissionGroup clickedPlayerGroup;
        public void OnPlayerGroupButtonClick()
        {
            permissionManager.SendSetPlayerPermissionGroupIA(editingPlayerData.core, clickedPlayerGroup);
        }

        private PermissionsPlayerData clickedPlayerData;
        public void OnPlayerButtonClick()
        {
            SetEditingPlayer(clickedPlayerData, playersEditor.GetSendingButton());
        }

        [PermissionsEvent(PermissionsEventType.OnPlayerPermissionGroupChanged)]
        public void OnPlayerPermissionGroupChanged()
        {
            if (permissionManager.PlayerDataForEvent != editingPlayerData)
                return;
            UpdateSelectedPlayerGroup();
        }
    }
}
