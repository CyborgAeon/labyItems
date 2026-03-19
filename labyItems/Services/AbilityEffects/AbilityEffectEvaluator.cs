using System.Linq;
using System.Text.RegularExpressions;
using labyItems.Models.Abilities.Effects;
using labyItems.Models.Characters;

namespace labyItems.Services.AbilityEffects;

public static class AbilityEffectEvaluator
{
    private static readonly Regex FirstIntRegex = new(
        @"-?\d+",
        RegexOptions.Compiled);

    public static AbilityEffectEvaluationResult Evaluate(
        CharacterDraft draft,
        IEnumerable<AbilityEffectInstance> effects)
    {
        var result = new AbilityEffectEvaluationResult();
        var immunities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var effect in effects ?? Array.Empty<AbilityEffectInstance>())
        {
            if (effect == null || effect.Instruction == null)
                continue;

            if (!IsConditionMet(draft, effect.Condition))
                continue;

            ApplyInstruction(
                effect.Instruction,
                result.ResistanceOverrides,
                immunities,
                result.ResistanceMultipliers,
                result.InfiniteResistanceTypes);
        }

        foreach (var immunity in immunities.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            result.Immunities.Add(immunity);

        return result;
    }

    public static BattleboardAdvancementEffects EvaluateResistanceImmunities(
        IEnumerable<AbilityEffectInstruction> instructions)
    {
        var instances = (instructions ?? Array.Empty<AbilityEffectInstruction>())
            .Where(instruction => instruction != null)
            .Select(instruction => new AbilityEffectInstance(
                AbilityKey: string.Empty,
                Instruction: instruction,
                Condition: EffectCondition.Always))
            .ToList();

        var evaluated = Evaluate(new CharacterDraft(), instances);
        return new BattleboardAdvancementEffects(
            evaluated.ResistanceOverrides,
            evaluated.Immunities,
            evaluated.ResistanceMultipliers,
            evaluated.InfiniteResistanceTypes);
    }

    public static void ApplyInstructions(
        IEnumerable<AbilityEffectInstruction> instructions,
        IDictionary<string, int> resistance,
        ISet<string> immunities,
        IDictionary<string, int>? resistanceMultipliers = null,
        ISet<string>? infiniteResistanceTypes = null)
    {
        foreach (var instruction in instructions ?? Array.Empty<AbilityEffectInstruction>())
            ApplyInstruction(
                instruction,
                resistance,
                immunities,
                resistanceMultipliers,
                infiniteResistanceTypes);
    }

    public static IEnumerable<AbilityEffectInstruction> FromSystemEffects(
        IEnumerable<AbilitySystemEffect> effects)
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
                var resistanceType = !string.IsNullOrWhiteSpace(effect.ResistanceType)
                    ? effect.ResistanceType
                    : token;

                var level = effect.Level ?? TryParseFirstInt(effect.DisplayName);
                if (level > 0)
                {
                    yield return new AbilityEffectInstruction(
                        AbilityEffectInstructionKind.ResistanceLevelOverride,
                        resistanceType,
                        level,
                        ImmunityName: null);
                }

