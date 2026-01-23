namespace JanSharp
{
    public enum WhenConditionsAreMetType
    {
        Show,
        Hide,
    }

    public static class PermissionsUtil
    {
        /// <summary>
        /// <para>Returns <see langword="true"/> for empty conditions list.</para>
        /// </summary>
        /// <param name="logicalAnds"></param>
        /// <param name="inverts"></param>
        /// <param name="permissionDefs"></param>
        /// <returns></returns>
        public static bool ResolveConditionsList(bool[] logicalAnds, bool[] inverts, PermissionDefinition[] permissionDefs)
        {
            int length = permissionDefs.Length;
            bool conditionsMatching = true;
            for (int i = 0; i < length; i++)
            {
                bool logicalAnd = logicalAnds[i];
                if (!conditionsMatching && logicalAnd)
                    continue;
                if (!logicalAnd && conditionsMatching && i != 0)
                    break;
                conditionsMatching = permissionDefs[i].valueForLocalPlayer != inverts[i];
            }
            return conditionsMatching;
        }
    }
}
