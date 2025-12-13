using UdonSharp;

namespace JanSharp.Internal
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PermissionManagerInternalHelper : UdonSharpBehaviour
    {
        public PermissionManager permissionManager;

        [LockstepEvent(LockstepEventType.OnImportFinished, Order = 10000)]
        public void OnImportFinished()
        {
            permissionManager.OnLateImportFinished();
        }
    }
}
