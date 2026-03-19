using System.Text.RegularExpressions;
using labyItems.Helpers;
using labyItems.Models.Abilities.Effects;

namespace labyItems.Services.AbilityEffects;

public static class TextFallbackEffectApplier
{
    private static readonly Regex LevelResistanceRegex = new(
        @"^\s*(?<level>\d+)(?:st|nd|rd|th)\s+level\s+resistance\s+to\s+(?<target>.+?)\s*$",
        RegexOptionsCompat.ForRuntime(RegexOptions.IgnoreCase | RegexOptions.Compiled));

    private static readonly Regex ImmunityToRegex = new(
        @"^\s*(?:total\s+)?immunity\s+to\s+(?<target>.+?)\s*$",
        RegexOptionsCompat.ForRuntime(RegexOptions.IgnoreCase | RegexOptions.Compiled));

    private static readonly Regex HalfSpiritRegex = new(
        @"\bhalf(?:-|\s)?(?:a\s+)?spirit\b",
        RegexOptionsCompat.ForRuntime(RegexOptions.IgnoreCase | RegexOptions.Compiled));

    public static void Apply(
        string? text,
        IDictionary<string, int> resistance,
        ISet<string> immunities,
        IDictionary<string, int>? resistanceMultipliers = null,
        ISet<string>? infiniteResistanceTypes = null)
    {
        foreach (var instruction in ExtractInstructions(text))
            AbilityEffectEvaluator.ApplyInstructions(
                new[] { instruction },
                resistance,
                immunities,
                resistanceMultipliers,
                infiniteResistanceTypes);
    }

    public static IEnumerable<AbilityEffectInstruction> ExtractInstructions(string? text)
    {
        var source = (text ?? string.Empty).Trim();
        if (source.Length == 0)
            yield break;

        if (TryParseLevelResistance(source, out var level, out var resistanceTypes))
        {
            foreach (var resistanceType in resistanceTypes)
            {
                yield return new AbilityEffectInstruction(
                    AbilityEffectInstructionKind.ResistanceLevelOverride,
                    ResistanceType: resistanceType,
                    Level: level,
                    ImmunityName: null);
            }
        }

        if (TryParseImmunityName(source, out var immunityName))
        {
            yield return new AbilityEffectInstruction(
                AbilityEffectInstructionKind.Immunity,
                ResistanceType: null,
                Level: null,
                ImmunityName: immunityName);
        }

        foreach (var resistanceType in ParseHalfEffectResistanceTypes(source))
        {
            yield return new AbilityEffectInstruction(
                AbilityEffectInstructionKind.ResistanceLevelMultiplier,
                ResistanceType: resistanceType,
                Level: 2,
                ImmunityName: null);
        }

        if (TryParseSpiritlessResistance(source))
        {
            yield return new AbilityEffectInstruction(
                AbilityEffectInstructionKind.ResistanceInfinite,
                ResistanceType: "Spirit",
                Level: null,
                ImmunityName: null);
        }

        if (TryParseMindlessResistance(source))
        {
            yield return new AbilityEffectInstruction(
                AbilityEffectInstructionKind.ResistanceInfinite,
                ResistanceType: "Neuro",
                Level: null,
                ImmunityName: null);
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

        if (text.Contains("all but neuronic")
            || text.Contains("all but neuro")
            || text.Contains("all bar neuronic")
            || text.Contains("all bar neuro"))
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

    private static IEnumerable<string> ParseHalfEffectResistanceTypes(string source)
    {
        var text = (source ?? string.Empty).Trim();
        if (text.Length == 0)
            yield break;

        var normalized = text.ToLowerInvariant();

        if (ContainsHalfEffectFor(normalized, "magic"))
            yield return "Magic";

        if (ContainsHalfEffectFor(normalized, "spirit")
            || ContainsHalfEffectFor(normalized, "spiritual")
            || HalfSpiritRegex.IsMatch(text))
        {
            yield return "Spirit";
        }
    }

    private static bool ContainsHalfEffectFor(string normalized, string target)
    {
        if (normalized.Length == 0 || target.Length == 0)
            return false;

        return normalized.Contains($"1/2 effect {target}", StringComparison.Ordinal)
               || normalized.Contains($"1/2-effect {target}", StringComparison.Ordinal)
               || normalized.Contains($"1/2 off {target}", StringComparison.Ordinal)
               || normalized.Contains($"1/2-off {target}", StringComparison.Ordinal)
               || normalized.Contains($"half effect {target}", StringComparison.Ordinal)
               || normalized.Contains($"half-effect {target}", StringComparison.Ordinal)
               || normalized.Contains($"half off {target}", StringComparison.Ordinal)
               || normalized.Contains($"half-off {target}", StringComparison.Ordinal)
               || normalized.Contains($"half a {target}", StringComparison.Ordinal)
               || (target.Equals("magic", StringComparison.Ordinal)
                   && normalized.Contains("twice their level", StringComparison.Ordinal)
                   && normalized.Contains("magic", StringComparison.Ordinal));
    }

    private static bool TryParseSpiritlessResistance(string source)
    {
        var text = (source ?? string.Empty).Trim();
        if (text.Length == 0)
            return false;

        var normalized = text.ToLowerInvariant();
        if (normalized.Equals("spiritless", StringComparison.Ordinal)
            || normalized.Contains("spiritless", StringComparison.Ordinal))
            return true;

        return normalized.Contains("infinite", StringComparison.Ordinal)
               && normalized.Contains("resistance", StringComparison.Ordinal)
               && normalized.Contains("spirit", StringComparison.Ordinal);
    }

    private static bool TryParseMindlessResistance(string source)
    {
        var text = (source ?? string.Empty).Trim();
        if (text.Length == 0)
            return false;

        var normalized = text.ToLowerInvariant();
        if (normalized.Equals("mindless", StringComparison.Ordinal)
            || normalized.Contains("mindless", StringComparison.Ordinal))
            return true;

        return normalized.Contains("infinite", StringComparison.Ordinal)
               && normalized.Contains("resistance", StringComparison.Ordinal)
               && (normalized.Contains("neuro", StringComparison.Ordinal)
                   || normalized.Contains("neuronic", StringComparison.Ordinal));
    }
}
