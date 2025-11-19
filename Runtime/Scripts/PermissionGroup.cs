using UdonSharp;
using UnityEngine;

namespace JanSharp
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PermissionGroup : SerializableWannaBeClass
    {
        public override bool SupportsImportExport => true;
        /// <summary>
        /// <para>Does not use these data versions. Uses the ones from the PermissionManager.</para>
        /// </summary>
        public override uint DataVersion => 0u;
        public override uint LowestSupportedDataVersion => 0u;

        [HideInInspector][SerializeField][SingletonReference] private PermissionManager permissionManager;

        public const string DefaultGroupName = "Default";

        #region GameState
        [System.NonSerialized] public bool isDefault;
        [System.NonSerialized] public uint id;
        [System.NonSerialized] public string groupName;
        [System.NonSerialized] public bool[] permissionValues;
        #endregion

        public override void Serialize(bool isExport)
        {
            lockstep.WriteString(groupName);
            lockstep.WriteFlags(permissionValues);
        }

        public override void Deserialize(bool isImport, uint importedDataVersion)
        {
            if (isImport)
            {
                // TODO: Scream!
                return;
            }
            groupName = lockstep.ReadString();
            isDefault = groupName == DefaultGroupName;
            permissionValues = new bool[permissionManager.PermissionDefinitionsCount];
            lockstep.ReadFlags(permissionValues);
        }
    }
}
