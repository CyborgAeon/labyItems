using labyItems.Models.Characters;

namespace labyItems.Services;

public sealed record BattleboardDamageReductionEffects(
    IReadOnlyDictionary<string, int> DamagePerSixths);

public static class BattleboardDamageReductionEffectResolver
{
    private static readonly string[] ResistanceTypes = { "Physical", "Magic", "Neuro", "Spirit" };

    public static BattleboardDamageReductionEffects ResolveFallback(CharacterDraft? draft)
    {
        var perSixths = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var character = draft ?? new CharacterDraft();
        ApplyFallbackMultiRaceHeuristics(character, perSixths);

        return new BattleboardDamageReductionEffects(perSixths);
    }

    private static void ApplyFallbackMultiRaceHeuristics(
        CharacterDraft draft,
        IDictionary<string, int> perSixths)
    {
        var key = NormalizeToken(draft.MultiRaceKey);
        var level = Math.Max(0, draft.MultiRaceLevel);
        if (key.Length == 0 || level <= 0)
            return;

        if (key.Equals("spiritualvessel", StringComparison.OrdinalIgnoreCase))
        {
            ApplyScaledPerSixths(perSixths, exceptedTypes: new[] { "Spirit" }, level);
            return;
        }

        if (key.Equals("vesselofthedragons", StringComparison.OrdinalIgnoreCase))
            ApplyScaledPerSixths(perSixths, exceptedTypes: new[] { "Magic" }, level);
    }

    private static void ApplyScaledPerSixths(
        IDictionary<string, int> perSixths,
        IEnumerable<string> exceptedTypes,
        int level)
    {
        var excepted = new HashSet<string>(
            (exceptedTypes ?? Array.Empty<string>())
            .Select(BattleboardAdvancementEffectResolver.NormalizeResistanceType)
            .Where(x => x.Length > 0),
            StringComparer.OrdinalIgnoreCase);

        var clampedLevel = Math.Clamp(level, 0, 6);
        if (clampedLevel <= 0)
            return;

        foreach (var raw in ResistanceTypes)
        {
            var key = BattleboardAdvancementEffectResolver.NormalizeResistanceType(raw);
            if (key.Length == 0 || excepted.Contains(key))
                continue;

            if (!perSixths.TryGetValue(key, out var existing) || clampedLevel > existing)
                perSixths[key] = clampedLevel;
        }
    }

    private static string NormalizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return new string((value ?? string.Empty)
            .Trim()
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }
}
