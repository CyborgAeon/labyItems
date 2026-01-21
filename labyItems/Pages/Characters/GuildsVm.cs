using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using labyItems.Models.Characters;
using labyItems.Services;
using Microsoft.Maui.ApplicationModel;
using ServiceCharacterClassRecord = labyItems.Services.CharacterClassRecord;

namespace labyItems.Pages.Characters;

public sealed class GuildsVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private Dictionary<string, GuildRecord> _guildRecords =
        new(StringComparer.OrdinalIgnoreCase);
    private GuildSlotRules _slotRules = GuildSlotRules.Default();
    private bool _classAllowsChurch;
    private readonly Func<Task>? _refreshDraftAbilitiesAsync;
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }

    private readonly CharacterDraft _draft;
    private readonly Action _notifyWizardGatingChanged;

    public CharacterDraft Draft => _draft;
    private readonly Func<IEnumerable<AlignmentRule?>> _getNonGuildRules;
    public GuildsVm(CharacterDraft draft, Action notifyWizardGatingChanged, Func<IEnumerable<AlignmentRule?>>? getNonGuildRules = null, Func<Task>? refreshDraftAbilitiesAsync = null)
    {
        _draft = draft;
        _notifyWizardGatingChanged = notifyWizardGatingChanged;
        _getNonGuildRules = getNonGuildRules ?? (() => Enumerable.Empty<AlignmentRule?>());
        _refreshDraftAbilitiesAsync = refreshDraftAbilitiesAsync;

        TypeFilters = new ObservableCollection<string> { "All" };
        _selectedTypeFilter = "All";

        AllGuilds = new ObservableCollection<GuildCardVm>();
        FilteredGuilds = new ObservableCollection<GuildCardVm>();

        ToggleExpandedCommand = new Command<GuildCardVm>(ToggleExpanded);
        ToggleSelectedCommand = new Command<GuildCardVm>(ToggleSelected);

        SelectTypeFilterCommand = new Command<string>(s =>
        {
            SelectedTypeFilter = string.IsNullOrWhiteSpace(s) ? "All" : s;
        });

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await ReloadAsync();
        });
    }

    public ObservableCollection<string> TypeFilters { get; }

    private string? _selectedTypeFilter;
    public string? SelectedTypeFilter
    {
        get => _selectedTypeFilter;
        set { if (Set(ref _selectedTypeFilter, value)) Refilter(); }
    }

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set { if (Set(ref _searchText, value)) Refilter(); }
    }

    public ObservableCollection<GuildCardVm> AllGuilds { get; }
    public ObservableCollection<GuildCardVm> FilteredGuilds { get; }

    public ICommand ToggleExpandedCommand { get; }
    public ICommand ToggleSelectedCommand { get; }
    public ICommand SelectTypeFilterCommand { get; }

    public async Task ReloadAsync()
    {
        _classAllowsChurch = await ClassAllowsChurchAsync();
        _slotRules = GuildSlotRules.FromAbilities(_draft.Abilities);

        _guildRecords = await GuildsService.GetAllAsync() ?? new Dictionary<string, GuildRecord>(StringComparer.OrdinalIgnoreCase);

        _slotRules.ApplyToCurrentSelection(_draft, _guildRecords);

        if (!_classAllowsChurch)
        {
            var toRemove = _draft.Guilds.Where(IsChurchGuild).ToList();
            foreach (var g in toRemove)
                _draft.Guilds.Remove(g);
        }

        var (hasCityBound, cityName) = GetCityBoundInfo();
        if (hasCityBound && !string.IsNullOrWhiteSpace(cityName))
        {
            var matchedGuildName = _guildRecords.Keys
                .FirstOrDefault(k => string.Equals(k, cityName, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(matchedGuildName))
            {
                if (!_draft.Guilds.Any(g => string.Equals(g, matchedGuildName, StringComparison.OrdinalIgnoreCase)))
                    _draft.Guilds.Add(matchedGuildName);
            }
        }
        var types = await GuildsService.GetTypesAsync();
        TypeFilters.Clear();
        TypeFilters.Add("All");
        foreach (var t in types)
            TypeFilters.Add(t);

        if (string.IsNullOrWhiteSpace(SelectedTypeFilter) || !TypeFilters.Contains(SelectedTypeFilter))
            SelectedTypeFilter = "All";

        var ordered = _guildRecords
        .Where(kv =>
        {
            var type = (kv.Value?.Type ?? "").Trim();
            var isCity = type.Equals("City", StringComparison.OrdinalIgnoreCase);

            if (!_slotRules.ShouldShowGuild(type, kv.Key, _draft.Guilds))
                return false;

            if (!_classAllowsChurch && IsChurchGuild(kv.Key))
                return _draft.Guilds.Contains(kv.Key, StringComparer.OrdinalIgnoreCase);

            return true;
        })
        .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
        .ToList();
        AllGuilds.Clear();
        for (var i = 0; i < ordered.Count; i++)
        {
            var name = ordered[i].Key;
            var rec = ordered[i].Value ?? new GuildRecord();

            var selectable = WouldStillHaveAnyAlignmentIfSelected(name);
            var vm = new GuildCardVm
            {
                Id = i + 1,
                Name = name,
                Type = rec.Type ?? "",
                Restrictions = rec.Restrictions ?? "",
                BasicBenefits = rec.Benefits?.Basic?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? new(),
                IntermediateBenefits = rec.Benefits?.Intermediate?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? new(),
                AdvancedBenefits = rec.Benefits?.Advanced?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? new(),
                IsSelected = _draft.Guilds.Contains(name, StringComparer.OrdinalIgnoreCase),
                IsExpanded = false,
                IsSelectable = selectable,
                NotSelectableReason = selectable ? "" : "Conflicts with current alignment restrictions.",
                IsLocked = _slotRules.IsGuildLocked(type: rec.Type ?? string.Empty, guildName: name),
            };

            vm.Icon = IconForType(vm.Type);
            AllGuilds.Add(vm);
        }

        Refilter();
        RecomputeDraftAlignments();
        _notifyWizardGatingChanged();
    }

    private (bool HasCityBound, string? CityName) GetCityBoundInfo()
    {
        var match = _draft.Abilities
            .Select(a => a?.Name ?? "")
            .FirstOrDefault(n => n.Contains("city bound", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(match))
            return (false, null);

        var lower = match.ToLowerInvariant();
        if (!lower.Contains("city bound"))
            return (false, null);
        var open = match.IndexOf('(');
        var close = match.LastIndexOf(')');
        if (open >= 0 && close > open)
        {
            var city = match.Substring(open + 1, close - open - 1).Trim();
            if (!string.IsNullOrWhiteSpace(city))
                return (true, city);
        }

        return (true, null);
    }

    private async Task<bool> ClassAllowsChurchAsync()
    {
        var cls = (_draft.Class ?? string.Empty).Trim();
        if (cls.Length == 0) return false;

        var all = await ClassService.GetAllAsync();
        if (!TryGetClass(all, cls, out var record) || record == null)
            return false;

        // is a priest, is not a hermit.
        return (record.Brackets?.Any(b => b == "🙏 Priest") == true &&
        !record.Levels.Any(e => e.Value.Any(v => v == "Hermit")));
    }

    private static bool TryGetClass(Dictionary<string, ServiceCharacterClassRecord> map, string key, out ServiceCharacterClassRecord? record)
    {
        if (map.TryGetValue(key, out record) && record != null)
            return true;

        foreach (var kvp in map)
        {
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                record = kvp.Value;
                return true;
            }
        }

        record = null;
        return false;
    }


    private bool WouldStillHaveAnyAlignmentIfSelected(string guildName)
    {
        var rules = new List<AlignmentRule?>();

        // non-guild rules (race/class/subtype/specs)
        rules.AddRange(_getNonGuildRules());

        // currently selected guild rules
        foreach (var g in _draft.Guilds)
            rules.Add(GetGuildRule(g));

        // plus this guild if not already selected
        if (!_draft.Guilds.Any(x => string.Equals(x, guildName, StringComparison.OrdinalIgnoreCase)))
            rules.Add(GetGuildRule(guildName));

        var available = CharacterDraft.ComputeAvailableAlignments(rules);
        return available.Count > 0;
    }

    private static string IconForType(string type)
    {
        var t = (type ?? "").Trim().ToLowerInvariant();
        if (t == "political") return "🏛️";
        if (t == "professional") return "🛠️";
        if (t.Contains("relig")) return "⛪";
        return "📜";
    }

    private static bool IsChurchGuild(string name)
    {
        var lower = (name ?? string.Empty).ToLowerInvariant();
        return lower.Contains("church")
               || lower.Contains("rings of talthar");
    }

    private void Refilter()
    {
        var text = (SearchText ?? "").Trim();
        var type = (SelectedTypeFilter ?? "All").Trim();

        bool Matches(GuildCardVm g)
        {
            if (type != "All" && !string.Equals(g.Type, type, StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.IsNullOrWhiteSpace(text))
                return true;

            return (g.Name?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false)
                   || (g.Restrictions?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false)
                   || g.BasicBenefits.Any(x => x.Contains(text, StringComparison.OrdinalIgnoreCase))
                   || g.IntermediateBenefits.Any(x => x.Contains(text, StringComparison.OrdinalIgnoreCase))
                   || g.AdvancedBenefits.Any(x => x.Contains(text, StringComparison.OrdinalIgnoreCase));
        }

        var list = AllGuilds.Where(Matches)
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        FilteredGuilds.Clear();
        foreach (var g in list)
            FilteredGuilds.Add(g);

        Raise(nameof(SelectedCount));
    }
    private AlignmentRule? GetGuildRule(string guildName)
    {
        if (string.IsNullOrWhiteSpace(guildName))
            return null;

        return _guildRecords.TryGetValue(guildName, out var rec)
            ? rec.AlignmentRule
            : null;
    }

    public int SelectedCount => _draft.Guilds.Count;

    private void RecomputeDraftAlignments()
    {
        var rules = new List<AlignmentRule?>();

        // Non-guild rules
        rules.AddRange(_getNonGuildRules());

        // Selected guild rules
        foreach (var g in _draft.Guilds)
            rules.Add(GetGuildRule(g));

        _draft.SetAvailableAlignmentsFromRules(rules);

        // Refresh selectability now that the world changed
        foreach (var card in AllGuilds)
        {
            var alignmentOk = WouldStillHaveAnyAlignmentIfSelected(card.Name);
            var slotsResult = _slotRules.CanSelect(card.Type, card.Name, _draft.Guilds, _guildRecords);

            var churchOk = _classAllowsChurch || !IsChurchGuild(card.Name);
            var selectable = alignmentOk && slotsResult.Allowed && churchOk;

            // Allow already-selected guilds to stay selectable so the user can deselect them
            card.IsSelectable = selectable || card.IsSelected;

            if (!alignmentOk)
                card.NotSelectableReason = "Conflicts with current alignment restrictions.";
            else if (!churchOk)
                card.NotSelectableReason = "Only priest-bracket classes may join a church.";
            else if (!slotsResult.Allowed)
                card.NotSelectableReason = slotsResult.Reason;
            else
                card.NotSelectableReason = "";
        }
    }

    private void ToggleExpanded(GuildCardVm? item)
    {
        if (item == null) return;
        foreach (var g in FilteredGuilds)
        {
            if (!ReferenceEquals(g, item) && g.IsExpanded)
                g.IsExpanded = false;
        }

        item.IsExpanded = !item.IsExpanded;
    }

    private void ToggleSelected(GuildCardVm? item)
    {
        if (item is null || item.IsLocked || (!item.IsSelectable && !item.IsSelected))
            return;

        if (!_classAllowsChurch && IsChurchGuild(item.Name) && !item.IsSelected)
        {
            item.NotSelectableReason = "Only priest-bracket classes may join a church.";
            return;
        }

        var slotCheck = _slotRules.CanSelect(item.Type, item.Name, _draft.Guilds, _guildRecords);
        if (!item.IsSelected && !slotCheck.Allowed)
        {
            item.NotSelectableReason = slotCheck.Reason;
            return;
        }

        var idx = _draft.Guilds.FindIndex(g => string.Equals(g, item.Name, StringComparison.OrdinalIgnoreCase));
        var nowSelected = idx < 0;

        if (nowSelected) _draft.Guilds.Add(item.Name);
        else _draft.Guilds.RemoveAt(idx);

        item.IsSelected = nowSelected;

        Raise(nameof(SelectedCount));
        RecomputeDraftAlignments();
        _notifyWizardGatingChanged();

        if (_refreshDraftAbilitiesAsync != null)
        {
            MainThread.BeginInvokeOnMainThread(async () => await _refreshDraftAbilitiesAsync());
        }
    }
}

public sealed class GuildCardVm : INotifyPropertyChanged
{
    private bool _isLocked;
    public bool IsLocked
    {
        get => _isLocked;
        set
        {
            if (_isLocked == value) return;
            _isLocked = value;
            Raise();
        }
    }

    private bool _isSelectable = true;
    public bool IsSelectable
    {
        get => _isSelectable;
        set
        {
            if (_isSelectable == value) return;
            _isSelectable = value;
            Raise();
        }
    }

    private string _notSelectableReason = "";
    public string NotSelectableReason
    {
        get => _notSelectableReason;
        set
        {
            if (_notSelectableReason == value) return;
            _notSelectableReason = value;
            Raise();
        }
    }
    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Icon { get; set; } = "📜";

    public string Restrictions { get; set; } = "";
    public List<string> BasicBenefits { get; set; } = new();
    public List<string> IntermediateBenefits { get; set; } = new();
    public List<string> AdvancedBenefits { get; set; } = new();

    public bool HasRestrictions => !string.IsNullOrWhiteSpace(Restrictions);

    public bool HasBasic => BasicBenefits.Count > 0;
    public bool HasIntermediate => IntermediateBenefits.Count > 0;
    public bool HasAdvanced => AdvancedBenefits.Count > 0;

    public bool HasAnyBenefits => HasBasic || HasIntermediate || HasAdvanced;

    public double ChevronRotation => IsExpanded ? 180 : 0;

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            Raise();
            Raise(nameof(ChevronRotation));
        }
    }

    private bool _isSelected;

    public event PropertyChangedEventHandler? PropertyChanged;

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
}

