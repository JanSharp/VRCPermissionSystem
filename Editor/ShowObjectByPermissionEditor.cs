using System.Collections.Generic;
using UdonSharpEditor;
using UnityEditor;
using UnityEditorInternal;
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
            PermissionDefinition[] permissionDefs = new PermissionDefinition[showObjectByPermission.assetGuids.Length];
            for (int i = 0; i < showObjectByPermission.assetGuids.Length; i++)
            {
                string guid = showObjectByPermission.assetGuids[i];
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

    internal class ShowObjectByPermissionDummy : ScriptableObject
    {
        public List<ShowObjectByPermissionDummyEntry> entries = new();

        public void PopulateFromReal(ShowObjectByPermission real)
        {
            name = real.name;
            entries.Clear();
            for (int i = 0; i < real.assetGuids.Length; i++)
                entries.Add(new()
                {
                    logicalAnd = real.logicalAnds[i],
                    defAsset = PermissionSystemEditorUtil.TryGetDefAssetByGuid(real.assetGuids[i], out var defAsset)
                        ? defAsset
                        : null,
                });
        }
    }

    [System.Serializable]
    internal struct ShowObjectByPermissionDummyEntry
    {
        public bool logicalAnd;
        public PermissionDefinitionAsset defAsset;
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(ShowObjectByPermission))]
    public class ShowObjectByPermissionEditor : Editor
    {
        private static readonly GUIContent mixedValueContent = EditorGUIUtility.TrTextContent("\u2014", "Mixed Values");

        private SerializedProperty whenConditionsAreMetProp;

        private SerializedObject[] sos;
        private SerializedProperty[] assetGuidsProps;
        private SerializedProperty[] logicalAndsProps;

        private ShowObjectByPermissionDummy[] dummies;
        private SerializedObject dummiesSo;
        private SerializedProperty entriesProp;
        private ReorderableList reorderableList;

        public void OnEnable()
        {
            whenConditionsAreMetProp = serializedObject.FindProperty("whenConditionsAreMet");

            sos = new SerializedObject[targets.Length];
            assetGuidsProps = new SerializedProperty[targets.Length];
            logicalAndsProps = new SerializedProperty[targets.Length];
            dummies = new ShowObjectByPermissionDummy[targets.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                sos[i] = new SerializedObject(targets[i]);
                assetGuidsProps[i] = sos[i].FindProperty("assetGuids");
                logicalAndsProps[i] = sos[i].FindProperty("logicalAnds");
                dummies[i] = CreateInstance<ShowObjectByPermissionDummy>();
                dummies[i].PopulateFromReal((ShowObjectByPermission)targets[i]);
            }
            dummiesSo = new SerializedObject(dummies);
            entriesProp = dummiesSo.FindProperty(nameof(ShowObjectByPermissionDummy.entries));

            reorderableList = new ReorderableList(
                dummiesSo,
                entriesProp,
                draggable: true,
                displayHeader: true,
                displayAddButton: true,
                displayRemoveButton: true);

            reorderableList.elementHeight = EditorGUIUtility.singleLineHeight;
            reorderableList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Conditions");
            reorderableList.drawElementCallback = DrawListElement;

            Undo.undoRedoPerformed += OnUndoRedo;
        }

        public void OnDisable()
        {
            foreach (ShowObjectByPermissionDummy dummy in dummies)
                DestroyImmediate(dummy);
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        void DrawListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            SerializedProperty prop = reorderableList.serializedProperty.GetArrayElementAtIndex(index);
            if (index != 0)
            {
                SerializedProperty operatorProp = prop.FindPropertyRelative(nameof(ShowObjectByPermissionDummyEntry.logicalAnd));
                Rect buttonRect = new Rect(rect.x, rect.y + 1f - reorderableList.elementHeight / 2f, 50f, EditorGUIUtility.singleLineHeight);
                buttonRect.width = operatorProp.hasMultipleDifferentValues ? 50f : 40f;
                if (!operatorProp.hasMultipleDifferentValues && operatorProp.boolValue)
                    buttonRect.x += 10f;
                if (GUI.Button(buttonRect, operatorProp.hasMultipleDifferentValues
                    ? mixedValueContent
                    : new GUIContent(operatorProp.boolValue ? "AND" : "OR")))
                {
                    operatorProp.boolValue = !operatorProp.boolValue;
                }
            }
            // Negative width is fine. Not the end of the world.
            EditorGUI.PropertyField(
                new Rect(rect.x + 52f, rect.y + 1f, -52f + rect.width, EditorGUIUtility.singleLineHeight),
                prop.FindPropertyRelative(nameof(ShowObjectByPermissionDummyEntry.defAsset)),
                GUIContent.none);
        }

        private void OnUndoRedo()
        {
            for (int i = 0; i < dummies.Length; i++)
                dummies[i].PopulateFromReal((ShowObjectByPermission)targets[i]);
            dummiesSo.Update();
        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(targets))
                return;

            serializedObject.Update();
            EditorGUILayout.PropertyField(whenConditionsAreMetProp);
            serializedObject.ApplyModifiedProperties();

            dummiesSo.Update();
            reorderableList.DoLayoutList();
            if (!dummiesSo.ApplyModifiedPropertiesWithoutUndo())
                return;

            for (int i = 0; i < sos.Length; i++)
            {
                sos[i].Update();
                EditorUtil.SetArrayProperty(
                    assetGuidsProps[i],
                    dummies[i].entries,
                    (p, v) => p.stringValue = EditorUtil.GetAssetGuidOrEmpty(v.defAsset));
                EditorUtil.SetArrayProperty(
                    logicalAndsProps[i],
                    dummies[i].entries,
                    (p, v) => p.boolValue = v.logicalAnd);
                sos[i].ApplyModifiedProperties();
            }
        }
    }
}
