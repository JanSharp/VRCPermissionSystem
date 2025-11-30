using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace JanSharp
{
    internal class PermissionConditionsDummy : ScriptableObject
    {
        public List<PermissionConditionsDummyEntry> entries = new();

        public void PopulateFromReal(string name, bool[] logicalAnds, string[] assetGuids)
        {
            this.name = name;
            entries.Clear();
            if (logicalAnds == null || assetGuids == null) // Newly created object.
                return;
            for (int i = 0; i < assetGuids.Length; i++)
                entries.Add(new()
                {
                    logicalAnd = logicalAnds[i],
                    defAsset = PermissionSystemEditorUtil.TryGetDefAssetByGuid(assetGuids[i], out var defAsset)
                        ? defAsset
                        : null,
                });
        }
    }

    [System.Serializable]
    internal struct PermissionConditionsDummyEntry
    {
        public bool logicalAnd;
        public PermissionDefinitionAsset defAsset;
    }

    public class PermissionConditionsList
    {
        private Object[] targets;
        private System.Func<Object, bool[]> getLogicalAnds;
        private System.Func<Object, string[]> getAssetGuids;

        private SerializedObject[] sos;
        private SerializedProperty[] assetGuidsProps;
        private SerializedProperty[] logicalAndsProps;

        private PermissionConditionsDummy[] dummies;
        private SerializedObject dummiesSo;
        private SerializedProperty entriesProp;
        private ReorderableList reorderableList;

        public PermissionConditionsList(
            Object[] targets,
            GUIContent header,
            string logicalAndsFieldName,
            string assetGuidsFieldName,
            System.Func<Object, bool[]> getLogicalAnds,
            System.Func<Object, string[]> getAssetGuids)
        {
            this.targets = targets;
            this.getLogicalAnds = getLogicalAnds;
            this.getAssetGuids = getAssetGuids;

            sos = new SerializedObject[targets.Length];
            logicalAndsProps = new SerializedProperty[targets.Length];
            assetGuidsProps = new SerializedProperty[targets.Length];
            dummies = new PermissionConditionsDummy[targets.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                sos[i] = new SerializedObject(targets[i]);
                logicalAndsProps[i] = sos[i].FindProperty(logicalAndsFieldName);
                assetGuidsProps[i] = sos[i].FindProperty(assetGuidsFieldName);
                dummies[i] = ScriptableObject.CreateInstance<PermissionConditionsDummy>();
                PopulateFromReal(dummies[i], targets[i]);
            }
            dummiesSo = new SerializedObject(dummies);
            entriesProp = dummiesSo.FindProperty(nameof(PermissionConditionsDummy.entries));

            reorderableList = new ReorderableList(
                dummiesSo,
                entriesProp,
                draggable: true,
                displayHeader: true,
                displayAddButton: true,
                displayRemoveButton: true);

            reorderableList.elementHeight = EditorGUIUtility.singleLineHeight;
            reorderableList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, header);
            reorderableList.drawElementCallback = DrawListElement;

            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void PopulateFromReal(PermissionConditionsDummy dummy, Object target)
        {
            dummy.PopulateFromReal(target.name, getLogicalAnds(target), getAssetGuids(target));
        }

        public void OnDisable()
        {
            foreach (PermissionConditionsDummy dummy in dummies)
                Object.DestroyImmediate(dummy);
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        void DrawListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            SerializedProperty prop = reorderableList.serializedProperty.GetArrayElementAtIndex(index);
            if (index != 0)
            {
                SerializedProperty operatorProp = prop.FindPropertyRelative(nameof(PermissionConditionsDummyEntry.logicalAnd));
                Rect buttonRect = new Rect(rect.x, rect.y + 1f - reorderableList.elementHeight / 2f, 50f, EditorGUIUtility.singleLineHeight);
                buttonRect.width = operatorProp.hasMultipleDifferentValues ? 50f : 40f;
                if (!operatorProp.hasMultipleDifferentValues && operatorProp.boolValue)
                    buttonRect.x += 10f;
                if (GUI.Button(buttonRect, operatorProp.hasMultipleDifferentValues
                    ? EditorUtil.mixedValueGUIContent
                    : new GUIContent(operatorProp.boolValue ? "AND" : "OR")))
                {
                    operatorProp.boolValue = !operatorProp.boolValue;
                }
            }
            // Negative width is fine. Not the end of the world.
            EditorGUI.PropertyField(
                new Rect(rect.x + 52f, rect.y + 1f, -52f + rect.width, EditorGUIUtility.singleLineHeight),
                prop.FindPropertyRelative(nameof(PermissionConditionsDummyEntry.defAsset)),
                GUIContent.none);
        }

        private void OnUndoRedo()
        {
            for (int i = 0; i < dummies.Length; i++)
                PopulateFromReal(dummies[i], targets[i]);
            dummiesSo.Update();
        }

        public void Draw()
        {
            dummiesSo.Update();
            reorderableList.DoLayoutList();
            if (!dummiesSo.ApplyModifiedPropertiesWithoutUndo())
                return;

            for (int i = 0; i < sos.Length; i++)
            {
                sos[i].Update();
                EditorUtil.SetArrayProperty(
                    logicalAndsProps[i],
                    dummies[i].entries,
                    (p, v) => p.boolValue = v.logicalAnd);
                EditorUtil.SetArrayProperty(
                    assetGuidsProps[i],
                    dummies[i].entries,
                    (p, v) => p.stringValue = EditorUtil.GetAssetGuidOrEmpty(v.defAsset));
                sos[i].ApplyModifiedProperties();
            }
        }
    }
}
