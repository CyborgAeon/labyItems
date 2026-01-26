using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Text.RegularExpressions;
using labyItems.Models.Characters;

namespace labyItems.Pages.Characters;

public sealed class RaceLevelRowVm
{
    public int Level { get; init; }
    public string Abilities { get; init; } = "";
}

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
    public string PeopleType { get; init; } = "";
    public string Description { get; init; } = "";
    public string BuyAsRaw { get; init; } = "";
    public string SearchText { get; set; } = "";

    public string Icon { get; init; } = "👤";

    public ObservableCollection<RaceLevelRowVm> LevelRows { get; } = new();
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

        for (var level = 1; level <= 8; level++)
        {
            var abilities = FindAbilitiesForLevel(levelledAbilities, level);
            if (string.IsNullOrWhiteSpace(abilities))
                continue;

            LevelRows.Add(new RaceLevelRowVm { Level = level, Abilities = abilities });
        }

        HasAnyAbilities = LevelRows.Count > 0;
        Raise(nameof(HasAnyAbilities));

        BuyAsChips.Clear();
        var raw = buyAs ?? "";
        foreach (var chip in SplitBuyAs(raw))
            BuyAsChips.Add(chip);

        Raise(nameof(BuyAsChips));
    }

    private static string FindAbilitiesForLevel(Dictionary<string, List<AbilityDefinition>> dict, int level)
    {
        foreach (var kvp in dict)
        {
            var lvl = ExtractLevel(kvp.Key);
            if (lvl != level) continue;

            var list = kvp.Value ?? new List<AbilityDefinition>();
            var text = string.Join(", ", list.Select(ToDisplayName).Where(x => !string.IsNullOrWhiteSpace(x)));
            return text;
        }

        return "";
    }

    private static int? ExtractLevel(string key)
    {
        if (int.TryParse(key, out var n)) return n;
        var m = Regex.Match(key ?? "", "\\d+");
        return m.Success && int.TryParse(m.Value, out n) ? n : null;
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

    private static string ToDisplayName(AbilityDefinition def)
    {
        if (def == null) return string.Empty;

        var name = def.Name ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(def.Effect))
            return $"{name} ({def.Effect})";

        return name;
    }
}
