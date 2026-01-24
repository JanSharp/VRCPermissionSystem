using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UdonSharp;
using UnityEditor;
using UnityEngine;

namespace JanSharp
{
    [InitializeOnLoad]
    public static class PermissionDefinitionReferenceOnBuild
    {
        /// <summary>
        /// <para>Contains only entires where <see cref="TypeCache.fieldPairs"/> contain at least one value.</para>
        /// </summary>
        private static List<TypeCache> ubTypeCache = new();
        private static List<System.Type> invalidUbTypes = new();
        private const BindingFlags PrivateAndPublicFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private class TypeCache
        {
            public System.Type ubType;
            public bool isPermissionResolver;
            public List<(string guidFieldName, string permissionDefFieldName, bool optional)> fieldPairs = new();

            public TypeCache(System.Type ubType)
            {
                this.ubType = ubType;
                isPermissionResolver = EditorUtil.DerivesFrom(ubType, typeof(PermissionResolver));
            }
        }

        static PermissionDefinitionReferenceOnBuild()
        {
            ubTypeCache.Clear();
            invalidUbTypes.Clear();
            foreach (System.Type ubType in OnAssemblyLoadUtil.AllUdonSharpBehaviourTypes)
                TryGenerateTypeCache(ubType);
            if (invalidUbTypes.Count != 0)
            {
                OnBuildUtil.RegisterAction(InvalidPermissionDefinitionReferences, order: -1000000);
                return;
            }

            foreach (TypeCache cached in ubTypeCache)
            {
                OnBuildUtil.RegisterTypeCumulative(
                    cached.ubType,
                    ubs => OnBuildCumulative(ubs.Cast<UdonSharpBehaviour>(), cached),
                    order: -953); // Must be > -1000 due to dependency on PermissionDefinitionOnBuild
            }
        }

        private static bool InvalidPermissionDefinitionReferences()
        {
            foreach (System.Type ubType in invalidUbTypes)
                TryGenerateTypeCache(ubType, validateOnly: true);
            return false;
        }

        private static void TryGenerateTypeCache(System.Type ubType, bool validateOnly = false)
        {
            TypeCache cached = validateOnly ? null : new(ubType);

            bool isValid = true;

            foreach (FieldInfo field in EditorUtil.GetFieldsIncludingBase(ubType, PrivateAndPublicFlags, stopAtType: typeof(UdonSharpBehaviour)))
            {
                PermissionDefinitionReferenceAttribute attr = field.GetCustomAttribute<PermissionDefinitionReferenceAttribute>(inherit: true);
                if (attr == null)
                    continue;

                if (field.FieldType != typeof(string))
                {
                    Debug.LogError($"[PermissionSystem] The {ubType.Name}.{field.Name} field has the {nameof(PermissionDefinitionReferenceAttribute)} "
                        + $"however its type is {field.FieldType.Name}. It must be a string.");
                    isValid = false;
                }
                if (!EditorUtil.IsSerializedField(field))
                {
                    Debug.LogError($"[PermissionSystem] The {ubType.Name}.{field.Name} field has the {nameof(PermissionDefinitionReferenceAttribute)} "
                        + $"however it is not a serialized field. It must either be public or have the {nameof(SerializeField)} attribute.");
                    isValid = false;
                }

                string defFieldName = attr.PermissionDefinitionFieldName ?? "";
                FieldInfo defField = EditorUtil.GetFieldIncludingBase(ubType, defFieldName, PrivateAndPublicFlags);
                if (defField == null)
                {
                    Debug.LogError($"[PermissionSystem] The {ubType.Name}.{field.Name} field has the {nameof(PermissionDefinitionReferenceAttribute)} "
                        + $"pointing to a {nameof(PermissionDefinition)} field by the name '{defFieldName}' however no such field exists.");
                    isValid = false;
                }
                if (defField != null && defField.FieldType != typeof(PermissionDefinition))
                {
                    Debug.LogError($"[PermissionSystem] The {ubType.Name}.{field.Name} field has the {nameof(PermissionDefinitionReferenceAttribute)} "
                        + $"pointing to the field by the name '{defFieldName}' which has the type {defField.FieldType.Name}, "
                        + $"however it must be a {nameof(PermissionDefinition)}.");
                    isValid = false;
                }
                if (defField != null && !EditorUtil.IsSerializedField(defField))
                {
                    Debug.LogError($"[PermissionSystem] The {ubType.Name}.{field.Name} field has the {nameof(PermissionDefinitionReferenceAttribute)} "
                        + $"pointing to the field by the name '{defFieldName}' which is not a serialized field. "
                        + $"It must either be public or have the {nameof(SerializeField)} attribute.");
                    isValid = false;
                }

                if (isValid && !validateOnly)
                    cached.fieldPairs.Add((field.Name, defFieldName, attr.Optional));
            }

            if (validateOnly)
                return;

            if (!isValid)
            {
                invalidUbTypes.Add(ubType);
                return;
            }

            if (cached.fieldPairs.Count != 0)
                ubTypeCache.Add(cached);
        }

        private static bool OnBuildCumulative(IEnumerable<UdonSharpBehaviour> ubs, TypeCache cached)
        {
            bool result = true;
            foreach (UdonSharpBehaviour ub in ubs)
                if (!OnBuild(ub, cached))
                    result = false;
            return result;
        }

        private static bool OnBuild(UdonSharpBehaviour ub, TypeCache cached)
        {
            bool result = true;
            SerializedObject so = new(ub);
            foreach (var data in cached.fieldPairs)
            {
                string guid = so.FindProperty(data.guidFieldName).stringValue;
                PermissionDefinition permissionDef = cached.isPermissionResolver
                    ? PermissionDefinitionOnBuild.RegisterPermissionDefDependency((PermissionResolver)ub, guid)
                    : PermissionDefinitionOnBuild.GetOrCreatePermissionDef(guid);
                so.FindProperty(data.permissionDefFieldName).objectReferenceValue = permissionDef;

                if (!data.optional && permissionDef == null)
                {
                    Debug.LogError($"[PermissionSystem] The {cached.ubType.Name}.{data.guidFieldName} field has "
                        + $"the {nameof(PermissionDefinitionReferenceAttribute)}, is non optional and is missing "
                        + $"a reference to any {nameof(PermissionDefinitionAsset)}.", ub);
                    result = false;
                }
            }
            so.ApplyModifiedProperties();
            return result;
        }
    }
}
