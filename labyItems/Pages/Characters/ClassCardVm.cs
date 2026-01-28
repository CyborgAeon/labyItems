using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;
using System.Text.RegularExpressions;
using labyItems.Models.Characters;
using labyItems.Services;
using labyItems.Models.Enums;

namespace labyItems.Pages.Characters;

public sealed class CharacterClassRecord
{
    public List<string> Brackets { get; set; } = new();
    public Dictionary<string, List<AbilityDefinition>> Levels { get; set; } = new();
    [JsonPropertyName("Max AC")]
    public JsonElement MaxAC { get; set; }
    public List<string>? Powerbase { get; set; }
    public JsonElement PowerPerLevel { get; set; }
    public int? CasterLevel { get; set; }
    public List<PowerCalculation>? PowerCalculations { get; set; }
    [JsonPropertyName("Buy as")]
    public List<string>? BuyAs { get; set; }
    public GuildOverrideRules? GuildOverrides { get; set; }
}

public sealed class LevelRowVm
{
    public int Level { get; init; }
    public string Body { get; set; } = "";
    public string Loc { get; set; } = "";
    public string Skills { get; set; } = "";
}

public sealed class ClassCardVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
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
    public int Id { get; set; }
    public string Key { get; init; } = "";
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public string Icon { get; init; } = "🛡️";
    public string Summary { get; init; } = "";
    public int MaxAc { get; init; }
    public int TBLP { get; init; }
    public string? PowerBase { get; init; } = "";
    public IReadOnlyList<string> BracketTags { get; init; } = Array.Empty<string>();

    public Dictionary<int, string> CardTags { get; set; }
    public string Tag1 => $"AC {MaxAc}";
    public string Tag2 => ResolveTags().Tag2;
    public string Tag3 => ResolveTags().Tag3;

    public ObservableCollection<LevelRowVm> LevelRows { get; init; } = new();

    public string RaceName { get; set; } = "";

    private bool _progressionLoaded;

    public async Task EnsureProgressionLoadedAsync()
    {
        if (_progressionLoaded) return;
        _progressionLoaded = true;

        var abilitiesByLevel = await GetAbilitiesByLevelAsync();
        var lifeByLevel = await GetLifeByLevelAsync();

        LevelRows.Clear();
        for (var level = 1; level <= 8; level++)
        {
            var hasLife = lifeByLevel.TryGetValue(level, out var p);
            var skills = abilitiesByLevel.TryGetValue(level, out var s) ? s : "";

            LevelRows.Add(new LevelRowVm
            {
                Level = level,
                Body = hasLife ? p.Body.ToString() : "",
                Loc = hasLife ? p.Loc.ToString() : "",
                Skills = skills
            });
        }
    }

    public Task ReloadProgressionAsync()
    {
        _progressionLoaded = false;
        return EnsureProgressionLoadedAsync();
    }

    private async Task<Dictionary<int, string>> GetAbilitiesByLevelAsync()
    {
        var result = new Dictionary<int, string>();
        var all = await ClassService.GetAllAsync();
        var wanted = LifeScalesService.NormalizeKey(string.IsNullOrWhiteSpace(Key) ? Name : Key);

        var classKey = all.Keys.FirstOrDefault(k => LifeScalesService.NormalizeKey(k) == wanted)
                       ?? all.Keys.FirstOrDefault(k => LifeScalesService.NormalizeKey(k).Contains(wanted) || wanted.Contains(LifeScalesService.NormalizeKey(k)));

        if (classKey == null) return result;
        if (!all.TryGetValue(classKey, out var record) || record?.Levels == null) return result;

        foreach (var kvp in record.Levels)
        {
            var level = ExtractLevel(kvp.Key);
            if (level is < 1 or > 8) continue;

            var text = kvp.Value == null
                ? ""
                : string.Join(", ", kvp.Value.Select(ToDisplayName).Where(x => !string.IsNullOrWhiteSpace(x)));
            if (text.Length > 0)
                result[level.Value] = text;
        }

        return result;
    }
    private async Task<Dictionary<int, LifeScalePoint>> GetLifeByLevelAsync()
    {
        var result = new Dictionary<int, LifeScalePoint>();

        var className = string.IsNullOrWhiteSpace(Key) ? Name : Key;
        var points = await LifeScalesService.GetLifeScaleAsync(RaceName, className);

        for (var i = 0; i < points.Count && i < 8; i++)
            result[i + 1] = points[i];

        return result;
    }
    private static int? ExtractLevel(string key)
    {
        if (int.TryParse(key, out var n)) return n;
        var m = Regex.Match(key ?? "", "\\d+");
        return m.Success && int.TryParse(m.Value, out n) ? n : null;
    }

    private static string ToDisplayName(AbilityDefinition def)
    {
        if (def == null) return string.Empty;

        var name = def.Name ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(def.Effect))
            return $"{name} ({def.Effect})";

        return name;
    }

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set { if (_isExpanded == value) return; _isExpanded = value; Raise(); }
    }

    // private Dictionary<int, string> ResolveTags()
    private (string Tag2, string Tag3) ResolveTags()
    {
        // var response = new Dictionary<int, string>();
        // response.Add(0, Tags[0].ToString() ?? $"{TBLP} Tblp");
        // response.Add(1, Tags[1].ToString() ?? $"{MaxAc} max AC");
        // response.Add(2, Tags[2].ToString() ?? PowerBase.ToString());
        var tag2 = PowerBase ?? "";
        var tag3 = TBLP > 0 ? $"{TBLP} TBLP" : "";

        if (!string.IsNullOrWhiteSpace(tag2) && !string.IsNullOrWhiteSpace(tag3))
            return (tag2, tag3);

        var tags = BracketTags?
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        if (tags.Count > 1)
        {
            var primary = tags[0];
            tags.RemoveAt(0);
            tags.Add(primary);
        }

        var idx = 0;
        if (string.IsNullOrWhiteSpace(tag2) && idx < tags.Count)
            tag2 = tags[idx++];

        if (string.IsNullOrWhiteSpace(tag3) && idx < tags.Count)
            tag3 = tags[idx++];

        return (tag2, tag3);
    }
}
