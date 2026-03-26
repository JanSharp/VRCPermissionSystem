namespace JanSharp
{
    /// <summary>
    /// <para>Unlike <see cref="PermissionResolver"/> these must not be instantiated at runtime and must not
    /// be destroyed at runtime either. This is to ensure determinism.</para>
    /// <para>These run before <see cref="PermissionResolver"/>, in other words game state gets updated before
    /// latency state.</para>
    /// </summary>
    public abstract class PermissionResolverForGameState : Internal.PermissionResolverBase
    {
        /// <summary>
        /// <para>The permission system calls this in <see cref="PlayerDataEventType.OnPlayerDataCreated"/>
        /// with an <c>Order</c> of <c>0</c> and whenever the permission group of a player changes.</para>
        /// <para>For imports the permission systems calls this in
        /// <see cref="LockstepEventType.OnImportFinishingUp"/> with an <c>Order</c> of <c>10000</c>. It does
        /// this entirely unconditionally, even if the permission system itself did not get imported,
        /// resolvers always run for imports.</para>
        /// <para>Game state safe.</para>
        /// </summary>
        public abstract void ResolveAll(PermissionsPlayerData player);
        /// <summary>
        /// <para>Called specifically when the value of one <see cref="PermissionDefinition"/> changes for the
        /// <see cref="PermissionGroup"/> the given <paramref name="player"/> is apart of.</para>
        /// <para>Not called during initialization, changing of permission groups nor imports.</para>
        /// <para>Game state safe.</para>
        /// </summary>
        /// <param name="permissionDef"></param>
        public virtual void Resolve(PermissionsPlayerData player, PermissionDefinition permissionDef) => ResolveAll(player);
    }
}
