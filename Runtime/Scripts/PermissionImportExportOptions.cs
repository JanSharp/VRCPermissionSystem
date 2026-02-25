using UdonSharp;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PermissionImportExportOptions : LockstepGameStateOptionsData
    {
        public override bool SupportsImportExport => true;
        public override uint DataVersion => 0u;
        public override uint LowestSupportedDataVersion => 0u;

        [System.NonSerialized] public bool includePermissionGroups = true;
        [System.NonSerialized] public bool includePlayerPermissionGroups = true;

        public override LockstepGameStateOptionsData Clone()
        {
            var clone = WannaBeClasses.New<PermissionImportExportOptions>(nameof(PermissionImportExportOptions));
            clone.includePermissionGroups = includePermissionGroups;
            clone.includePlayerPermissionGroups = includePlayerPermissionGroups;
            return clone;
        }

        public override void Serialize(bool isExport)
        {
            lockstep.WriteFlags(
                includePermissionGroups,
                includePlayerPermissionGroups);
        }

        public override void Deserialize(bool isImport, uint importedDataVersion)
        {
            lockstep.ReadFlags(
                out includePermissionGroups,
                out includePlayerPermissionGroups);
        }
    }
}
