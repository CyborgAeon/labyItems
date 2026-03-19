using System.Text.RegularExpressions;
using labyItems.Helpers;
using labyItems.Models.Characters;
using labyItems.Services.Specialisations;

namespace labyItems.Services;

public sealed record BattleboardAdvancementEffects(
    IReadOnlyDictionary<string, int> ResistanceOverrides,
    IReadOnlyList<string> Immunities);

public static class BattleboardAdvancementEffectResolver
{
    private static readonly Regex LevelResistanceRegex = new(
        @"^\s*(?<level>\d+)(?:st|nd|rd|th)\s+level\s+resistance\s+to\s+(?<target>.+?)\s*$",
        RegexOptionsCompat.ForRuntime(RegexOptions.IgnoreCase | RegexOptions.Compiled));

    private static readonly Regex ImmunityToRegex = new(
        @"^\s*(?:total\s+)?immunity\s+to\s+(?<target>.+?)\s*$",
        RegexOptionsCompat.ForRuntime(RegexOptions.IgnoreCase | RegexOptions.Compiled));

    public static BattleboardAdvancementEffects ResolveFallback(IEnumerable<string>? advancementAbilityNames)
    {
        var resistance = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var immunities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in advancementAbilityNames ?? Array.Empty<string>())
        {
            var name = (raw ?? string.Empty).Trim();
            if (name.Length == 0)
                continue;

            ApplyFallbackNameEffects(name, resistance, immunities);
        }

