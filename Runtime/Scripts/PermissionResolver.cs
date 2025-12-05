using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    public abstract class PermissionResolver : UdonSharpBehaviour
    {
        /// <summary>
        /// <para>A place to run any logic that needs to run when an object is instantiated rathe than
        /// existing at scene load already.</para>
        /// <para>Any system instantiating objects must
        /// <see cref="GameObject.GetComponentsInChildren{T}(bool)"/> with <c>includeInactive</c>
        /// <see langword="true"/> and then call this function on all these components.</para>
        /// <para>This sucks. I know. Thank Unity for not giving us any event when an inactive object got
        /// instantiated. Which is bound to happen if the instantiated object isn't using a prefab, but rather
        /// a reference to an object in the scene already, in which case the permission resolver may
        /// deactivate objects (including itself) due to the state of permissions.</para>
        /// <para>Listening to <c>Start</c> or <c>Awake</c> anyway is likely a good idea anyway. Use
        /// <see cref="PermissionManagerAPI.ExistedAtSceneLoad(PermissionResolver)"/> to check if the resolver
        /// already existed at scene load vs if it got instantiated, which could require different (likely
        /// less) handling than instantiated ones.</para>
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
