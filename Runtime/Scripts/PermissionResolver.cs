using UdonSharp;

namespace JanSharp
{
    public abstract class PermissionResolver : UdonSharpBehaviour
    {
        /// <summary>
        /// <para>A place to run any logic that needs to run when an object is instantiated rather than
        /// existing at scene load already.</para>
        /// <para>Any system instantiating objects must call this function on every instantiated
        /// object.</para>
        /// <para>The <see cref="PermissionResolverInstantiationHelper"/> exists to help with this process,
        /// as well as automating it entirely in cases where the helper is guaranteed to be active in the
        /// hierarchy for the newly created object.</para>
        /// <para>The reason why we cannot rely on any Unity event on the PermissionResolver itself is because
        /// if it is inactive in the hierarchy well there simply are no Unity events to listen to.</para>
        /// <para>And it being inactive is effectively guaranteed to happen for PermissionResolvers such as
        /// the <see cref="ShowObjectByPermission"/> if the object being duplicated is itself an object in the
        /// scene (not a prefab asset reference), because that PermissionResolver deactivates its own object
        /// based on permission conditions. And there are cases, such as UI, where not using prefab assets is
        /// actually preferable (or in the case of input fields literally required, because otherwise VRChat's
        /// input field popup doesn't pop up, see
        /// https://feedback.vrchat.com/bug-reports/p/instantiated-input-fields-dont-open-the-vrc-keyboard).</para>
        /// <para>See <see cref="ShowObjectByPermission"/> as an example.</para>
        /// <para>Not game state safe.</para>
        /// </summary>
        public abstract void InitializeInstantiated();
        /// <summary>
        /// <para>Not game state safe.</para>
        /// </summary>
        public abstract void Resolve();
    }
}
