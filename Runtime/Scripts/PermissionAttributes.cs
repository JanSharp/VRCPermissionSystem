namespace JanSharp
{
    /// <summary>
    /// <inheritdoc cref="PermissionDefinitionReferenceAttribute(string)"/>
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class PermissionDefinitionReferenceAttribute : UnityEngine.PropertyAttribute
    {
        // See the attribute guidelines at
        //  http://go.microsoft.com/fwlink/?LinkId=85236

        readonly string permissionDefinitionFieldName;
        public string PermissionDefinitionFieldName => permissionDefinitionFieldName;

        public bool Optional { get; set; } = false;

        /// <summary>
        /// <para>This attribute must be applied to a <see cref="string"/> field which is serialized by unity
        /// (so public or using <see cref="UnityEngine.SerializeField"/>).</para>
        /// <para>Use this to create a reference or dependency (depending on if <see cref="Optional"/> is set)
        /// on a <see cref="PermissionDefinitionAsset"/>. Yes asset, this is a custom property drawer
        /// resolving a guid which is saved in the field this attribute is applied to to an asset.</para>
        /// <para>If the class which defines the field this attribute is applied to derives from the
        /// <see cref="PermissionResolver"/> class then it is automatically registered as a resolver for
        /// the associated <see cref="PermissionDefinition"/> of the chosen
        /// <see cref="PermissionDefinitionAsset"/>. This happens when entering play mode or building the
        /// world.</para>
        /// <para>Also at that time (build time) the associated <see cref="PermissionDefinition"/> field gets
        /// populated, making it available for use at runtime.</para>
        /// </summary>
        /// <param name="permissionDefinitionFieldName">The <c>nameof()</c> a
        /// <see cref="PermissionDefinition"/> field. Said field must also be serialized by unity. Likely best
        /// to also use the <see cref="UnityEngine.HideInInspector"/> attribute since that field gets auto
        /// populated.</param>
        public PermissionDefinitionReferenceAttribute(string permissionDefinitionFieldName)
        {
            this.permissionDefinitionFieldName = permissionDefinitionFieldName;
        }
    }
}
