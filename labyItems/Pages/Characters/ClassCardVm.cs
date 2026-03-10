using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Linq;
using System.Windows.Input;
using System.Text.RegularExpressions;
using labyItems.Models.Characters;
using labyItems.Services;
using labyItems.Models.Enums;

namespace labyItems.Pages.Characters;

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
    public bool IsNonStandard { get; init; }

    public Dictionary<int, string> CardTags { get; set; }
    public string Tag1 => $"AC {MaxAc}";
    public string Tag2 => ResolveTags().Tag2;
    public string Tag3 => ResolveTags().Tag3;
    public IReadOnlyList<string> TagChips => BuildTagChips();

    public ObservableCollection<LevelRowVm> LevelRows { get; init; } = new();

    public string RaceName { get; set; } = "";

    private bool _progressionLoaded;
    public IReadOnlyList<string> BracketTags { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Brackets => BracketTags
        .Where(t => !string.IsNullOrWhiteSpace(t))
        .Select(t => t.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    public IReadOnlyList<string> BracketLabels => Brackets
        .Select(GetBracketLabel)
        .Where(t => !string.IsNullOrWhiteSpace(t))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    public string BracketSubheading => BracketLabels.Count == 0
        ? Category
        : string.Join(" / ", BracketLabels);

    public bool HasSplitIcon => Brackets.Count >= 2;

    public string SingleEmoji => Brackets.Count == 0 ? Icon : GetBracketEmoji(Brackets[0]);
    public string SingleBg => GetBracketColor(Brackets.Count == 0 ? Category : Brackets[0]);

    public string SplitLeftEmoji => GetBracketEmoji(GetBracketAt(0, Category));
    public string SplitRightEmoji => GetBracketEmoji(GetBracketAt(1, GetBracketAt(0, Category)));

    public string SplitLeftBg => GetBracketColor(GetBracketAt(0, Category));
    public string SplitRightBg => GetBracketColor(GetBracketAt(1, GetBracketAt(0, Category)));

    private string GetBracketAt(int index, string fallback)
    {
        if (index >= 0 && index < Brackets.Count)
            return Brackets[index];
        return fallback;
    }

    private static string GetBracketColor(string? bracket)
    {
        var b = GetBracketLabel(bracket);

        if (b.Equals("Neuro", StringComparison.OrdinalIgnoreCase)) return "#E9D5FF";
        if (b.Equals("Wizard", StringComparison.OrdinalIgnoreCase)) return "#D8E2DC";
        if (b.Equals("Warrior", StringComparison.OrdinalIgnoreCase)) return "#FEC5BB";
        if (b.Equals("Priest", StringComparison.OrdinalIgnoreCase)) return "#FAE1DD";
        if (b.Equals("Druid", StringComparison.OrdinalIgnoreCase)) return "#DED6CE";
        if (b.Equals("Scout", StringComparison.OrdinalIgnoreCase)) return "#F5EBE0";

        return "#F3F4F6";
    }

    private static string GetBracketEmoji(string? bracket)
    {
        var (emoji, label) = ParseBracketParts(bracket);
        if (!string.IsNullOrWhiteSpace(emoji))
            return emoji;

        var b = label;

        if (b.Equals("Neuro", StringComparison.OrdinalIgnoreCase)) return "🧠";
        if (b.Equals("Wizard", StringComparison.OrdinalIgnoreCase)) return "🪄";
        if (b.Equals("Warrior", StringComparison.OrdinalIgnoreCase)) return "⚔️";
        if (b.Equals("Priest", StringComparison.OrdinalIgnoreCase)) return "✨";
        if (b.Equals("Druid", StringComparison.OrdinalIgnoreCase)) return "🌿";
        if (b.Equals("Scout", StringComparison.OrdinalIgnoreCase)) return "🛡️";

        return "❔";
    }

    private static string GetBracketLabel(string? bracket)
    {
        var (_, label) = ParseBracketParts(bracket);
        return string.IsNullOrWhiteSpace(label) ? string.Empty : label;
    }

    private static (string Emoji, string Label) ParseBracketParts(string? bracket)
    {
        var text = (bracket ?? string.Empty).Trim();
        if (text.Length == 0)
            return (string.Empty, string.Empty);

        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1 && LooksLikeEmoji(parts[0]))
            return (parts[0], string.Join(" ", parts.Skip(1)));

        return (string.Empty, text);
    }

    private static bool LooksLikeEmoji(string token)
    {
        foreach (var ch in token)
        {
            if (!char.IsLetterOrDigit(ch))
                return true;
        }

        return false;
    }

    public static (string Icon, string Category, IReadOnlyList<string> Tags) ParseBrackets(IReadOnlyList<string>? brackets)
    {
        if (brackets == null || brackets.Count == 0)
            return ("🛡️", "warrior", Array.Empty<string>());

        var parsed = new List<(string Icon, string Category, string Tag)>();
        foreach (var raw in brackets)
        {
            var entry = ParseBracketToken(raw);
            if (string.IsNullOrWhiteSpace(entry.Tag))
                continue;
            parsed.Add(entry);
        }

        if (parsed.Count == 0)
            return ("🛡️", "warrior", Array.Empty<string>());

        var primary = parsed[0];
        var tags = parsed
            .Select(p => p.Tag)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return (primary.Icon, primary.Category, tags);
    }

    private static (string Icon, string Category, string Tag) ParseBracketToken(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return ("🛡️", "warrior", string.Empty);

        var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return ("🛡️", "warrior", string.Empty);

        if (parts.Length == 1)
        {
            var token = parts[0].Trim();
            return ("🛡️", token, token);
        }

        var icon = parts[0];
        var category = string.Join(" ", parts.Skip(1));
        var tag = $"{icon} {category}".Trim();

        return (string.IsNullOrWhiteSpace(icon) ? "🛡️" : icon, category, tag);
    }

    public static string BuildSummaryFromLevels(Dictionary<string, List<AbilityDefinition>> levels)
    {
        var firstNonEmpty = levels
            .OrderBy(k => int.TryParse(k.Key, out var n) ? n : 999)
            .SelectMany(k => k.Value ?? new List<AbilityDefinition>())
            .Select(ToDisplayName)
            .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

        return firstNonEmpty ?? "";
    }

    public static string ExtractPowerBase(labyItems.Services.CharacterClassRecord rec)
    {
        var baseName = rec.Powerbase?.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(baseName))
            return baseName;

        return rec.PowerCalculations?.FirstOrDefault()?.PowerBase ?? "";
    }
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

    public void MarkProgressionDirty()
    {
        _progressionLoaded = false;
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

    public static string ToDisplayName(AbilityDefinition def)
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

    private (string Tag2, string Tag3) ResolveTags()
    {
        var tag2 = PowerBase ?? "";
        var tag3 = TBLP > 0 ? $"{TBLP} TBLP" : "";

        if (!string.IsNullOrWhiteSpace(tag2) && !string.IsNullOrWhiteSpace(tag3))
            return (tag2, tag3);

        var tags = BracketLabels
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

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

    private IReadOnlyList<string> BuildTagChips()
    {
        var chips = new[] { Tag1, Tag2, Tag3 }
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (IsNonStandard)
            chips.Add("Non-standard");

        return chips;
    }
}
