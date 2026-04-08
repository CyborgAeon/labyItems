using labyItems.Services.AbilityEffects;

namespace labyItems.Services;

public sealed record BattleboardAdvancementEffects(
    IReadOnlyDictionary<string, int> ResistanceOverrides,
    IReadOnlyList<string> Immunities,
    IReadOnlyDictionary<string, int> ResistanceMultipliers,
    IReadOnlySet<string> InfiniteResistanceTypes,
    IReadOnlyDictionary<string, int> ResistancePerSixths);

public static class BattleboardAdvancementEffectResolver
{
    public static BattleboardAdvancementEffects ResolveFallback(IEnumerable<string>? advancementAbilityKeysOrNames)
    {
        var resistance = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var multipliers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var infiniteResistanceTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var immunities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in advancementAbilityKeysOrNames ?? Array.Empty<string>())
        {
            var name = (raw ?? string.Empty).Trim();
            if (name.Length == 0)
                continue;

            TextFallbackEffectApplier.Apply(name, resistance, immunities, multipliers, infiniteResistanceTypes);
        }

        return new BattleboardAdvancementEffects(
            resistance,
            immunities.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
            multipliers,
            infiniteResistanceTypes,
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
    }

    public static async Task<BattleboardAdvancementEffects> ResolveAsync(IEnumerable<string>? advancementAbilityKeysOrNames)
    {
        // This method supports both:
        // - legacy draft snapshots storing display names
        // - current draft snapshots storing stable ability keys (see `AbilityKey`)
        var keysOrNames = (advancementAbilityKeysOrNames ?? Array.Empty<string>())
            .Select(x => (x ?? string.Empty).Trim())
            .Where(x => x.Length > 0)
            .ToList();

        var fallback = ResolveFallback(keysOrNames);
        var resistance = new Dictionary<string, int>(fallback.ResistanceOverrides, StringComparer.OrdinalIgnoreCase);
        var multipliers = new Dictionary<string, int>(fallback.ResistanceMultipliers, StringComparer.OrdinalIgnoreCase);
        var infiniteResistanceTypes = new HashSet<string>(fallback.InfiniteResistanceTypes, StringComparer.OrdinalIgnoreCase);
        var perSixths = new Dictionary<string, int>(fallback.ResistancePerSixths, StringComparer.OrdinalIgnoreCase);
        var immunities = new HashSet<string>(fallback.Immunities, StringComparer.OrdinalIgnoreCase);

        if (keysOrNames.Count == 0)
            return new BattleboardAdvancementEffects(resistance, immunities.ToList(), multipliers, infiniteResistanceTypes, perSixths);

        try
        {
            var lookup = await AbilityDetailsLookupService.GetLookupAsync();
            var definitionLookup = await AbilityDefinitionLookupService.GetLookupAsync();

            foreach (var keyOrName in keysOrNames)
            {
                var details = AbilityDetailsLookupService.FindByIndex(lookup, keyOrName);
                var resolvedDisplayName = (details?.Index ?? keyOrName).Trim();
                var abilityRef = (details?.AbilityRef ?? string.Empty).Trim();

                var definition = AbilityDefinitionLookupService.Find(definitionLookup, abilityRef)
                                 ?? AbilityDefinitionLookupService.Find(definitionLookup, keyOrName)
                                 ?? AbilityDefinitionLookupService.Find(definitionLookup, resolvedDisplayName);

                var effects = definition?.SystemEffects;
                if (effects is { Count: > 0 })
                {
                    var instructions = AbilityEffectEvaluator.FromSystemEffects(effects);
                    AbilityEffectEvaluator.ApplyInstructions(
                        instructions,
                        resistance,
                        immunities,
                        multipliers,
                        infiniteResistanceTypes);
                }

                // Keep text fallback behaviour for effects that are not yet represented in `SystemEffects`.
                TextFallbackEffectApplier.Apply(
                    resolvedDisplayName,
                    resistance,
                    immunities,
                    multipliers,
                    infiniteResistanceTypes);
                TextFallbackEffectApplier.Apply(
                    definition?.Effect,
                    resistance,
                    immunities,
                    multipliers,
                    infiniteResistanceTypes);
            }
        }
        catch
        {
            // Fallback effects still apply when data lookup is unavailable.
        }

        return new BattleboardAdvancementEffects(
            resistance,
            immunities.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
            multipliers,
            infiniteResistanceTypes,
            perSixths);
    }

    public static IReadOnlyDictionary<string, int> ApplyResistanceOverrides(
        IReadOnlyDictionary<string, int>? baseline,
        IReadOnlyDictionary<string, int>? overrides)
    {
        var merged = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in baseline ?? new Dictionary<string, int>())
        {
            var key = NormalizeResistanceType(pair.Key);
            if (key.Length == 0)
                continue;
            merged[key] = Math.Max(0, pair.Value);
        }

        foreach (var pair in overrides ?? new Dictionary<string, int>())
        {
            var key = NormalizeResistanceType(pair.Key);
            if (key.Length == 0)
                continue;

            var incoming = Math.Max(0, pair.Value);
            if (!merged.TryGetValue(key, out var current) || incoming > current)
                merged[key] = incoming;
        }

        return merged;
    }

    public static string NormalizeResistanceType(string? raw)
        => AbilityEffectEvaluator.NormalizeResistanceType(raw);

    public static IReadOnlyDictionary<string, int> ApplyResistanceMultipliers(
        IReadOnlyDictionary<string, int>? baseline,
        IReadOnlyDictionary<string, int>? overrides)
    {
        var merged = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in baseline ?? new Dictionary<string, int>())
        {
            var key = NormalizeResistanceType(pair.Key);
            if (key.Length == 0)
                continue;

            var value = Math.Max(1, pair.Value);
            merged[key] = value;
        }

        foreach (var pair in overrides ?? new Dictionary<string, int>())
        {
            var key = NormalizeResistanceType(pair.Key);
            if (key.Length == 0)
                continue;

            var incoming = Math.Max(1, pair.Value);
            if (!merged.TryGetValue(key, out var current) || incoming > current)
                merged[key] = incoming;
        }

        return merged;
    }

    public static IReadOnlySet<string> MergeInfiniteResistanceTypes(
        IEnumerable<string>? baseline,
        IEnumerable<string>? incoming)
    {
        var merged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in baseline ?? Array.Empty<string>())
        {
            var key = NormalizeResistanceType(raw);
            if (key.Length > 0)
                merged.Add(key);
        }

        foreach (var raw in incoming ?? Array.Empty<string>())
        {
            var key = NormalizeResistanceType(raw);
            if (key.Length > 0)
                merged.Add(key);
        }

        return merged;
    }

    public static IReadOnlyDictionary<string, int> ApplyResistancePerSixths(
        IReadOnlyDictionary<string, int>? baseline,
        IReadOnlyDictionary<string, int>? overrides)
    {
        var merged = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in baseline ?? new Dictionary<string, int>())
        {
            var key = NormalizeResistanceType(pair.Key);
            if (key.Length == 0)
                continue;

            var value = Math.Clamp(pair.Value, 0, 6);
            if (value <= 0)
                continue;

            merged[key] = value;
        }

        foreach (var pair in overrides ?? new Dictionary<string, int>())
        {
            var key = NormalizeResistanceType(pair.Key);
            if (key.Length == 0)
                continue;

            var incoming = Math.Clamp(pair.Value, 0, 6);
            if (incoming <= 0)
                continue;

            if (!merged.TryGetValue(key, out var current) || incoming > current)
                merged[key] = incoming;
        }

        return merged;
    }
}
