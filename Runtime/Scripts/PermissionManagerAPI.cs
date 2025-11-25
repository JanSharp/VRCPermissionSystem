
namespace JanSharp
{
    public enum PermissionsEventType
    {
        /// <summary>
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
        public abstract PermissionGroup CreatePermissionGroupInGS(string groupName);

        public abstract void SendSetPlayerPermissionGroupIA(CorePlayerData corePlayerData, PermissionGroup group);

        public abstract PermissionsPlayerData PlayerDataForEvent { get; }
        public abstract PermissionGroup PreviousPlayerPermissionGroup { get; }
    }
}
