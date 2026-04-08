using System.Collections.Generic;
using System.Linq;
using labyItems.Models.Characters;
using labyItems.Models.ViewModels;

namespace labyItems.Helpers;

public static class WizardAbilityProgressionHelper
{
    public static IReadOnlyList<AbilityDefinition> GetAbilitiesForLevel(
        Dictionary<string, List<AbilityDefinition>>? levels,
        int level)
    {
        if (levels == null || levels.Count == 0)
            return Array.Empty<AbilityDefinition>();

        foreach (var kvp in levels)
        {
            var parsedLevel = ExtractLevel(kvp.Key);
            if (parsedLevel != level)
                continue;

            var abilities = kvp.Value?
                .Where(def => def != null)
                .ToList();

            return abilities ?? new List<AbilityDefinition>();
        }

        return Array.Empty<AbilityDefinition>();
    }

    public static IReadOnlyList<LevelAbilityRowVm> BuildRaceStageRows(
        Dictionary<string, List<AbilityDefinition>>? levels)
    {
        if (levels == null || levels.Count == 0)
            return Array.Empty<LevelAbilityRowVm>();

        var grouped = new Dictionary<(ProgressionStageKind Kind, int Value), List<AbilityDefinition>>();

        foreach (var entry in levels)
        {
            if (!CharacterProgressionTables.TryParseStage(entry.Key, out var stage)
                || !stage.IsValid)
            {
                continue;
            }

            var key = (stage.Kind, stage.Value);
            if (!grouped.TryGetValue(key, out var list))
            {
                list = new List<AbilityDefinition>();
                grouped[key] = list;
            }

            foreach (var ability in entry.Value ?? Enumerable.Empty<AbilityDefinition>())
            {
                if (ability != null)
                    list.Add(ability);
            }
        }

        return grouped
            .OrderBy(entry => entry.Key.Kind == ProgressionStageKind.Table ? 1 : 0)
            .ThenBy(entry => entry.Key.Value)
            .Select(entry => LevelAbilityRowBuilder.Build(
                level: entry.Key.Value,
                abilityDefinitions: entry.Value,
                isTableStage: entry.Key.Kind == ProgressionStageKind.Table))
            .ToList();
    }

    public static int? ExtractLevel(string? key)
        => CharacterProgressionTables.TryParseStage(key, out var stage)
           && stage.Kind == ProgressionStageKind.Level
            ? stage.Value
            : null;
}
