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
                if (!PermissionSystemEditorUtil.OnPermissionConditionsListBuild(
                    showObjectByPermission,
                    showObjectByPermission.AssetGuids,
                    permissionDefsFieldName: "permissionDefs",
                    conditionsHeaderName: "Conditions"))
                {
                    result = false;
                }
            return result;
        }
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(ShowObjectByPermission))]
    public class ShowObjectByPermissionEditor : Editor
    {
        private SerializedProperty whenConditionsAreMetProp;
        private SerializedProperty showWhileLoadingProp;
        private PermissionConditionsList conditionsList;

        public void OnEnable()
        {
            whenConditionsAreMetProp = serializedObject.FindProperty("whenConditionsAreMet");
            showWhileLoadingProp = serializedObject.FindProperty("showWhileLoading");

            conditionsList = new PermissionConditionsList(
                targets: targets,
                header: new GUIContent("Conditions", "Empty Conditions are treated as a match"),
                logicalAndsFieldName: "logicalAnds",
                invertsFieldName: "inverts",
                assetGuidsFieldName: "assetGuids",
                getLogicalAnds: t => ((ShowObjectByPermission)t).logicalAnds,
                getInverts: t => ((ShowObjectByPermission)t).inverts,
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
            EditorGUILayout.PropertyField(showWhileLoadingProp);
            serializedObject.ApplyModifiedProperties();

            conditionsList.Draw();
        }
    }
}
