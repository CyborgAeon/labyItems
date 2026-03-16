using System.Text.Json;
using labyItems.Models.Characters;
using labyItems.Models.Rules;
using labyItems.Services;

namespace labyItems.Pages.Characters.ViewModels;

internal static class MultiPathWizardHelpers
{
    public static string ResolvePowerBase<TDefinition, TAvailabilityOption, TLevelAbility, TSystemEffect>(
        TDefinition definition)
        where TDefinition : IMultiPathDefinition<TAvailabilityOption, TLevelAbility, TSystemEffect>
        where TLevelAbility : IMultiPathLevelAbility
    {
        foreach (var abilities in definition.Levels.Values)
        {
            foreach (var ability in abilities ?? new List<TLevelAbility>())
            {
                var type = (ability?.Type ?? string.Empty).Trim();
                const string powerPrefix = "LevelOfPower:";
                if (!type.StartsWith(powerPrefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                var powerBase = type.Substring(powerPrefix.Length).Trim();
                if (powerBase.Length > 0)
                    return powerBase;
            }
        }

        return string.Empty;
    }

    public static int ResolveMaxLevel<TDefinition, TAvailabilityOption, TLevelAbility, TSystemEffect>(
        TDefinition definition)
        where TDefinition : IMultiPathDefinition<TAvailabilityOption, TLevelAbility, TSystemEffect>
    {
        if (definition.MaxLevel > 0)
            return definition.MaxLevel;

        var parsed = definition.Levels.Keys
            .Select(level => int.TryParse(level, out var parsedLevel) ? parsedLevel : 0)
            .Where(level => level > 0)
            .DefaultIfEmpty(0)
            .Max();

        return Math.Max(1, parsed);
    }

    public static TAvailabilityOption? FindAvailableOption<TAvailabilityOption>(
        IEnumerable<TAvailabilityOption>? options,
        Func<List<RuleClause>, bool> isAvailable)
        where TAvailabilityOption : IMultiPathAvailabilityOption
    {
        foreach (var option in options ?? Enumerable.Empty<TAvailabilityOption>())
        {
            var rules = option.Rules ?? new List<RuleClause>();
            if (isAvailable(rules))
                return option;
        }

        return default;
    }

    public static Dictionary<int, int> ResolveCostsByLevel<TAvailabilityOption>(TAvailabilityOption option, int maxLevel)
        where TAvailabilityOption : IMultiPathAvailabilityOption
    {
        var map = new Dictionary<int, int>();
        for (var level = 1; level <= maxLevel; level++)
        {
            if (option.CostsByLevel != null
                && option.CostsByLevel.TryGetValue(level.ToString(), out var cost))
            {
                map[level] = Math.Max(0, cost);
            }
            else
            {
                map[level] = 0;
            }
        }

        return map;
    }

    public static IReadOnlyList<TLevelAbility> ResolveAbilitiesForLevel<TDefinition, TAvailabilityOption, TLevelAbility, TSystemEffect>(
        TDefinition definition,
        int level)
        where TDefinition : IMultiPathDefinition<TAvailabilityOption, TLevelAbility, TSystemEffect>
        where TLevelAbility : IMultiPathLevelAbility
    {
        if (!definition.Levels.TryGetValue(level.ToString(), out var list) || list == null)
            return Array.Empty<TLevelAbility>();

        return list
            .Where(ability => ability != null)
            .ToList();
    }

    public static string BuildSummary<TRow>(IReadOnlyList<TRow> detailRows, Func<TRow, string> abilityTextSelector)
    {
        foreach (var row in detailRows)
        {
            var text = abilityTextSelector(row);
            if (string.IsNullOrWhiteSpace(text))
                continue;

            return text;
        }

        return "No abilities listed.";
    }

    public static string BuildAbilityEffectsText<TLevelAbility>(IReadOnlyList<TLevelAbility> abilities)
        where TLevelAbility : IMultiPathLevelAbility
    {
        if (abilities.Count == 0)
            return "No new ability.";

        var lines = new List<string>();
        foreach (var ability in abilities)
        {
            var name = (ability.Name ?? string.Empty).Trim();
            var effect = (ability.Effect ?? string.Empty).Trim();

            if (name.Length == 0 && effect.Length == 0)
                continue;

            if (name.Length == 0)
            {
                lines.Add(effect);
                continue;
            }

            if (effect.Length == 0)
            {
                lines.Add(name);
                continue;
            }

            if (effect.StartsWith(name, StringComparison.OrdinalIgnoreCase))
            {
                lines.Add(effect);
                continue;
            }

            lines.Add($"{name}: {effect}");
        }

        return lines.Count == 0
            ? "No new ability."
            : string.Join("\n", lines);
    }

    public static int ResolveMaxAc<TDefinition, TAvailabilityOption, TLevelAbility, TSystemEffect, TLifeScaleReference>(
        TDefinition definition,
        Func<IEnumerable<RuleClause>?, bool> matchesRules)
        where TDefinition : IMultiPathDefinition<TAvailabilityOption, TLevelAbility, TSystemEffect>
        where TSystemEffect : IMultiPathSystemEffect<TLifeScaleReference>
        where TLifeScaleReference : class, IMultiPathLifeScaleReference
    {
        if (!definition.SystemEffectsByLevel.TryGetValue("1", out var levelEffects) || levelEffects == null)
            return 0;

        foreach (var effect in levelEffects)
        {
            if (!effect.EffectType.Equals("MaxAcOverride", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!matchesRules(effect.Conditions))
                continue;

            if (effect.Value.ValueKind == JsonValueKind.Number && effect.Value.TryGetInt32(out var parsed))
                return parsed;

            if (effect.Value.ValueKind == JsonValueKind.String
                && int.TryParse(effect.Value.GetString(), out parsed))
            {
                return parsed;
            }
        }

        return 0;
    }

    public static async Task<LifeScalePoint?> ResolveLifeForLevelAsync<TDefinition, TAvailabilityOption, TLevelAbility, TSystemEffect, TLifeScaleReference>(
        TDefinition definition,
        int level,
        Func<IEnumerable<RuleClause>?, bool> matchesRules,
        Func<TLifeScaleReference?, int, Task<LifeScalePoint?>> resolveLifeScaleReferenceAsync)
        where TDefinition : IMultiPathDefinition<TAvailabilityOption, TLevelAbility, TSystemEffect>
        where TSystemEffect : IMultiPathSystemEffect<TLifeScaleReference>
        where TLifeScaleReference : class, IMultiPathLifeScaleReference
    {
        if (!definition.SystemEffectsByLevel.TryGetValue(level.ToString(), out var effects) || effects == null)
            return null;

        foreach (var effect in effects)
        {
            if (!matchesRules(effect.Conditions))
                continue;

            if (effect.EffectType.Equals("BaseLife", StringComparison.OrdinalIgnoreCase))
            {
                if (TryParseBaseLifeValue(effect.Value, out var parsedLife))
                    return parsedLife;
            }

            if (effect.EffectType.Equals("BaseLifeFromLifeScale", StringComparison.OrdinalIgnoreCase))
            {
                var lifeFromScale = await resolveLifeScaleReferenceAsync(effect.LifeScaleReference, level);
                if (lifeFromScale.HasValue)
                    return lifeFromScale;
            }
        }

        return null;
    }

    public static bool TryParseBaseLifeValue(JsonElement valueElement, out LifeScalePoint life)
    {
        life = default;

        if (valueElement.ValueKind != JsonValueKind.Object)
            return false;

        var hasTblp = valueElement.TryGetProperty("TBLP", out var tblpElement);
        var hasLoc = valueElement.TryGetProperty("PerLocation", out var locElement);
        if (!hasTblp || !hasLoc)
            return false;

        if (!TryReadInt(tblpElement, out var tblp) || !TryReadInt(locElement, out var loc))
            return false;

        life = new LifeScalePoint(tblp, loc);
        return true;
    }

    private static bool TryReadInt(JsonElement element, out int value)
    {
        value = 0;
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value))
            return true;

        if (element.ValueKind == JsonValueKind.String
            && int.TryParse(element.GetString(), out value))
        {
            return true;
        }

        return false;
    }
}