        return new BattleboardAdvancementEffects(
            resistance,
            immunities.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList());
    }

    public static async Task<BattleboardAdvancementEffects> ResolveAsync(IEnumerable<string>? advancementAbilityNames)
    {
        var names = (advancementAbilityNames ?? Array.Empty<string>())
            .Select(x => (x ?? string.Empty).Trim())
            .Where(x => x.Length > 0)
            .ToList();

        var fallback = ResolveFallback(names);
        var resistance = new Dictionary<string, int>(fallback.ResistanceOverrides, StringComparer.OrdinalIgnoreCase);
        var immunities = new HashSet<string>(fallback.Immunities, StringComparer.OrdinalIgnoreCase);

        if (names.Count == 0)
            return new BattleboardAdvancementEffects(resistance, immunities.ToList());

        try
        {
            var lookup = await AbilityDetailsLookupService.GetLookupAsync();
            var index = await SpecialisationDefinitionRepository.GetIndexAsync();
            var references = index?.AbilityReferences
                             ?? new Dictionary<string, AbilityDefinition>(StringComparer.OrdinalIgnoreCase);

            foreach (var name in names)
            {
                var details = AbilityDetailsLookupService.FindByIndex(lookup, name);
                var abilityRef = (details?.AbilityRef ?? string.Empty).Trim();

                AbilityDefinition? definition = null;
                if (abilityRef.Length > 0 && references.TryGetValue(abilityRef, out var byRef))
                    definition = byRef;
                else
                    definition = ResolveByName(references, name);

                var effects = definition?.SystemEffects;
                if (effects == null || effects.Count == 0)
                    continue;

                ApplySystemEffects(effects, resistance, immunities);
            }
        }
        catch
        {
            // Fallback effects still apply when data lookup is unavailable.
        }

        return new BattleboardAdvancementEffects(
            resistance,
            immunities.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList());
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

    private static void ApplyFallbackNameEffects(
        string abilityName,
        IDictionary<string, int> resistance,
        ISet<string> immunities)
    {
        if (TryParseLevelResistance(abilityName, out var level, out var types))
        {
            foreach (var type in types)
                SetResistanceLevel(resistance, type, level);
        }

        if (TryParseImmunityName(abilityName, out var immunityName))
            immunities.Add(immunityName);
    }

    private static AbilityDefinition? ResolveByName(
        IReadOnlyDictionary<string, AbilityDefinition> references,
        string abilityName)
    {
        var normalized = AbilityDetailsLookupService.NormalizeKey(abilityName);
        if (normalized.Length == 0)
            return null;

        foreach (var pair in references)
        {
            var def = pair.Value;
            if (def == null)
                continue;

            var candidates = new[]
            {
                def.Name,
                def.Key,
                def.AbilityRef,
                def.UpdateKey,
                def.BattleboardNameOverride
            };

            if (candidates.Any(candidate =>
                    AbilityDetailsLookupService.NormalizeKey(candidate)
                        .Equals(normalized, StringComparison.OrdinalIgnoreCase)))
            {
                return def;
            }
        }

        return null;
    }

    private static void ApplySystemEffects(
        IEnumerable<AbilitySystemEffect> effects,
        IDictionary<string, int> resistance,
        ISet<string> immunities)
    {
        foreach (var effect in effects ?? Array.Empty<AbilitySystemEffect>())
        {
            if (effect == null)
                continue;

            var effectType = (effect.EffectType ?? string.Empty).Trim();
            if (effectType.Length == 0)
                continue;

            if (effectType.StartsWith("lor:", StringComparison.OrdinalIgnoreCase))
            {
                var token = effectType[4..].Trim();
                var resistanceType = NormalizeResistanceType(
                    !string.IsNullOrWhiteSpace(effect.ResistanceType)
                        ? effect.ResistanceType
                        : token);
                if (resistanceType.Length == 0)
                    continue;

                var level = Math.Max(0, effect.Level ?? ParseFirstInt(effect.DisplayName));
                if (level <= 0)
                    continue;

                SetResistanceLevel(resistance, resistanceType, level);
                continue;
            }

            if (effectType.Equals("immunity", StringComparison.OrdinalIgnoreCase))
            {
                var display = (effect.DisplayName ?? string.Empty).Trim();
                var target = (effect.ImmunityName ?? string.Empty).Trim();

                if (display.Length == 0 && target.Length > 0)
                    display = $"Immunity to {target}";
                if (display.Length == 0)
                    continue;

                immunities.Add(display);
            }
        }
    }

    private static bool TryParseLevelResistance(
        string abilityName,
        out int level,
        out IReadOnlyList<string> resistanceTypes)
    {
        level = 0;
        resistanceTypes = Array.Empty<string>();

        var match = LevelResistanceRegex.Match(abilityName ?? string.Empty);
        if (!match.Success)
            return false;

        if (!int.TryParse(match.Groups["level"].Value, out level) || level <= 0)
            return false;

        var target = (match.Groups["target"].Value ?? string.Empty).Trim().ToLowerInvariant();
        if (target.Length == 0)
            return false;

        var types = ResolveResistanceTypes(target);
        if (types.Count == 0)
            return false;

        resistanceTypes = types;
        return true;
    }

    private static List<string> ResolveResistanceTypes(string target)
    {
        var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var text = (target ?? string.Empty).Trim().ToLowerInvariant();
        if (text.Length == 0)
            return types.ToList();

        if (text.Contains("all but neuronic") || text.Contains("all but neuro"))
        {
            types.Add("Physical");
            types.Add("Magic");
            types.Add("Spirit");
            return types.ToList();
        }

        if (text.Contains("all"))
        {
            types.Add("Physical");
            types.Add("Magic");
            types.Add("Neuro");
            types.Add("Spirit");
            return types.ToList();
        }

        if (text.Contains("physical"))
            types.Add("Physical");
        if (text.Contains("magic"))
            types.Add("Magic");
        if (text.Contains("neuronic") || text.Contains("neuro"))
            types.Add("Neuro");
        if (text.Contains("spirit"))
            types.Add("Spirit");

        return types.ToList();
    }

    private static bool TryParseImmunityName(string abilityName, out string immunityName)
    {
        immunityName = string.Empty;
        var text = (abilityName ?? string.Empty).Trim();
        if (text.Length == 0)
            return false;

        var match = ImmunityToRegex.Match(text);
        if (!match.Success)
            return false;

        var target = (match.Groups["target"].Value ?? string.Empty).Trim();
        if (target.Length == 0)
            return false;

        immunityName = $"Immunity to {target}";
        return true;
    }

    private static void SetResistanceLevel(IDictionary<string, int> resistance, string resistanceType, int level)
    {
        var key = NormalizeResistanceType(resistanceType);
        if (key.Length == 0)
            return;

        var safeLevel = Math.Max(0, level);
        if (safeLevel == 0)
            return;

        if (!resistance.TryGetValue(key, out var existing) || safeLevel > existing)
            resistance[key] = safeLevel;
    }

    public static string NormalizeResistanceType(string? raw)
    {
        var value = (raw ?? string.Empty).Trim().ToLowerInvariant();
        if (value.Length == 0)
            return string.Empty;

        if (value.Contains("phys"))
            return "Physical";
        if (value.Contains("mag"))
            return "Magic";
        if (value.Contains("neur") || value.Contains("neuro"))
            return "Neuro";
        if (value.Contains("spirit"))
            return "Spirit";

        return value switch
        {
            "physical" => "Physical",
            "magic" => "Magic",
            "neuro" => "Neuro",
            "neuronic" => "Neuro",
            "spirit" => "Spirit",
            _ => string.Empty
        };
    }

    private static int ParseFirstInt(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var match = Regex.Match(text, @"-?\d+");
        if (match.Success && int.TryParse(match.Value, out var parsed))
            return parsed;

        return 0;
    }
}
