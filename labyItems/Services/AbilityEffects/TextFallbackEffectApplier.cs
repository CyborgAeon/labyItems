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

    public static void Apply(
        string? text,
        IDictionary<string, int> resistance,
        ISet<string> immunities)
    {
        foreach (var instruction in ExtractInstructions(text))
            AbilityEffectEvaluator.ApplyInstructions(new[] { instruction }, resistance, immunities);
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
}
