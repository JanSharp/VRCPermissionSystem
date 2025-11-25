
namespace JanSharp
{
    public enum PermissionsEventType
    {
        /// <summary>
        /// <para>Use <see cref="PermissionManagerAPI.CreatedPermissionGroup"/> to get the newly created
        /// permission group.</para>
        /// <para>Use <see cref="PermissionManagerAPI.CreatedPermissionGroupDuplicationSource"/> to get the
        /// permission group which was duplicated to create the new group.</para>
        /// <para>Game state safe.</para>
        /// </summary>
        OnPermissionGroupDuplicated,
        /// <summary>
        /// <para>Use <see cref="PermissionManagerAPI.PlayerDataForEvent"/> to get the player who's
        /// <see cref="PermissionsPlayerData.permissionGroup"/> has changed.</para>
        /// <para>Use <see cref="PermissionManagerAPI.PreviousPlayerPermissionGroup"/> to get the permission
        /// group the given player had before the change.</para>
        /// <para>Game state safe.</para>
        /// </summary>
        OnPlayerPermissionGroupChanged,
    }

    [System.AttributeUsage(System.AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class PermissionsEventAttribute : CustomRaisedEventBaseAttribute
    {
        /// <summary>
        /// <para>The method this attribute gets applied to must be public.</para>
        /// <para>The name of the function this attribute is applied to must have the exact same name as the
        /// name of the <paramref name="eventType"/>.</para>
        /// <para>Event registration is performed at OnBuild, which is to say that scripts with these kinds of
        /// event handlers must exist in the scene at build time, any runtime instantiated objects with these
        /// scripts on them will not receive these events.</para>
        /// <para>Disabled scripts still receive events.</para>
        /// </summary>
        /// <param name="eventType">The event to register this function as a listener to.</param>
        public PermissionsEventAttribute(PermissionsEventType eventType)
            : base((int)eventType)
        { }
    }

    [SingletonScript("fe2230975d59bf380865519bc62b6a3a")] // Runtime/Prefabs/PermissionManager.prefab
    public abstract class PermissionManagerAPI : LockstepGameState
    {
        public abstract PermissionGroup DefaultPermissionGroup { get; }
        public abstract int PermissionGroupsCount { get; }
        public abstract PermissionDefinition[] PermissionDefinitions { get; }
        public abstract PermissionGroup GetPermissionGroup(int index);

        /// <summary>
        /// </summary>
        /// <param name="groupName"></param>
        /// <returns><see langword="null"/> if the there is no group with the given
        /// <paramref name="groupName"/>.</returns>
        public abstract PermissionGroup GetPermissionGroup(string groupName);

        public abstract void SendDuplicatePermissionGroupIA(string groupName, PermissionGroup toDuplicate);
        /// <summary>
        /// <para>Raises <see cref="PermissionsEventType.OnPermissionGroupDuplicated"/>, so long as
        /// <paramref name="groupName"/> is not already in use.</para>
        /// </summary>
        /// <param name="groupName">Leading and trailing spaces get trimmed. Must not be an empty
        /// string. When invalid, <see langword="null"/> is returned.</param>
        /// <param name="toDuplicate"></param>
        /// <returns><see langword="null"/> if the given <paramref name="groupName"/> is already in
        /// use.</returns>
        public abstract PermissionGroup DuplicatePermissionGroupInGS(string groupName, PermissionGroup toDuplicate);

        public abstract void SendSetPlayerPermissionGroupIA(CorePlayerData corePlayerData, PermissionGroup group);
        /// <summary>
        /// <para>Raises <see cref="PermissionsEventType.OnPlayerPermissionGroupChanged"/>, so long as the
        /// given player wasn't already in the given group.</para>
        /// </summary>
        /// <param name="corePlayerData"></param>
        /// <param name="group"></param>
        public abstract void SetPlayerPermissionGroupInGS(CorePlayerData corePlayerData, PermissionGroup group);

        /// <summary>
        /// <para>Usable inside of <see cref="PermissionsEventType.OnPermissionGroupDuplicated"/>.</para>
        /// <para>Game state safe.</para>
        /// </summary>
        public abstract PermissionGroup CreatedPermissionGroup { get; }
        /// <summary>
        /// <para>Usable inside of <see cref="PermissionsEventType.OnPermissionGroupDuplicated"/>.</para>
        /// <para>Game state safe.</para>
        /// </summary>
        public abstract PermissionGroup CreatedPermissionGroupDuplicationSource { get; }

        /// <summary>
        /// <para>Usable inside of <see cref="PermissionsEventType.OnPlayerPermissionGroupChanged"/>.</para>
        /// <para>Game state safe.</para>
        /// </summary>
        public abstract PermissionsPlayerData PlayerDataForEvent { get; }
        /// <summary>
        /// <para>Usable inside of <see cref="PermissionsEventType.OnPlayerPermissionGroupChanged"/>.</para>
        /// <para>Game state safe.</para>
        /// </summary>
        public abstract PermissionGroup PreviousPlayerPermissionGroup { get; }
    }
}
