using System.Collections.Generic;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

namespace JanSharp
{
    [InitializeOnLoad]
    public static class ShowObjectByPermissionOnBuild
    {
        static ShowObjectByPermissionOnBuild()
        {
            OnBuildUtil.RegisterTypeCumulative<ShowObjectByPermission>(OnBuildCumulative);
        }

        private static bool OnBuildCumulative(IEnumerable<ShowObjectByPermission> showObjectByPermissions)
        {
            bool result = true;
            foreach (var showObjectByPermission in showObjectByPermissions)
                if (!OnBuild(showObjectByPermission))
                    result = false;
            return result;
        }

        private static bool OnBuild(ShowObjectByPermission showObjectByPermission)
        {
            bool result = true;
            PermissionDefinition[] permissionDefs = new PermissionDefinition[showObjectByPermission.AssetGuids.Length];
            for (int i = 0; i < showObjectByPermission.AssetGuids.Length; i++)
            {
                string guid = showObjectByPermission.AssetGuids[i];
                permissionDefs[i] = PermissionDefinitionOnBuild.RegisterPermissionDefDependency(showObjectByPermission, guid);
                if (permissionDefs[i] != null)
                    continue;
                result = false;
                Debug.LogError($"[PermissionSystem] A Show Object By Permission component "
                    + $"({showObjectByPermission.name}) is trying to use a Permission Definition Asset "
                    + $"in its Conditions which does not exist.", showObjectByPermission);
            }
            SerializedObject so = new(showObjectByPermission);
            EditorUtil.SetArrayProperty(
                so.FindProperty("permissionDefs"),
                permissionDefs,
                (p, v) => p.objectReferenceValue = v);
            so.ApplyModifiedProperties();
            return result;
        }
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(ShowObjectByPermission))]
    public class ShowObjectByPermissionEditor : Editor
    {
        private SerializedProperty whenConditionsAreMetProp;
        private PermissionConditionsList conditionsList;

        public void OnEnable()
        {
            whenConditionsAreMetProp = serializedObject.FindProperty("whenConditionsAreMet");

            conditionsList = new PermissionConditionsList(
                targets: targets,
                header: new GUIContent("Conditions"),
                logicalAndsFieldName: "logicalAnds",
                assetGuidsFieldName: "assetGuids",
                getLogicalAnds: t => ((ShowObjectByPermission)t).logicalAnds,
                getAssetGuids: t => ((ShowObjectByPermission)t).AssetGuids);
        }

        public void OnDisable()
        {
            conditionsList.OnDisable();
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(targets))
                return;

            serializedObject.Update();
            EditorGUILayout.PropertyField(whenConditionsAreMetProp);
            serializedObject.ApplyModifiedProperties();

            conditionsList.Draw();
        }
    }
}
