using System;
using System.Linq;
using System.Collections.Generic;
using labyItems.Models.Characters;

namespace labyItems.Services;

public static class AbilityDraftBuilder
{
    public static List<AbilityDraft> BuildFromLevels(
        Dictionary<string, List<AbilityDefinition>> levels,
        int achievedLevel = 8)
    {
        var resolvedLevel = Math.Max(0, achievedLevel);
        var list = new List<AbilityDraft>();
        foreach (var kvp in levels ?? new Dictionary<string, List<AbilityDefinition>>())
        {
            var key = (kvp.Key ?? string.Empty).Trim();
            int? level = int.TryParse(key, out var parsed) ? parsed : null;

            if (level.HasValue && level.Value > resolvedLevel)
                continue;

            foreach (var ability in kvp.Value ?? new List<AbilityDefinition>())
                list.AddRange(ParseAbility(ability, level, resolvedLevel));
        }

        return list;
    }

    public static List<AbilityDraft> ParseAbility(
        string rawAbility,
        int? levelGained,
        int achievedLevel = 8)
        => ParseAbility(new AbilityDefinition { Name = rawAbility }, levelGained, achievedLevel);

    public static List<AbilityDraft> ParseAbility(
        AbilityDefinition abilityDefinition,
        int? levelGained,
        int achievedLevel = 8)
    {
        var result = new List<AbilityDraft>();

        if (abilityDefinition == null)
            return result;

        var abilityName = (abilityDefinition.Name ?? string.Empty).Trim();
        if (abilityName.Length == 0)
            return result;

        var type = ParseDeclaredType(abilityDefinition.Type);
        var draft = CreateDraft(abilityDefinition, levelGained, Math.Max(0, achievedLevel), type);
        draft.Name = abilityName;
        result.Add(draft);

        return result;
    }

    private static AbilityType ParseDeclaredType(string? declaredType)
    {
        if (!string.IsNullOrWhiteSpace(declaredType))
        {
            var raw = declaredType.Trim();
            if (Enum.TryParse<AbilityType>(raw, ignoreCase: true, out var parsed))
                return parsed;

            var compact = raw.Replace(" ", string.Empty).Replace("-", string.Empty);
            if (Enum.TryParse<AbilityType>(compact, ignoreCase: true, out parsed))
                return parsed;
        }

        return AbilityType.Static;
    }

    private static AbilityDraft CreateDraft(
        AbilityDefinition def,
        int? levelGained,
        int achievedLevel,
        AbilityType type)
    {
        var draft = new AbilityDraft
        {
            Name = def.Name ?? string.Empty,
            BattleboardNameOverride = def.BattleboardNameOverride,
            UpdateKey = def.UpdateKey,
            AbilityType = type,
            LevelGained = levelGained,
            Effect = def.Effect,
            Source = def.Source,
            Count = ResolveCount(def, levelGained, achievedLevel),
            Progression = def.Progression,
            Amount = def.Amount?.ToList(),
            Frequency = def.Frequency,
            OverwriteKey = def.OverwriteKey,
            ShortStringValue = def.Effect ?? def.Name ?? string.Empty
        };

        if (def.PreReqs != null)
            draft.PreReqs.AddRange(def.PreReqs.Where(p => !string.IsNullOrWhiteSpace(p)));
        if (def.GuildOverrides != null)
            draft.GuildOverrides.AddRange(def.GuildOverrides.Where(p => !string.IsNullOrWhiteSpace(p)));

        return draft;
    }

    private static int? ResolveCount(AbilityDefinition def, int? levelGained, int achievedLevel)
    {
        if (def.Progression == null)
            return def.Count;

        if (levelGained.HasValue && achievedLevel < levelGained.Value)
            return 0;

        return def.Progression.ResolveCount(achievedLevel);
    }

    public static int ResolveInnateRank(AbilityDraft? ability, int achievedLevel = 8)
    {
        if (ability == null)
            return 0;

        var baseCount = Math.Max(ability.Count ?? 1, 0);
        var total = baseCount;

        if (TryResolveFrequencySkillRank(ability, out var skillRank, out var frequency, achievedLevel))
        {
            var additional = Math.Max(0, skillRank - 1) / frequency;
            total = baseCount + additional;
        }

        return Math.Clamp(total, 0, 8);
    }

    public static bool TryResolveFrequencySkillRank(
        AbilityDraft? ability,
        out int rank,
        int achievedLevel = 8)
        => TryResolveFrequencySkillRank(ability, out rank, out _, achievedLevel);

    public static bool TryResolveFrequencySkillRank(
        AbilityDraft? ability,
        out int rank,
        out int frequency,
        int achievedLevel = 8)
    {
        rank = 0;
        frequency = 0;

        if (ability == null || !ability.LevelGained.HasValue)
            return false;

        if (!TryParseFrequency(ability.Frequency, out frequency) || frequency <= 0)
            return false;

        var resolvedAchievedLevel = Math.Max(0, achievedLevel);
        rank = Math.Max(0, resolvedAchievedLevel - ability.LevelGained.Value + 1);
        return rank > 0;
    }

    public static bool TryParseFrequency(string? raw, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var text = raw.Trim();
        return int.TryParse(text, out value);
    }
}
