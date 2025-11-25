using UdonSharp;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PermissionGroup : WannaBeClass
    {
        public const string DefaultGroupName = "Default";

        #region GameState
        [System.NonSerialized] public bool isDefault;
        [System.NonSerialized] public uint id;
        [System.NonSerialized] public string groupName;
        [System.NonSerialized] public bool[] permissionValues;
        [System.NonSerialized] public bool isDeleted;
        #endregion
    }
}
