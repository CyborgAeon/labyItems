using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using labyItems.Models.Characters;
using labyItems.Services;

namespace labyItems.Pages.Characters;


public sealed class CharacterClassesVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }

    public ICommand ToggleExpandedCommand { get; }

    private readonly ObservableCollection<ClassCardVm> _all = new();
    public ObservableCollection<ClassCardVm> FilteredClasses { get; } = new();

    private string _classSearchText = "";
    public string ClassSearchText
    {
        get => _classSearchText;
        set { if (Set(ref _classSearchText, value)) Refilter(); }
    }

    private string? _selectedRace;
    public string? SelectedRace
    {
        get => _selectedRace;
        set
        {
            if (!Set(ref _selectedRace, value)) return;
            _ = ApplyLifeScalesForSelectedRaceAsync();
        }
    }

    public CharacterClassesVm()
    {
        ToggleExpandedCommand = new Command<ClassCardVm>(ToggleExpanded);
    }

    public async Task LoadAsync()
    {
        _all.Clear();

        var dict = await ClassService.GetAllAsync();
        var id = 1;

        foreach (var kvp in dict)
        {
            var key = kvp.Key;
            var rec = kvp.Value;

            var (icon, category) = ParseBracket(rec.Brackets.FirstOrDefault());

            var maxAc = rec.MaxAC.ValueKind switch
            {
                JsonValueKind.Number => rec.MaxAC.GetInt32(),
                JsonValueKind.String => int.TryParse(rec.MaxAC.GetString(), out var n) ? n : 0,
                _ => 0
            };

            var tblp = rec.PowerPerLevel.ValueKind switch
            {
                JsonValueKind.Number => rec.PowerPerLevel.GetInt32(),
                JsonValueKind.String => int.TryParse(rec.PowerPerLevel.GetString(), out var n) ? n : 0,
                _ => 0
            };

            var levelRows = new ObservableCollection<LevelRowVm>(
                Enumerable.Range(1, 8).Select(lvl =>
                {
                    rec.Levels.TryGetValue(lvl.ToString(), out var arr);
                    arr ??= new List<AbilityDefinition>();

                    var names = arr.Select(ToDisplayName).ToList();

                    var body = names.Count >= 1 ? ExtractNumberToken(names[0]) : "";
                    var loc = names.Count >= 2 ? ExtractNumberToken(names[1]) : "";
                    var skills = names.Count <= 2 ? "" : string.Join(", ", names.Skip(2));

                    return new LevelRowVm
                    {
                        Level = lvl,
                        Body = body,
                        Loc = loc,
                        Skills = skills
                    };
                })
            );

            _all.Add(new ClassCardVm
            {
                Id = id++,
                Key = key,
                Name = key,
                Category = category,
                Icon = icon,
                Summary = BuildSummaryFromLevels(rec.Levels),
                MaxAc = maxAc,
                TBLP = tblp,
                PowerBase = ExtractPowerBase(rec),
                LevelRows = levelRows
            });
        }

        Refilter();
        await ApplyLifeScalesForSelectedRaceAsync();
    }

    private async Task ApplyLifeScalesForSelectedRaceAsync()
    {
        if (_all.Count == 0) return;
        if (string.IsNullOrWhiteSpace(SelectedRace)) return;

        var race = SelectedRace!;
        var tasks = _all.Select(async card =>
        {
            var points = await LifeScalesService.GetLifeScaleAsync(race, card.Name);
            if (points.Count == 0) return;

            for (var i = 0; i < card.LevelRows.Count && i < points.Count; i++)
            {
                card.LevelRows[i].Body = points[i].Body.ToString();
                card.LevelRows[i].Loc = points[i].Loc.ToString();
            }
        });

        await Task.WhenAll(tasks);
        Raise(nameof(FilteredClasses));
    }

    private void ToggleExpanded(ClassCardVm? item)
    {
        if (item == null) return;

        foreach (var c in FilteredClasses)
            if (!ReferenceEquals(c, item) && c.IsExpanded)
                c.IsExpanded = false;

        item.IsExpanded = !item.IsExpanded;
    }

    private void Refilter()
    {
        var q = (ClassSearchText ?? "").Trim().ToLowerInvariant();

        var matches = _all.Where(c =>
            q.Length == 0 ||
            c.Name.ToLowerInvariant().Contains(q) ||
            c.Category.ToLowerInvariant().Contains(q));

        FilteredClasses.Clear();
        foreach (var m in matches)
            FilteredClasses.Add(m);
    }

    private static string BuildSummaryFromLevels(Dictionary<string, List<AbilityDefinition>> levels)
    {
        var firstNonEmpty = levels
            .OrderBy(k => int.TryParse(k.Key, out var n) ? n : 999)
            .SelectMany(k => k.Value ?? new List<AbilityDefinition>())
            .Select(ToDisplayName)
            .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

        return firstNonEmpty ?? "";
    }

    private static (string Icon, string Category) ParseBracket(string? bracket)
    {
        if (string.IsNullOrWhiteSpace(bracket)) return ("🛡️", "warrior");

        var parts = bracket.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var icon = parts.Length >= 1 ? parts[0] : "🛡️";
        var category = parts.Length == 2 ? parts[1] : "warrior";

        return (icon, category.ToLowerInvariant());
    }

    private static string ExtractPowerBase(labyItems.Services.CharacterClassRecord rec)
    {
        var baseName = rec.Powerbase?.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(baseName))
            return baseName;

        return rec.PowerCalculations?.FirstOrDefault()?.PowerBase ?? "";
    }

    private static string ToDisplayName(AbilityDefinition def)
    {
        if (def == null) return string.Empty;

        var name = def.Name ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(def.Effect))
            return $"{name} ({def.Effect})";

        return name;
    }

    private static string ExtractNumberToken(string s)
    {
        var digits = new string(s.Where(char.IsDigit).ToArray());
        return digits;
    }
}