                continue;
            }

            if (effectType.StartsWith("lor-multiplier:", StringComparison.OrdinalIgnoreCase)
                || effectType.StartsWith("lor-mult:", StringComparison.OrdinalIgnoreCase))
            {
                var token = effectType[(effectType.IndexOf(':') + 1)..].Trim();
                var resistanceType = !string.IsNullOrWhiteSpace(effect.ResistanceType)
                    ? effect.ResistanceType
                    : token;

                var multiplier = effect.Level ?? TryParseFirstInt(effect.DisplayName);
                if (multiplier > 1)
                {
                    yield return new AbilityEffectInstruction(
                        AbilityEffectInstructionKind.ResistanceLevelMultiplier,
                        resistanceType,
                        multiplier,
                        ImmunityName: null);
                }

                continue;
            }

            if (effectType.StartsWith("lor-infinite:", StringComparison.OrdinalIgnoreCase)
                || effectType.Equals("lor-infinite", StringComparison.OrdinalIgnoreCase)
                || effectType.Equals("spiritless", StringComparison.OrdinalIgnoreCase)
                || effectType.Equals("mindless", StringComparison.OrdinalIgnoreCase))
            {
                var infiniteType = ResolveInfiniteResistanceType(effectType, effect.ResistanceType);
                if (infiniteType.Length == 0)
                    continue;

                yield return new AbilityEffectInstruction(
                    AbilityEffectInstructionKind.ResistanceInfinite,
                    ResistanceType: infiniteType,
                    Level: null,
                    ImmunityName: null);
                continue;
            }

            if (effectType.Equals("immunity", StringComparison.OrdinalIgnoreCase))
            {
                var display = (effect.DisplayName ?? string.Empty).Trim();
                var target = (effect.ImmunityName ?? string.Empty).Trim();

                if (display.Length == 0 && target.Length > 0)
                    display = $"Immunity to {target}";

                if (display.Length > 0)
                {
                    yield return new AbilityEffectInstruction(
                        AbilityEffectInstructionKind.Immunity,
                        ResistanceType: null,
                        Level: null,
                        ImmunityName: display);
                }

                continue;
            }
        }
    }

    private static bool IsConditionMet(CharacterDraft draft, EffectCondition? condition)
    {
        _ = draft;
        // Phase 1/2 behaviour: conditions are stored but not yet evaluated.
        // This method intentionally returns true for now so existing behaviour is unchanged.
        return condition == null || condition == EffectCondition.Always || condition.Field == null;
    }

    private static void ApplyInstruction(
        AbilityEffectInstruction? instruction,
        IDictionary<string, int> resistance,
        ISet<string> immunities,
        IDictionary<string, int>? resistanceMultipliers,
        ISet<string>? infiniteResistanceTypes)
    {
        if (instruction == null)
            return;

        switch (instruction.Kind)
        {
            case AbilityEffectInstructionKind.ResistanceLevelOverride:
            {
                var resistanceType = NormalizeResistanceType(instruction.ResistanceType);
                if (string.IsNullOrWhiteSpace(resistanceType))
                    return;

                var level = instruction.Level ?? 0;
                SetResistanceLevel(resistance, resistanceType, level);
                return;
            }
            case AbilityEffectInstructionKind.ResistanceLevelMultiplier:
            {
                var resistanceType = NormalizeResistanceType(instruction.ResistanceType);
                if (string.IsNullOrWhiteSpace(resistanceType))
                    return;

                var multiplier = instruction.Level ?? 0;
                if (multiplier <= 1 || resistanceMultipliers == null)
                    return;

                if (!resistanceMultipliers.TryGetValue(resistanceType, out var current)
                    || multiplier > current)
                {
                    resistanceMultipliers[resistanceType] = multiplier;
                }
                return;
            }
            case AbilityEffectInstructionKind.ResistanceInfinite:
            {
                if (infiniteResistanceTypes == null)
                    return;

                var resistanceType = NormalizeResistanceType(instruction.ResistanceType);
                if (string.IsNullOrWhiteSpace(resistanceType))
                    return;

                infiniteResistanceTypes.Add(resistanceType);
                return;
            }
            case AbilityEffectInstructionKind.Immunity:
            {
                var immunityName = (instruction.ImmunityName ?? string.Empty).Trim();
                if (immunityName.Length == 0)
                    return;

                immunities.Add(immunityName);
                return;
            }
            default:
                return;
        }
    }

    private static void SetResistanceLevel(
        IDictionary<string, int> resistance,
        string resistanceType,
        int level)
    {
        if (resistanceType.Length == 0 || level <= 0)
            return;

        if (!resistance.TryGetValue(resistanceType, out var existing) || level > existing)
            resistance[resistanceType] = level;
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

    private static int TryParseFirstInt(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var match = FirstIntRegex.Match(text);
        if (match.Success && int.TryParse(match.Value, out var parsed))
            return parsed;

        return 0;
    }

    private static string ResolveInfiniteResistanceType(string effectType, string? explicitResistanceType)
    {
        if (effectType.Equals("spiritless", StringComparison.OrdinalIgnoreCase))
            return "Spirit";

        if (effectType.Equals("mindless", StringComparison.OrdinalIgnoreCase))
            return "Neuro";

        if (effectType.StartsWith("lor-infinite:", StringComparison.OrdinalIgnoreCase))
        {
            var token = effectType[(effectType.IndexOf(':') + 1)..].Trim();
            var normalized = NormalizeResistanceType(token);
            if (normalized.Length > 0)
                return normalized;
        }

        var fallback = NormalizeResistanceType(explicitResistanceType);
        if (fallback.Length > 0)
            return fallback;

        // Preserve legacy behaviour where bare `lor-infinite` implies Spirit.
        return "Spirit";
    }
}
