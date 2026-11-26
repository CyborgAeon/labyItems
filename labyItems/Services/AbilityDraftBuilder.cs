using System;
using System.Linq;
using System.Collections.Generic;
using labyItems.Helpers;
using labyItems.Models.Abilities;
using labyItems.Models.Characters;

namespace labyItems.Services;

public static class AbilityDraftBuilder
{
    public static List<AbilityDraft> BuildFromLevels(
        Dictionary<string, List<AbilityDefinition>> levels,
        int achievedLevel = 8,
        int achievedTable = 1)
    {
        var resolvedLevel = Math.Max(0, achievedLevel);
        var resolvedTable = Math.Max(1, achievedTable);
        var list = new List<AbilityDraft>();
        foreach (var kvp in levels ?? new Dictionary<string, List<AbilityDefinition>>())
        {
            var key = (kvp.Key ?? string.Empty).Trim();
            var level = default(int?);
            var table = default(int?);

            if (CharacterProgressionTables.TryParseStage(key, out var stage))
            {
                if (stage.Kind == ProgressionStageKind.Level)
                {
                    level = stage.Value;
                    if (stage.Value > resolvedLevel)
                        continue;
                }
                else if (stage.Kind == ProgressionStageKind.Table)
                {
                    table = stage.Value;
                    if (stage.Value > resolvedTable)
                        continue;
                }
            }

            foreach (var ability in kvp.Value ?? new List<AbilityDefinition>())
                list.AddRange(ParseAbility(
                    ability,
                    levelGained: level,
                    achievedLevel: resolvedLevel,
                    tableGained: table,
                    achievedTable: resolvedTable));
        }

        return list;
    }

    public static List<AbilityDraft> ParseAbility(
        string rawAbility,
        int? levelGained,
        int achievedLevel = 8,
        int? tableGained = null,
        int achievedTable = 1)
        => ParseAbility(
            new AbilityDefinition { Name = rawAbility },
            levelGained,
            achievedLevel,
            tableGained,
            achievedTable);

    public static List<AbilityDraft> ParseAbility(
        AbilityDefinition abilityDefinition,
        int? levelGained,
        int achievedLevel = 8,
        int? tableGained = null,
        int achievedTable = 1)
    {
        var result = new List<AbilityDraft>();

        if (abilityDefinition == null)
            return result;

        var abilityName = (abilityDefinition.Name ?? string.Empty).Trim();
        if (abilityName.Length == 0)
            return result;

        var resolvedAchievedLevel = Math.Max(0, achievedLevel);
        var resolvedAchievedTable = Math.Max(1, achievedTable);

        if (levelGained.HasValue && levelGained.Value > resolvedAchievedLevel)
            return result;

        if (tableGained.HasValue && tableGained.Value > resolvedAchievedTable)
            return result;

        var type = ParseDeclaredType(abilityDefinition.Type);
        var draft = CreateDraft(
            abilityDefinition,
            levelGained,
            tableGained,
            resolvedAchievedLevel,
            resolvedAchievedTable,
            type);
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
        int? tableGained,
        int achievedLevel,
        int achievedTable,
        AbilityType type)
    {
        var draft = new AbilityDraft
        {
            AbilityKey = AbilityKey.Build(def),
            GrantInstanceId = def.GrantId,
            GrantDuration = def.GrantId == null ? null : def.Duration,
            GrantSystemEffects = def.GrantId == null ? null : def.SystemEffects?.Select(effect => new AbilitySystemEffect
            {
                EffectType = effect.EffectType,
                DisplayName = effect.DisplayName,
                ResistanceType = effect.ResistanceType,
                Level = effect.Level,
                ImmunityName = effect.ImmunityName
            }).ToList(),
            HasConditionalArmour = def.SystemEffects?.Any(effect =>
                string.Equals(effect.EffectType, "ConditionalArmour", StringComparison.OrdinalIgnoreCase)) == true,
            Name = def.Name ?? string.Empty,
            BattleboardNameOverride = def.BattleboardNameOverride,
            UpdateKey = def.UpdateKey,
            AbilityType = type,
            LevelGained = levelGained,
            ApplicableLevel = levelGained.HasValue ? achievedLevel : null,
            TableGained = tableGained,
            Effect = def.Effect,
            Source = def.Source,
            Count = ResolveCount(def, levelGained, tableGained, achievedLevel, achievedTable),
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

    private static int? ResolveCount(
        AbilityDefinition def,
        int? levelGained,
        int? tableGained,
        int achievedLevel,
        int achievedTable)
    {
        if (def.Progression == null)
            return def.Count;

        if (levelGained.HasValue && achievedLevel < levelGained.Value)
            return 0;

        if (tableGained.HasValue && achievedTable < tableGained.Value)
            return 0;

        if (tableGained.HasValue)
            return def.Progression.ResolveCount(achievedTable);

        return def.Progression.ResolveCount(achievedLevel);
    }

    public static int ResolveInnateRank(
        AbilityDraft? ability, int? achievedLevel = null, IEnumerable<int>? permittedAdditionalLevels = null)
    {
        if (ability == null)
            return 0;

        var applicableLevel = achievedLevel ?? ability.ApplicableLevel ?? 8;
        if (ability.LevelGained is int acquiredAt && applicableLevel < acquiredAt)
            return 0;

        if (ability.Count.HasValue)
            return Math.Max(0, ability.Count.Value);

        if (!TryResolveFrequencySkillRank(ability, out var levels, out var frequency, applicableLevel))
            return 1;

        if (permittedAdditionalLevels != null)
            levels = AbilityQuantityResolver.AddPermittedEffectiveLevels(levels, permittedAdditionalLevels);
        return AbilityQuantityResolver.GetFrequencyIncrements(levels, frequency);
    }

    public static bool TryResolveFrequencySkillRank(
        AbilityDraft? ability,
        out int rank,
        int? achievedLevel = null)
        => TryResolveFrequencySkillRank(ability, out rank, out _, achievedLevel);

    public static bool TryResolveFrequencySkillRank(
        AbilityDraft? ability,
        out int rank,
        out int frequency,
        int? achievedLevel = null)
    {
        rank = 0;
        frequency = 0;

        if (ability?.LevelGained is not int acquiredAt
            || !TryParseFrequency(ability.Frequency, out frequency) || frequency <= 0)
            return false;

        var applicableLevel = achievedLevel ?? ability.ApplicableLevel ?? 8;
        rank = AbilityQuantityResolver.GetEffectiveLevels(applicableLevel, acquiredAt);
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
