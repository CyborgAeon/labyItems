using System.Text.RegularExpressions;
using labyItems.Helpers;
using labyItems.Models.Characters;

namespace labyItems.Services;

public readonly record struct ArmourBonusTotals(int Pac, int Dac, int Mac, int Sac, int Nac);

public static class ArmourBonusResolver
{
    private static readonly Regex ArmourTokenRegex = new(
        @"([+-]?\d+)\s*(PAC|DAC|MAC|SAC|NAC)",
        RegexOptionsCompat.ForRuntime(RegexOptions.IgnoreCase | RegexOptions.Compiled));

    public static ArmourBonusTotals ResolveMaxPerSourceTotals(IEnumerable<AbilityDraft>? abilities)
    {
        var maxBySourceStat = new Dictionary<(string Source, string Stat), int>();

        foreach (var ability in abilities ?? Enumerable.Empty<AbilityDraft>())
        {
            if (ability == null)
                continue;

            var sourceKey = BuildSourceKey(ability).ToUpperInvariant();
            var values = ResolveArmourValues(ability);
            if (values.Count == 0)
                continue;

            foreach (var kvp in values)
            {
                var mapKey = (sourceKey, kvp.Key);
                if (!maxBySourceStat.TryGetValue(mapKey, out var current) || kvp.Value > current)
                    maxBySourceStat[mapKey] = kvp.Value;
            }
        }

        int pac = 0, dac = 0, mac = 0, sac = 0, nac = 0;
        foreach (var kvp in maxBySourceStat)
        {
            switch (kvp.Key.Stat.ToUpperInvariant())
            {
                case "PAC":
                    pac += kvp.Value;
                    break;
                case "DAC":
                    dac += kvp.Value;
                    break;
                case "MAC":
                    mac += kvp.Value;
                    break;
                case "SAC":
                    sac += kvp.Value;
                    break;
                case "NAC":
                    nac += kvp.Value;
                    break;
            }
        }

        return new ArmourBonusTotals(pac, dac, mac, sac, nac);
    }

    private static Dictionary<string, int> ResolveArmourValues(AbilityDraft ability)
    {
        var resolved = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (ability == null)
            return resolved;

        if (TryGetTypedStat(ability.AbilityType, out var typedStat))
        {
            var value = ability.Count ?? 0;
            if (value == 0)
            {
                value = ParseArmourTokenValue(ability.Effect, typedStat);
                if (value == 0)
                    value = ParseArmourTokenValue(ability.Name, typedStat);
            }

            if (value != 0)
                resolved[typedStat] = value;

            return resolved;
        }

        var parsed = ParseArmourTokens(ability.Effect);
        if (parsed.Count == 0)
            parsed = ParseArmourTokens(ability.Name);

        return parsed;
    }

    private static bool TryGetTypedStat(AbilityType type, out string stat)
    {
        stat = type switch
        {
            AbilityType.Pac => "PAC",
            AbilityType.Dac => "DAC",
            AbilityType.Mac => "MAC",
            AbilityType.Sac => "SAC",
            _ => string.Empty
        };

        return stat.Length > 0;
    }

    private static Dictionary<string, int> ParseArmourTokens(string? text)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text))
            return result;

        foreach (Match m in ArmourTokenRegex.Matches(text))
        {
            if (!int.TryParse(m.Groups[1].Value, out var value))
                continue;

            if (value == 0)
                continue;

            var key = m.Groups[2].Value.ToUpperInvariant();
            result[key] = result.TryGetValue(key, out var current) ? current + value : value;
        }

        return result;
    }

    private static int ParseArmourTokenValue(string? text, string stat)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        foreach (Match m in ArmourTokenRegex.Matches(text))
        {
            if (!int.TryParse(m.Groups[1].Value, out var value))
                continue;

            var key = m.Groups[2].Value.ToUpperInvariant();
            if (!key.Equals(stat, StringComparison.OrdinalIgnoreCase))
                continue;

            return value;
        }

        return 0;
    }

    private static string BuildSourceKey(AbilityDraft ability)
    {
        var source = (ability.Source ?? string.Empty).Trim();
        if (source.Length > 0)
            return source;

        var name = (ability.Name ?? string.Empty).Trim();
        if (name.Length == 0)
            return "__unknown-source";

        return $"__ability:{ability.AbilityType}:{name}";
    }
}
