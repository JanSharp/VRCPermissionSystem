namespace JanSharp
{
    public enum WhenConditionsAreMetType
    {
        Show,
        Hide,
    }

    public static class PermissionsUtil
    {
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
