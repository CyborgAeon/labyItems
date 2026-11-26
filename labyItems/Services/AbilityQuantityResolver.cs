namespace labyItems.Services;

public static class AbilityQuantityResolver
{
    public static int GetEffectiveLevels(int applicableLevel, int acquisitionLevel)
    {
        if (applicableLevel < acquisitionLevel)
            return 0;

        return (int)Math.Min(int.MaxValue, (long)applicableLevel - acquisitionLevel + 1);
    }

    public static int AddPermittedEffectiveLevels(int baseLevels, IEnumerable<int> contributions)
    {
        if (baseLevels <= 0)
            return 0;

        var levels = (long)baseLevels;
        foreach (var contribution in contributions)
        {
            levels += Math.Max(0, contribution);
            if (levels >= int.MaxValue)
                return int.MaxValue;
        }
        return (int)levels;
    }

    public static int GetFrequencyIncrements(int effectiveLevels, int frequency)
        => effectiveLevels <= 0 || frequency <= 0
            ? 0
            : 1 + (effectiveLevels - 1) / frequency;

    public static int GetProgressionQuantity(int applicableLevel, int perLevels, int amount)
        => (int)Math.Min(int.MaxValue, (long)(Math.Max(0, applicableLevel) / Math.Max(1, perLevels)) * Math.Max(0, amount));
}