public sealed record GuildSelectability(bool Allowed, string Reason);

public sealed class GuildSlotRules
{
    public int PoliticalSlots { get; private set; } = 1;
    public int SocialSlots { get; private set; } = 1;
    public int ProfessionalSlots { get; private set; } = 1;
    public int CitySlots { get; private set; } = 0;

    public bool CityOnly { get; private set; }
    public bool AllGuildsBlocked { get; private set; }

    private static readonly Regex TradeCityRegex = new(@"trade\s+(political|social|professional)\s+for\s+city\s+(.+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    public HashSet<string> ForcedCityNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    public static GuildSlotRules Default() => new();

    public static GuildSlotRules FromAbilities(IEnumerable<AbilityDraft> abilities)
    {
        var rules = new GuildSlotRules();

        foreach (var ability in abilities ?? Array.Empty<AbilityDraft>())
        {
            var name = (ability?.Name ?? string.Empty).Trim();
            if (name.Length == 0) continue;

            var lower = name.ToLowerInvariant();

            var tradeMatch = TradeCityRegex.Match(name);
            if (tradeMatch.Success)
            {
                var slot = tradeMatch.Groups[1].Value;
                var city = tradeMatch.Groups[2].Value.Trim().TrimEnd('.', '!');

                rules.TradeSlotForCity(slot, city);
                continue;
            }

            if (lower.Contains("city bound", StringComparison.OrdinalIgnoreCase))
            {
                rules.CityOnly = true;
                var city = ExtractCityName(name);
                if (!string.IsNullOrWhiteSpace(city))
                    rules.ForcedCityNames.Add(city);
            }

            if (ContainsAllGuildBan(lower))
            {
                rules.AllGuildsBlocked = true;
                continue;
            }

            if (ContainsTypeBan(lower, "political"))
                rules.PoliticalSlots = 0;
            if (ContainsTypeBan(lower, "social"))
                rules.SocialSlots = 0;
            if (ContainsTypeBan(lower, "professional"))
                rules.ProfessionalSlots = 0;
        }

        rules.Normalize();
        return rules;
    }

    public void ApplyToCurrentSelection(CharacterDraft draft, Dictionary<string, GuildRecord> records)
    {
        if (AllGuildsBlocked)
        {
            draft.Guilds.Clear();
            return;
        }

        var kept = new List<string>();
        foreach (var existing in draft.Guilds)
        {
            var type = GetTypeForGuild(existing, records);
            var canKeep = CanSelect(type, existing, kept, records);
            if (canKeep.Allowed || ForcedCityNames.Contains(existing, StringComparer.OrdinalIgnoreCase))
                kept.Add(existing);
        }

        foreach (var forcedCity in ForcedCityNames)
        {
            var currentCityCount = CountOfType("city", kept, records);
            if (currentCityCount >= GetLimit("city"))
                break;

            if (records.ContainsKey(forcedCity) && !kept.Contains(forcedCity, StringComparer.OrdinalIgnoreCase))
                kept.Add(forcedCity);
        }

        draft.Guilds.Clear();
        foreach (var g in kept.Distinct(StringComparer.OrdinalIgnoreCase))
            draft.Guilds.Add(g);
    }

    public bool ShouldShowGuild(string type, string guildName, IReadOnlyList<string> selected)
    {
        var typeNorm = NormalizeType(type);
        var isSelected = selected.Contains(guildName, StringComparer.OrdinalIgnoreCase);

        if (AllGuildsBlocked)
            return isSelected;

        if (CityOnly && !IsCity(typeNorm))
            return isSelected;

        var limit = GetLimit(typeNorm);
        if (limit <= 0)
            return isSelected;

        if (IsCity(typeNorm) && ForcedCityNames.Count > 0 && !ForcedCityNames.Contains(guildName, StringComparer.OrdinalIgnoreCase))
            return isSelected;

        return true;
    }

    public bool IsGuildLocked(string type, string guildName)
    {
        var typeNorm = NormalizeType(type);
        if (AllGuildsBlocked)
            return false;

        if (IsCity(typeNorm) && ForcedCityNames.Contains(guildName, StringComparer.OrdinalIgnoreCase))
            return true;

        return false;
    }

    public GuildSelectability CanSelect(string type, string guildName, IReadOnlyList<string> selected, Dictionary<string, GuildRecord> records)
    {
        var typeNorm = NormalizeType(type);
        if (AllGuildsBlocked)
            return new GuildSelectability(false, "No guild slots available.");

        if (CityOnly && !IsCity(typeNorm))
            return new GuildSelectability(false, "City-bound: only city guilds are allowed.");

        var limit = GetLimit(typeNorm);
        if (limit <= 0)
            return new GuildSelectability(false, $"No {DisplayType(typeNorm)} guild slots available.");

        var isCity = IsCity(typeNorm);

        if (isCity && ForcedCityNames.Count > 0 && !ForcedCityNames.Contains(guildName, StringComparer.OrdinalIgnoreCase))
            return new GuildSelectability(false, $"City slot reserved for {string.Join(" or ", ForcedCityNames)}.");

        var currentCount = CountOfType(typeNorm, selected, records);
        var alreadySelected = selected.Contains(guildName, StringComparer.OrdinalIgnoreCase);

        if (!alreadySelected && currentCount >= limit)
            return new GuildSelectability(false, $"All {DisplayType(typeNorm)} guild slots are filled.");

        return new GuildSelectability(true, "");
    }

    private void TradeSlotForCity(string slot, string cityName)
    {
        var type = NormalizeType(slot);
        DecrementType(type);
        CitySlots++;

        if (!string.IsNullOrWhiteSpace(cityName))
            ForcedCityNames.Add(cityName.Trim());
    }

    private void DecrementType(string type)
    {
        if (IsPolitical(type) && PoliticalSlots > 0) PoliticalSlots--;
        else if (IsSocial(type) && SocialSlots > 0) SocialSlots--;
        else if (IsProfessional(type) && ProfessionalSlots > 0) ProfessionalSlots--;
    }

    private void Normalize()
    {
        if (AllGuildsBlocked)
        {
            PoliticalSlots = 0;
            SocialSlots = 0;
            ProfessionalSlots = 0;
            CitySlots = 0;
            ForcedCityNames.Clear();
            CityOnly = false;
            return;
        }

        if (CityOnly)
        {
            CitySlots = 1;
            PoliticalSlots = 0;
            SocialSlots = 0;
            ProfessionalSlots = 0;
        }

        PoliticalSlots = Math.Max(0, PoliticalSlots);
        SocialSlots = Math.Max(0, SocialSlots);
        ProfessionalSlots = Math.Max(0, ProfessionalSlots);
        CitySlots = Math.Max(0, CitySlots);
    }

    private static bool ContainsAllGuildBan(string lower)
    {
        return lower.Contains("cannot join any guild")
               || lower.Contains("can not join any guild")
               || lower.Contains("may not join any guild")
               || lower.Contains("no guilds");
    }

    private static bool ContainsTypeBan(string lower, string type)
    {
        var key = type.ToLowerInvariant();
        return lower.Contains($"cannot join {key} guild")
               || lower.Contains($"can not join {key} guild")
               || lower.Contains($"may not join {key} guild")
               || lower.Contains($"no {key} guild");
    }

    private static string ExtractCityName(string abilityName)
    {
        if (string.IsNullOrWhiteSpace(abilityName))
            return string.Empty;

        var open = abilityName.IndexOf('(');
        var close = abilityName.LastIndexOf(')');
        if (open >= 0 && close > open)
        {
            var city = abilityName.Substring(open + 1, close - open - 1).Trim();
            if (!string.IsNullOrWhiteSpace(city))
                return city;
        }

        return string.Empty;
    }

    private static int CountOfType(string type, IReadOnlyList<string> selected, Dictionary<string, GuildRecord> records)
    {
        var typeNorm = NormalizeType(type);

        var count = 0;
        foreach (var g in selected)
        {
            var t = NormalizeType(GetTypeForGuild(g, records));
            if (t == typeNorm)
                count++;
        }

        return count;
    }

    private int GetLimit(string type)
    {
        var typeNorm = NormalizeType(type);
        if (IsPolitical(typeNorm)) return PoliticalSlots;
        if (IsSocial(typeNorm)) return SocialSlots;
        if (IsProfessional(typeNorm)) return ProfessionalSlots;
        if (IsCity(typeNorm)) return CitySlots;
        return 0;
    }

    private static string GetTypeForGuild(string guildName, Dictionary<string, GuildRecord> records)
    {
        if (records.TryGetValue(guildName, out var rec) && rec != null)
            return rec.Type ?? string.Empty;

        return string.Empty;
    }

    private static string NormalizeType(string type)
        => (type ?? string.Empty).Trim().ToLowerInvariant();

    private static bool IsCity(string type) => string.Equals(type, "city", StringComparison.OrdinalIgnoreCase);
    private static bool IsPolitical(string type) => string.Equals(type, "political", StringComparison.OrdinalIgnoreCase);
    private static bool IsSocial(string type) => string.Equals(type, "social", StringComparison.OrdinalIgnoreCase);
    private static bool IsProfessional(string type) => string.Equals(type, "professional", StringComparison.OrdinalIgnoreCase);

    private static string DisplayType(string type)
    {
        var t = NormalizeType(type);
        return t switch
        {
            "city" => "city",
            "political" => "political",
            "social" => "social",
            "professional" => "professional",
            _ => "guild"
        };
    }
}
