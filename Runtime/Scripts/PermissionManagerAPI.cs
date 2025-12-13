
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
        /// <para>Use <see cref="PermissionManagerAPI.DeletedPermissionGroup"/> to get the permission group
        /// which has been deleted.</para>
        /// <para>Game state safe.</para>
        /// </summary>
        OnPermissionGroupDeleted,
        /// <summary>
        /// <para>Use <see cref="PermissionManagerAPI.RenamedPermissionGroup"/> to get the permission group
        /// which has been renamed.</para>
        /// <para>Use <see cref="PermissionManagerAPI.PreviousPermissionGroupName"/> to get the group name the
        /// permission group had before the rename.</para>
        /// <para>Game state safe.</para>
        /// </summary>
        OnPermissionGroupRenamed,
        /// <summary>
        /// <para>Use <see cref="PermissionManagerAPI.PlayerDataForEvent"/> to get the player who's
        /// <see cref="PermissionsPlayerData.permissionGroup"/> has changed.</para>
        /// <para>Use <see cref="PermissionManagerAPI.PreviousPlayerPermissionGroup"/> to get the permission
        /// group the given player had before the change.</para>
        /// <para>Game state safe.</para>
        /// </summary>
        OnPlayerPermissionGroupChanged,
        /// <summary>
        /// <para>Use <see cref="PermissionManagerAPI.ChangedPermissionGroup"/> to get the permission group
        /// for which a <see cref="PermissionGroup.permissionValues"/> has been changed.</para>
        /// <para>Use <see cref="PermissionManagerAPI.ChangedPermission"/> to get the permission which has
        /// been changed.</para>
        /// <para>Only gets raised if the value actually changed, and since it is either
        /// <see langword="true"/> or <see langword="false"/> the previous value can be determined quite
        /// easily. It is the inverse of the current value.</para>
        /// <para>Game state safe.</para>
        /// </summary>
        OnPermissionValueChanged,
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
        public abstract PermissionDefinition[] PermissionDefinitions { get; }
        public abstract PermissionGroup[] PermissionGroups { get; }
        public abstract PermissionGroup GetPermissionGroup(int index);
        public abstract int PermissionGroupsCount { get; }

        /// <summary>
        /// </summary>
        /// <param name="groupName"></param>
        /// <returns><see langword="null"/> if the there is no group with the given
        /// <paramref name="groupName"/>.</returns>
        public abstract PermissionGroup GetPermissionGroup(string groupName);
        public abstract bool PlayerHasPermission(CorePlayerData corePlayerData, PermissionDefinition permissionDef);

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

        public abstract void SendDeletePermissionGroupIA(PermissionGroup group, PermissionGroup groupToMovePlayersTo);
        /// <summary>
        /// <para>Does nothing if <see cref="PermissionGroup.isDefault"/> is <see langword="true"/> for
        /// <paramref name="group"/> or <see cref="PermissionGroup.isDeleted"/> is <see langword="true"/> for
        /// <paramref name="group"/> or <paramref name="group"/> is equal to
        /// <paramref name="groupToMovePlayersTo"/> or <see cref="PermissionGroup.isDeleted"/> is
        /// <see langword="true"/> for <paramref name="groupToMovePlayersTo"/>.</para>
        /// <para>Every player that was part of the given <paramref name="group"/> gets changed to
        /// <paramref name="groupToMovePlayersTo"/>, and raises
        /// <see cref="PermissionsEventType.OnPlayerPermissionGroupChanged"/>.</para>
        /// <para>Raises <see cref="PermissionsEventType.OnPermissionGroupDeleted"/> afterwards.</para>
        /// <para>Sets <see cref="PermissionGroup.isDeleted"/> to <see langword="true"/> before raising any
        /// events.</para>
        /// <para>However only removes <paramref name="group"/> from internal data structures at the very end,
        /// right before raising <see cref="PermissionsEventType.OnPermissionGroupDeleted"/>.</para>
        /// </summary>
        /// <param name="group"></param>
        /// <param name="groupToMovePlayersTo"></param>
        public abstract void DeletePermissionGroupInGS(PermissionGroup group, PermissionGroup groupToMovePlayersTo);

        public abstract void SendRenamePermissionGroupIA(PermissionGroup group, string newGroupName);
        public abstract void RenamePermissionGroupInGS(PermissionGroup group, string newGroupName);

        public abstract void SendSetPlayerPermissionGroupIA(CorePlayerData corePlayerData, PermissionGroup group);
        /// <summary>
        /// <para>Raises <see cref="PermissionsEventType.OnPlayerPermissionGroupChanged"/>, so long as the
        /// given player wasn't already in the given group.</para>
        /// </summary>
        /// <param name="corePlayerData"></param>
        /// <param name="group"></param>
        public abstract void SetPlayerPermissionGroupInGS(CorePlayerData corePlayerData, PermissionGroup group);

        public abstract void SendSetPermissionValueIA(PermissionGroup group, string permissionInternalName, bool value);
        public abstract void SetPermissionValueInGS(PermissionGroup group, string permissionInternalName, bool value);

        public abstract bool ExistedAtSceneLoad(PermissionResolver resolver);
        public abstract void RegisterResolvers(PermissionResolver resolver, PermissionDefinition[] permissionDefs);
        public abstract void RegisterResolvers(PermissionResolver resolver, PermissionDefinition[] permissionDefs, int startIndex);
        public abstract void RegisterResolvers(PermissionResolver resolver, PermissionDefinition[] permissionDefs, int startIndex, int count);
        public abstract void DeregisterResolvers(PermissionResolver resolver, PermissionDefinition[] permissionDefs);
        public abstract void DeregisterResolvers(PermissionResolver resolver, PermissionDefinition[] permissionDefs, int startIndex);
        public abstract void DeregisterResolvers(PermissionResolver resolver, PermissionDefinition[] permissionDefs, int startIndex, int count);

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
        /// <para>Usable inside of <see cref="PermissionsEventType.OnPermissionGroupDeleted"/>.</para>
        /// <para>Game state safe.</para>
        /// </summary>
        public abstract PermissionGroup DeletedPermissionGroup { get; }

        /// <summary>
        /// <para>Usable inside of <see cref="PermissionsEventType.OnPermissionGroupRenamed"/>.</para>
        /// <para>Game state safe.</para>
        /// </summary>
        public abstract PermissionGroup RenamedPermissionGroup { get; }
        /// <summary>
        /// <para>Usable inside of <see cref="PermissionsEventType.OnPermissionGroupRenamed"/>.</para>
        /// <para>Game state safe.</para>
        /// </summary>
        public abstract string PreviousPermissionGroupName { get; }

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

        /// <summary>
        /// <para>Usable inside of <see cref="PermissionsEventType.OnPermissionValueChanged"/>.</para>
        /// <para>Game state safe.</para>
        /// </summary>
        public abstract PermissionGroup ChangedPermissionGroup { get; }
        /// <summary>
        /// <para>Usable inside of <see cref="PermissionsEventType.OnPermissionValueChanged"/>.</para>
        /// <para>Game state safe.</para>
        /// </summary>
        public abstract PermissionDefinition ChangedPermission { get; }
    }
}
