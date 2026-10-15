using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using labyItems.Models.Rules;

namespace labyItems.Pages.Search;

internal static class MultiPathSearchFilters
{
    public static IReadOnlyList<MultiPathFilterSectionVm> Build(IEnumerable<RuleClause> rules)
    {
        var groups = new[] { "Alignment", "Races", "Classes", "Multi-classes", "Descriptors" };
        var rows = rules.Where(r => r != null).SelectMany(r => (r.Value ?? []).Select(value => Create(r, value))).Where(x => x.Label.Length > 0)
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToList();
        return groups.Select(group => new MultiPathFilterSectionVm(group, rows.Where(x => x.Group == group).OrderBy(x => x.Label, StringComparer.OrdinalIgnoreCase).Select(x => new MultiPathFilterChipVm(x.Group, x.Key, x.Label)))).Where(x => x.Chips.Count > 0).ToList();
    }

    public static bool Matches(IEnumerable<RuleClause>? rules, IReadOnlySet<string> selectedKeys)
        => selectedKeys.Count == 0 || (rules ?? []).Any(rule => (rule.Value ?? []).Any(value => selectedKeys.Contains(Create(rule, value).Key)));

    private static (string Group, string Key, string Label) Create(RuleClause rule, string rawValue)
    {
        var field = rule.Field?.Trim() ?? string.Empty;
        var value = NormalizeValue(rawValue);
        if (field.Equals("Alignment.Moral", StringComparison.OrdinalIgnoreCase))
            return ("Alignment", $"alignment|{value.ToLowerInvariant()}", value.Equals("Good", StringComparison.OrdinalIgnoreCase) ? "Goodly" : Capitalize(value));
        if (field.Contains("Race", StringComparison.OrdinalIgnoreCase) || (field.Equals("Specialisation", StringComparison.OrdinalIgnoreCase) && string.Equals(rule.SpecialisationKey, "Race Subtype", StringComparison.OrdinalIgnoreCase)))
            return ("Races", $"race|{value.ToLowerInvariant()}", value);
        if (field.Equals("AnyClass", StringComparison.OrdinalIgnoreCase))
            return ("Multi-classes", $"multiclass|{value.ToLowerInvariant()}", value.Replace('-', ' '));
        if (field.Contains("Class", StringComparison.OrdinalIgnoreCase) || field.Equals("Bracket", StringComparison.OrdinalIgnoreCase))
        {
            var bracket = field.Contains("Bracket", StringComparison.OrdinalIgnoreCase) || field.Equals("Bracket", StringComparison.OrdinalIgnoreCase);
            return ("Classes", $"{(bracket ? "class-bracket" : "class")}|{value.ToLowerInvariant()}", bracket ? $"{value} (bracket)" : value.Replace('-', ' '));
        }
        var label = field.Equals("ManaColour", StringComparison.OrdinalIgnoreCase) ? $"{Capitalize(value)} mana user"
            : field.Equals("PriestStyle", StringComparison.OrdinalIgnoreCase) ? $"{SplitWords(value)} priest"
            : value.Replace('-', ' ');
        return ("Descriptors", $"descriptor:{field.ToLowerInvariant()}|{value.ToLowerInvariant()}", label);
    }

    private static string NormalizeValue(string value)
    {
        var clean = value.Trim();
        var space = clean.IndexOf(' ');
        if (clean.Length > 0 && !char.IsLetterOrDigit(clean[0]) && space >= 0) clean = clean[(space + 1)..].Trim();
        return clean switch { "Half-Elf" => "Half Elf", "Half-Ogre" => "Half Ogre", "Half-Orc" => "Half Orc", "Ogres" => "Ogre", "High-Wizard" => "High Wizard", _ => clean };
    }
    private static string SplitWords(string value) => string.Concat(value.Select((c, i) => i > 0 && char.IsUpper(c) ? $" {char.ToLowerInvariant(c)}" : char.ToLowerInvariant(c).ToString()));
    private static string Capitalize(string value) => value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();
}

public sealed class MultiPathFilterSectionVm : INotifyPropertyChanged
{
    private bool _isExpanded = true;
    public MultiPathFilterSectionVm(string title, IEnumerable<MultiPathFilterChipVm> chips) { Title = title; foreach (var chip in chips) Chips.Add(chip); ToggleExpandedCommand = new Command(() => { _isExpanded = !_isExpanded; PropertyChanged?.Invoke(this, new(nameof(IsExpanded))); }); }
    public string Title { get; }
    public ObservableCollection<MultiPathFilterChipVm> Chips { get; } = new();
    public bool IsExpanded => _isExpanded;
    public ICommand ToggleExpandedCommand { get; }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class MultiPathFilterChipVm(string group, string key, string label) : INotifyPropertyChanged
{
    private bool _isSelected;
    public string Group { get; } = group;
    public string Key { get; } = key;
    public string Label { get; } = label;
    public bool IsSelected { get => _isSelected; set { if (_isSelected == value) return; _isSelected = value; PropertyChanged?.Invoke(this, new(nameof(IsSelected))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}
