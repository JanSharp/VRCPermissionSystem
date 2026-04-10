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
        /// <summary>
        /// <para>For simplicity and more consistency it is recommended to use
        /// <see cref="PermissionGroupExtensions.CheckIsDeleted(PermissionGroup)"/> instead of reading this
        /// value directly.</para>
        /// </summary>
        [System.NonSerialized] public bool isDeleted;

        /// <summary>
        /// <para>Weak wanna be class references.</para>
        /// </summary>
        [System.NonSerialized] public PermissionsPlayerData[] playersInGroup = new PermissionsPlayerData[ArrList.MinCapacity];
        [System.NonSerialized] public int playersInGroupCount = 0;
        /// <summary>
        /// <para>Weak wanna be class references.</para>
        /// </summary>
        [System.NonSerialized] public PermissionsPlayerData[] onlinePlayersInGroup = new PermissionsPlayerData[ArrList.MinCapacity];
        [System.NonSerialized] public int onlinePlayersInGroupCount = 0;
        #endregion
    }

    public static class PermissionGroupExtensions
    {
        /// <summary>
        /// <para>Can be called on <see langword="null"/> instances.</para>
        /// </summary>
        /// <param name="permissionGroup"></param>
        /// <returns><see langword="true"/> if the instance is either not lively or
        /// <see cref="PermissionGroup.isDeleted"/> is <see langword="true"/>.</returns>
        public static bool CheckIsDeleted(this PermissionGroup permissionGroup)
        {
            return !permissionGroup.CheckLiveliness() || permissionGroup.isDeleted;
        }
    }
}
