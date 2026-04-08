namespace labyItems.Services;

public static class BattleboardResistanceLevelService
{
    private static readonly string[] PreferredTypes = { "Physical", "Magic", "Neuro", "Spirit" };

    public static Dictionary<string, int> BuildBaselineRawLevels(
        IReadOnlyDictionary<string, int>? baseline,
        IReadOnlyDictionary<string, int>? overrides = null)
    {
        var merged = new Dictionary<string, int>(
            BattleboardAdvancementEffectResolver.ApplyResistanceOverrides(baseline, overrides),
            StringComparer.OrdinalIgnoreCase);

        var normalized = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in merged)
        {
            var key = BattleboardAdvancementEffectResolver.NormalizeResistanceType(pair.Key);
            if (key.Length == 0)
                continue;

            var raw = pair.Value <= 0 ? 8 : pair.Value;
            normalized[key] = raw;
        }

        foreach (var preferred in PreferredTypes)
        {
            if (!normalized.TryGetValue(preferred, out var raw) || raw <= 0)
                normalized[preferred] = 8;
        }

        return normalized;
    }

    public static IReadOnlyDictionary<string, int> BuildDisplayedLevels(
        IReadOnlyDictionary<string, int>? rawLevels,
        IReadOnlyDictionary<string, int>? multipliers,
        IReadOnlySet<string>? infiniteResistanceTypes,
        IReadOnlyDictionary<string, int>? perSixths = null)
    {
        var baseline = BuildBaselineRawLevels(rawLevels);
        var multiplierLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in multipliers ?? new Dictionary<string, int>())
        {
            var key = BattleboardAdvancementEffectResolver.NormalizeResistanceType(pair.Key);
            if (key.Length == 0)
                continue;

            multiplierLookup[key] = Math.Max(1, pair.Value);
        }

        var infiniteSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (infiniteResistanceTypes != null)
        {
            foreach (var raw in infiniteResistanceTypes)
            {
                var key = BattleboardAdvancementEffectResolver.NormalizeResistanceType(raw);
                if (key.Length > 0)
                    infiniteSet.Add(key);
            }
        }

        var perSixthLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in perSixths ?? new Dictionary<string, int>())
        {
            var key = BattleboardAdvancementEffectResolver.NormalizeResistanceType(pair.Key);
            if (key.Length == 0)
                continue;

            var value = Math.Clamp(pair.Value, 0, 6);
            if (value <= 0)
                continue;

            perSixthLookup[key] = value;
        }

        var displayed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in baseline)
        {
            var key = BattleboardAdvancementEffectResolver.NormalizeResistanceType(pair.Key);
            if (key.Length == 0)
                continue;

            if (infiniteSet.Contains(key))
            {
                displayed[key] = int.MaxValue;
                continue;
            }

            var multiplier = multiplierLookup.TryGetValue(key, out var resolvedMultiplier)
                ? resolvedMultiplier
                : 1;
            var baseDisplayed = Math.Max(0, pair.Value) * Math.Max(1, multiplier);

            if (perSixthLookup.TryGetValue(key, out var perSixthLevel))
            {
                if (perSixthLevel >= 6)
                {
                    displayed[key] = int.MaxValue;
                    continue;
                }

                if (perSixthLevel > 0)
                    baseDisplayed += CalculatePerSixthBonus(baseDisplayed, perSixthLevel);
            }

            displayed[key] = baseDisplayed;
        }

        return displayed;
    }

    public static int CalculatePerSixthBonus(int baseLevel, int perSixthLevel)
    {
        if (baseLevel <= 0)
            return 0;

        var clamped = Math.Clamp(perSixthLevel, 0, 6);
        if (clamped <= 0)
            return 0;

        return (int)Math.Round(baseLevel * (clamped / 6d), MidpointRounding.AwayFromZero);
    }
}
