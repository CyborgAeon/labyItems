using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Text.RegularExpressions;
using labyItems.Helpers;
using labyItems.Models.Characters;
using labyItems.Models.ViewModels;

namespace labyItems.Pages.Characters;

public sealed class RaceCardVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            Raise();
        }
    }


    public int Id { get; init; }
    public string Name { get; init; } = "";
    public IReadOnlyList<string> PeopleTypes { get; init; } = Array.Empty<string>();
    public string PeopleType { get; init; } = "";
    public bool IsNonStandard { get; init; }
    public string Description { get; init; } = "";
    public string BuyAsRaw { get; init; } = "";
    public string SearchText { get; set; } = "";

    public string Icon { get; init; } = "👤";
    public string PeopleTypeDisplay => IsNonStandard
        ? string.IsNullOrWhiteSpace(PeopleType) ? "Non-standard" : $"{PeopleType} • Non-standard"
        : PeopleType;

    public ObservableCollection<LevelAbilityRowVm> LevelRows { get; } = new();
    public ObservableCollection<string> BuyAsChips { get; } = new();

    public bool HasAnyAbilities { get; private set; }

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            Raise();
        }
    }

    public void BuildRowsAndChips(Dictionary<string, List<AbilityDefinition>> levelledAbilities, string? buyAs)
    {
        LevelRows.Clear();

        foreach (var stageRow in BuildOrderedStageRows(levelledAbilities))
        {
            LevelRows.Add(stageRow);
        }

        HasAnyAbilities = LevelRows.Count > 0;
        Raise(nameof(HasAnyAbilities));

        BuyAsChips.Clear();
        var raw = buyAs ?? "";
        foreach (var chip in SplitBuyAs(raw))
            BuyAsChips.Add(chip);

        Raise(nameof(BuyAsChips));
    }

    private static IReadOnlyList<LevelAbilityRowVm> BuildOrderedStageRows(
        Dictionary<string, List<AbilityDefinition>> dict)
    {
        var grouped = new Dictionary<(ProgressionStageKind Kind, int Value), List<AbilityDefinition>>();

        foreach (var kvp in dict)
        {
            if (!CharacterProgressionTables.TryParseStage(kvp.Key, out var stage)
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

            foreach (var ability in kvp.Value ?? Enumerable.Empty<AbilityDefinition>())
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

    private static IEnumerable<string> SplitBuyAs(string s)
    {
        var t = (s ?? "").Trim();
        if (t.Length == 0) yield break;

        var parts = Regex.Split(t, "\\s*;\\s*|\\s*,\\s*")
                         .Select(x => x.Trim())
                         .Where(x => x.Length > 0)
                         .ToList();

        if (parts.Count == 0)
        {
            yield return t;
            yield break;
        }

        foreach (var p in parts)
            yield return p;
    }
}
