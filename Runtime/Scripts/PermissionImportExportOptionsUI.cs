using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PermissionImportExportOptionsUI : LockstepGameStateOptionsUI
    {
        [HideInInspector][SerializeField][SingletonReference] private PlayerDataExportUIAPI playerDataExportUI;
        [HideInInspector][SerializeField][SingletonReference] private PlayerDataImportUIAPI playerDataImportUI;

        public override string OptionsClassName => nameof(PermissionImportExportOptions);

        [SerializeField] private bool isImportUI;

        private PermissionImportExportOptions currentOptions;
        private PermissionImportExportOptions optionsToValidate;

        private ToggleFieldWidgetData includePermissionGroupsToggle;
        private ToggleFieldWidgetData includePlayerPermissionGroupsToggle;

        protected override LockstepGameStateOptionsData NewOptionsImpl()
        {
            return wannaBeClasses.New<PermissionImportExportOptions>(nameof(PermissionImportExportOptions));
        }

        protected override void ValidateOptionsImpl()
        {
        }

        protected override void UpdateCurrentOptionsFromWidgetsImpl()
        {
            if (includePermissionGroupsToggle.Interactable)
                currentOptions.includePermissionGroups = includePermissionGroupsToggle.Value;
            if (includePlayerPermissionGroupsToggle.Interactable)
                currentOptions.includePlayerPermissionGroups = includePlayerPermissionGroupsToggle.Value;
        }

        protected override void InitWidgetData()
        {
        }

        private void LazyInitWidgetData()
        {
            if (includePermissionGroupsToggle != null)
                return;
            includePermissionGroupsToggle = widgetManager.NewToggleField("Permission Groups", false);
            includePlayerPermissionGroupsToggle = widgetManager.NewToggleField("Permission Group", false);
        }

        protected override void OnOptionsEditorShow(LockstepOptionsEditorUI ui, uint importedDataVersion)
        {
            LazyInitWidgetData();
            if (isImportUI)
            {
                var optionsFromExport = (PermissionImportExportOptions)lockstep.ReadCustomClass(nameof(PermissionImportExportOptions), isImport: true);
                includePermissionGroupsToggle.Interactable = optionsFromExport.includePermissionGroups;
                includePlayerPermissionGroupsToggle.Interactable = optionsFromExport.includePlayerPermissionGroups;
                optionsFromExport.Delete();
            }
            includePermissionGroupsToggle.SetValueWithoutNotify(includePermissionGroupsToggle.Interactable && currentOptions.includePermissionGroups);
            includePlayerPermissionGroupsToggle.SetValueWithoutNotify(includePlayerPermissionGroupsToggle.Interactable && currentOptions.includePlayerPermissionGroups);
            ui.General.AddChildDynamic(includePermissionGroupsToggle);
            AddPlayerDataToggle(includePlayerPermissionGroupsToggle);
        }

        private void AddPlayerDataToggle(ToggleFieldWidgetData toggle)
        {
            if (isImportUI)
                playerDataImportUI.AddPlayerDataOptionToggle(toggle);
            else
                playerDataExportUI.AddPlayerDataOptionToggle(toggle);
        }

        protected override void OnOptionsEditorHide(LockstepOptionsEditorUI ui)
        {
        }
    }
}
