using UdonSharp;

namespace JanSharp
{
    public abstract class PermissionResolver : UdonSharpBehaviour
    {
        public abstract void Resolve();
    }
}
