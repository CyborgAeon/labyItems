using System;
using System.Linq;
using labyItems.Models.Characters;

namespace labyItems.Services;

public static class AbilityDraftBuilder
{
    public static List<AbilityDraft> BuildFromLevels(Dictionary<string, List<AbilityDefinition>> levels)
    {
        var list = new List<AbilityDraft>();
        foreach (var kvp in levels ?? new Dictionary<string, List<AbilityDefinition>>())
        {
            var key = (kvp.Key ?? string.Empty).Trim();
            int? level = int.TryParse(key, out var parsed) ? parsed : null;
            foreach (var ability in kvp.Value ?? new List<AbilityDefinition>())
                list.AddRange(ParseAbility(ability, level));
        }

        return list;
    }

    public static List<AbilityDraft> ParseAbility(string rawAbility, int? levelGained)
        => ParseAbility(new AbilityDefinition { Name = rawAbility }, levelGained);

    public static List<AbilityDraft> ParseAbility(AbilityDefinition abilityDefinition, int? levelGained)
    {
        var result = new List<AbilityDraft>();

        if (abilityDefinition == null)
            return result;

        var abilityName = (abilityDefinition.Name ?? string.Empty).Trim();
        if (abilityName.Length == 0)
            return result;

        var type = ParseDeclaredType(abilityDefinition.Type);
        var draft = CreateDraft(abilityDefinition, levelGained, type);
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

    private static AbilityDraft CreateDraft(AbilityDefinition def, int? levelGained, AbilityType type)
    {
        var draft = new AbilityDraft
        {
            Name = def.Name ?? string.Empty,
            AbilityType = type,
            LevelGained = levelGained,
            Effect = def.Effect,
            Source = def.Source,
            Count = def.Count,
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
}
