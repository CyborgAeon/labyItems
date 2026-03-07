using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using labyItems.Models.Characters;
using labyItems.Services;
using Microsoft.Maui.ApplicationModel;

namespace labyItems.Pages.Characters;

public sealed class GuildsVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private Dictionary<string, GuildRecord> _guildRecords =
        new(StringComparer.OrdinalIgnoreCase);
    private GuildSlotRules _slotRules = GuildSlotRules.Default();
    private string _currentClassName = "";
    private HashSet<string> _currentClassBrackets = new(StringComparer.OrdinalIgnoreCase);
    private string _currentRaceName = "";
    private HashSet<string> _currentPeopleTypes = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _currentRaceSelections = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<Task>? _refreshDraftAbilitiesAsync;
    private readonly bool _applyCharacterAvailabilityFilters;
    private readonly bool _allowGuildSelection;
    private readonly bool _searchByNameOnly;
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }
    private void RaiseSelectedGuildsChanged()
    {
        Raise(nameof(SelectedGuilds));
        Raise(nameof(IsComplete));
    }

    private readonly CharacterDraft _draft;
    private readonly Action _notifyWizardGatingChanged;
    private readonly ICharacterCreationDataService _creationDataService;

    public CharacterDraft Draft => _draft;
    private readonly Func<IEnumerable<AlignmentRule?>> _getNonGuildRules;
    public GuildsVm(
        CharacterDraft draft,
        Action notifyWizardGatingChanged,
        Func<IEnumerable<AlignmentRule?>>? getNonGuildRules = null,
        Func<Task>? refreshDraftAbilitiesAsync = null,
        ICharacterCreationDataService? creationDataService = null,
        bool applyCharacterAvailabilityFilters = true,
        bool allowGuildSelection = true,
        bool searchByNameOnly = false)
    {
        _draft = draft;
        _notifyWizardGatingChanged = notifyWizardGatingChanged;
        _getNonGuildRules = getNonGuildRules ?? (() => Enumerable.Empty<AlignmentRule?>());
        _refreshDraftAbilitiesAsync = refreshDraftAbilitiesAsync;
        _applyCharacterAvailabilityFilters = applyCharacterAvailabilityFilters;
        _allowGuildSelection = allowGuildSelection;
        _searchByNameOnly = searchByNameOnly;
        _creationDataService = creationDataService
            ?? ServiceHelper.ResolveService<ICharacterCreationDataService>()
            ?? new CharacterCreationDataService();

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
    public IEnumerable<GuildCardVm> SelectedGuilds =>
        AllGuilds
            .Where(g => g.IsSelected)
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    public bool IsComplete =>
        SelectedGuilds.All(g => g.AreBenefitOptionsComplete);

    public ICommand ToggleExpandedCommand { get; }
    public ICommand ToggleSelectedCommand { get; }
    public ICommand SelectTypeFilterCommand { get; }

    public async Task ReloadAsync()
    {
        _slotRules = GuildSlotRules.FromDraft(_draft);

        _guildRecords = await _creationDataService.GetGuildsAsync() ?? new Dictionary<string, GuildRecord>(StringComparer.OrdinalIgnoreCase);
        if (ShouldForceKhaniabadCity())
            _slotRules.ForceCity("Khaniabad");
        _slotRules.ResolvePeopleTypeOverrides(_guildRecords);

        await RefreshContextAsync();

        _slotRules.ApplyToCurrentSelection(_draft, _guildRecords);
        ApplyAvailabilityToCurrentSelection();

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
        ApplyAvailabilityToCurrentSelection();
        var types = await _creationDataService.GetGuildTypesAsync();
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

            if (!_slotRules.ShouldShowGuild(type, kv.Key, _draft.Guilds))
                return false;

            var availability = EvaluateAvailabilityForCurrentContext(kv.Value, kv.Key);
            if (!availability.Allowed)
                return false;

            return true;
        })
        .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
        .ToList();
        AllGuilds.Clear();
        for (var i = 0; i < ordered.Count; i++)
        {
            var name = ordered[i].Key;
            var rec = ordered[i].Value ?? new GuildRecord();

            var isSelected = _draft.Guilds.Contains(name, StringComparer.OrdinalIgnoreCase);
            var alignmentOk = WouldStillHaveAnyAlignmentIfSelected(name);
            var slotCheck = _slotRules.CanSelect(rec.Type ?? string.Empty, name, _draft.Guilds, _guildRecords);
            var availability = EvaluateAvailabilityForCurrentContext(rec, name);
            var selectable = alignmentOk && slotCheck.Allowed && availability.Allowed;
            var reason = "";
            if (!alignmentOk) reason = "Conflicts with current alignment restrictions.";
            else if (!slotCheck.Allowed) reason = slotCheck.Reason;
            else if (!availability.Allowed) reason = availability.Reason;
            var cardSelectable = _allowGuildSelection && (selectable || isSelected);

            var vm = new GuildCardVm
            {
                Id = i + 1,
                Name = name,
                Type = rec.Type ?? "",
                Restrictions = rec.Restrictions ?? "",
                BasicBenefits = FormatBenefitList(rec.Benefits?.Basic),
                IntermediateBenefits = FormatBenefitList(rec.Benefits?.Intermediate),
                AdvancedBenefits = FormatBenefitList(rec.Benefits?.Advanced),
                BasicOptionGroups = BuildBenefitOptionGroups(name, "Basic", rec.Benefits?.Basic),
                IntermediateOptionGroups = BuildBenefitOptionGroups(name, "Intermediate", rec.Benefits?.Intermediate),
                AdvancedOptionGroups = BuildBenefitOptionGroups(name, "Advanced", rec.Benefits?.Advanced),
                MiracleRows = BuildMiracleRows(rec.MiracleList),
                IsSelected = isSelected,
                IsExpanded = false,
                IsSelectable = cardSelectable,
                NotSelectableReason = cardSelectable ? "" : (_allowGuildSelection ? reason : ""),
                IsLocked = _slotRules.IsGuildLocked(type: rec.Type ?? string.Empty, guildName: name),
            };

            vm.Icon = IconForType(vm.Type);
            AllGuilds.Add(vm);
        }

        Refilter();
        RecomputeDraftAlignments();
        _notifyWizardGatingChanged();
    }

    private bool ShouldForceKhaniabadCity()
    {
        var race = (_draft.Race ?? string.Empty).Trim();
        if (!string.Equals(race, "Human", StringComparison.OrdinalIgnoreCase))
            return false;

        var subtype = (_draft.RaceSubtypeValue ?? _draft.RaceSubtype ?? string.Empty).Trim();
        if (!string.Equals(subtype, "Ishmaic", StringComparison.OrdinalIgnoreCase))
            return false;

        var cls = (_draft.Class ?? string.Empty).Trim();
        var isKallah = string.Equals(cls, "Kallah Beggar", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(cls, "Kallah", StringComparison.OrdinalIgnoreCase);
        var isHanot = string.Equals(cls, "Hanot Beggar", StringComparison.OrdinalIgnoreCase)
                      || string.Equals(cls, "Hannot Beggar", StringComparison.OrdinalIgnoreCase);
        if (!isKallah && !isHanot)
            return false;

        if (_draft.SpecialisationSelections.TryGetValue("Ishmaic Clan", out var clan)
            && !string.IsNullOrWhiteSpace(clan))
            return false;

        return true;
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

    private async Task RefreshContextAsync()
    {
        _currentClassName = (_draft.Class ?? string.Empty).Trim();
        _currentRaceName = (_draft.Race ?? string.Empty).Trim();
        _currentClassBrackets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _currentRaceSelections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _currentPeopleTypes.Clear();

        if (_currentClassName.Length > 0)
        {
            var classMap = await _creationDataService.GetClassesAsync();
            if (_creationDataService.TryGetByName(classMap, _currentClassName, out var rec) && rec != null)
            {
                foreach (var b in rec.Brackets ?? Enumerable.Empty<string>())
                    _currentClassBrackets.Add((b ?? string.Empty).Trim());
            }
        }

        if (_currentRaceName.Length > 0)
        {
            var peopleMap = await _creationDataService.GetPeopleAsync();
            if (_creationDataService.TryGetByName(peopleMap, _currentRaceName, out var rec) && rec != null)
            {
                foreach (var t in rec.PeopleType ?? new List<string>())
                {
                    if (!string.IsNullOrWhiteSpace(t))
                        _currentPeopleTypes.Add(t.Trim());
                }
            }
        }

        var subtype = (_draft.RaceSubtypeValue ?? _draft.RaceSubtype ?? string.Empty).Trim();
        if (subtype.Length > 0)
            _currentRaceSelections.Add(subtype);

        foreach (var selection in _draft.SpecialisationSelections.Values)
        {
            var s = (selection ?? string.Empty).Trim();
            if (s.Length > 0)
                _currentRaceSelections.Add(s);
        }
    }

    private void ApplyAvailabilityToCurrentSelection()
    {
        if (!_applyCharacterAvailabilityFilters)
            return;

        var kept = new List<string>();
        foreach (var g in _draft.Guilds)
        {
            var result = EvaluateAvailability(g);
            if (result.Allowed)
                kept.Add(g);
        }

        _draft.Guilds.Clear();
        foreach (var g in kept.Distinct(StringComparer.OrdinalIgnoreCase))
            _draft.Guilds.Add(g);
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

    private AvailabilityResult EvaluateAvailability(string guildName)
    {
        if (string.IsNullOrWhiteSpace(guildName))
            return AvailabilityResult.Ok();

        if (_guildRecords.TryGetValue(guildName, out var rec) && rec != null)
            return EvaluateAvailability(rec, guildName);

        return AvailabilityResult.Ok();
    }

    private AvailabilityResult EvaluateAvailability(GuildRecord rec, string guildName)
    {
        var availability = rec.Availability ?? new GuildAvailability();
        var whitelist = availability.Whitelist ?? new GuildAvailabilityRules();
        var blacklist = availability.Blacklist ?? new GuildAvailabilityRules();

        if (!string.IsNullOrWhiteSpace(availability.RequiredGuild)
            && !_draft.Guilds.Contains(availability.RequiredGuild, StringComparer.OrdinalIgnoreCase))
        {
            return new AvailabilityResult(false, $"Requires {availability.RequiredGuild}.");
        }

        if (MatchesBlacklist(blacklist, out var blackReason))
            return new AvailabilityResult(false, blackReason);

        if (!MeetsWhitelist(whitelist, out var whiteReason))
            return new AvailabilityResult(false, whiteReason);

        return AvailabilityResult.Ok();
    }

    private AvailabilityResult EvaluateAvailabilityForCurrentContext(GuildRecord rec, string guildName)
    {
        if (!_applyCharacterAvailabilityFilters)
            return AvailabilityResult.Ok();

        return EvaluateAvailability(rec, guildName);
    }

    private AvailabilityResult EvaluateAvailabilityForCurrentContext(string guildName)
    {
        if (!_applyCharacterAvailabilityFilters)
            return AvailabilityResult.Ok();

        return EvaluateAvailability(guildName);
    }

    private bool MatchesBlacklist(GuildAvailabilityRules? rules, out string reason)
    {
        reason = "";
        if (rules == null)
            return false;

        if (ContainsClassName(rules.Classes, _currentClassName))
        {
            reason = "Class not permitted.";
            return true;
        }

        if (rules.Brackets?.Any(b => _currentClassBrackets.Contains(b ?? string.Empty)) == true)
        {
            reason = "Bracket not permitted.";
            return true;
        }

        if (rules.PeopleType?.Any(t => _currentPeopleTypes.Contains(t ?? string.Empty)) == true)
        {
            reason = "People type not permitted.";
            return true;
        }

        if (rules.Races?.Any(RaceMatches) == true)
        {
            reason = "Race not permitted.";
            return true;
        }

        return false;
    }

    private bool MeetsWhitelist(GuildAvailabilityRules? rules, out string reason)
    {
        reason = "";
        if (rules == null)
            return true;

        if (rules.Classes is { Count: > 0 } && !ContainsClassName(rules.Classes, _currentClassName))
        {
            reason = $"Only: {string.Join(", ", rules.Classes)}";
            return false;
        }

        if (rules.Brackets is { Count: > 0 } && !_currentClassBrackets.Any(b => rules.Brackets.Contains(b, StringComparer.OrdinalIgnoreCase)))
        {
            reason = $"Requires bracket(s): {string.Join(", ", rules.Brackets)}";
            return false;
        }

        if (rules.PeopleType is { Count: > 0 })
        {
            if (!rules.PeopleType.Any(t => _currentPeopleTypes.Contains(t ?? string.Empty)))
            {
                reason = $"Limited to: {string.Join(", ", rules.PeopleType)}";
                return false;
            }
        }

        if (rules.Races is { Count: > 0 })
        {
            if (!rules.Races.Any(RaceMatches))
            {
                var names = rules.Races.Select(r => r?.Name).Where(n => !string.IsNullOrWhiteSpace(n));
                reason = $"Limited to races: {string.Join(", ", names)}";
                return false;
            }
        }

        if (!AlignmentsSatisfied(rules.Alignments))
        {
            reason = "No compatible alignment available.";
            return false;
        }

        return true;
    }

    private bool AlignmentsSatisfied(GuildAvailabilityAlignments? alignments)
    {
        if (alignments == null)
            return true;

        var allowedOrders = ParseOrders(alignments.Order);
        var allowedMorals = ParseMorals(alignments.Moral);
        if (allowedOrders.Count == 0 && allowedMorals.Count == 0)
            return true;

        foreach (var a in _draft.AvailableAlignments ?? Enumerable.Empty<Alignment>())
        {
            var orderOk = allowedOrders.Count == 0 || allowedOrders.Contains(a.Order);
            var moralOk = allowedMorals.Count == 0 || allowedMorals.Contains(a.Moral);
            if (orderOk && moralOk)
                return true;
        }

        return false;
    }

    private static bool ContainsClassName(IEnumerable<string>? classNames, string className)
    {
        var wanted = NormalizeLookupKey(className);
        if (wanted.Length == 0)
            return false;

        foreach (var candidate in classNames ?? Enumerable.Empty<string>())
        {
            if (NormalizeLookupKey(candidate) == wanted)
                return true;
        }

        return false;
    }

    private static string NormalizeLookupKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return new string(value
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
    }

    private static HashSet<OrderAxis> ParseOrders(IEnumerable<string>? values)
    {
        var set = new HashSet<OrderAxis>();
        foreach (var v in values ?? Array.Empty<string>())
        {
            if (Enum.TryParse<OrderAxis>(v, true, out var parsed))
                set.Add(parsed);
        }
        return set;
    }

    private static HashSet<MoralAxis> ParseMorals(IEnumerable<string>? values)
    {
        var set = new HashSet<MoralAxis>();
        foreach (var v in values ?? Array.Empty<string>())
        {
            if (Enum.TryParse<MoralAxis>(v, true, out var parsed))
                set.Add(parsed);
        }
        return set;
    }

    private bool RaceMatches(GuildAvailabilityRace? rule)
    {
        if (rule == null)
            return false;

        var name = (rule.Name ?? string.Empty).Trim();
        if (name.Length == 0)
            return false;

        if (!string.Equals(name, _currentRaceName, StringComparison.OrdinalIgnoreCase))
            return false;

        var subtype = (rule.Subtype ?? string.Empty).Trim();
        if (subtype.Length == 0)
            return true;

        return _currentRaceSelections.Contains(subtype);
    }

    private static AlignmentRule? BuildAlignmentRuleFromAvailability(GuildAvailabilityAlignments? align)
    {
        if (align == null)
            return null;

        var orders = ParseOrders(align.Order);
        var morals = ParseMorals(align.Moral);
        if (orders.Count == 0 && morals.Count == 0)
            return null;

        return new AlignmentRule
        {
            Mode = "restrict",
            Allowed = new AllowedAxes
            {
                Order = orders.ToList(),
                Moral = morals.ToList()
            }
        };
    }

    private static string IconForType(string type)
    {
        var t = (type ?? "").Trim().ToLowerInvariant();
        if (t == "political") return "🏛️";
        if (t == "professional") return "🛠️";
        if (t.Contains("relig")) return "⛪";
        return "📜";
    }

    private static List<GuildMiracleRowVm> BuildMiracleRows(Dictionary<string, List<string>>? miracleList)
    {
        if (miracleList == null || miracleList.Count == 0)
            return new List<GuildMiracleRowVm>();

        var ordered = miracleList
            .Select(kvp =>
            {
                var key = (kvp.Key ?? string.Empty).Trim();
                int? level = int.TryParse(key, out var parsed) ? parsed : null;
                var miracles = kvp.Value ?? new List<string>();
                return (Key: key, Level: level, Miracles: miracles);
            })
            .Where(x => x.Key.Length > 0)
            .OrderBy(x => x.Level ?? int.MaxValue)
            .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rows = new List<GuildMiracleRowVm>();
        foreach (var entry in ordered)
        {
            var names = entry.Miracles
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .ToList();

            if (names.Count == 0)
                continue;

            rows.Add(new GuildMiracleRowVm
            {
                Level = entry.Level?.ToString() ?? entry.Key,
                Miracles = string.Join(", ", names)
            });
        }

        return rows;
    }

    private static List<string> FormatBenefitList(IEnumerable<GuildBenefitEntry>? benefits)
    {
        var list = new List<string>();
        foreach (var benefit in benefits ?? Enumerable.Empty<GuildBenefitEntry>())
        {
            if (benefit?.Ability == null)
                continue;

            var display = FormatBenefit(benefit.Ability);
            if (!string.IsNullOrWhiteSpace(display))
                list.Add(display);
        }
        return list;
    }

    private static string FormatBenefit(AbilityDefinition? benefit)
    {
        if (benefit == null)
            return string.Empty;

        var name = (benefit.Name ?? string.Empty).Trim();
        var effect = (benefit.Effect ?? string.Empty).Trim();
        var frequency = (benefit.Frequency ?? string.Empty).Trim();
        var display = name;

        if (!string.IsNullOrWhiteSpace(effect))
            display = display.Length > 0 ? $"{display}: {effect}" : effect;

        if (benefit.Count.HasValue && benefit.Count.Value > 1)
            display = display.Length > 0 ? $"{display} (x{benefit.Count.Value})" : $"x{benefit.Count.Value}";

        if (!string.IsNullOrWhiteSpace(frequency))
            display = display.Length > 0 ? $"{display} [{frequency}]" : frequency;

        return display;
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

            if (_searchByNameOnly)
                return g.Name?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false;

            return (g.Name?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false)
                   || (g.Restrictions?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false)
                   || g.BasicBenefits.Any(x => x.Contains(text, StringComparison.OrdinalIgnoreCase))
                   || g.IntermediateBenefits.Any(x => x.Contains(text, StringComparison.OrdinalIgnoreCase))
                   || g.AdvancedBenefits.Any(x => x.Contains(text, StringComparison.OrdinalIgnoreCase))
                   || MatchesOptionGroups(g.BasicOptionGroups, text)
                   || MatchesOptionGroups(g.IntermediateOptionGroups, text)
                   || MatchesOptionGroups(g.AdvancedOptionGroups, text);
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

        if (!_guildRecords.TryGetValue(guildName, out var rec) || rec == null)
            return null;

        return _creationDataService.GetGuildAlignmentRule(rec);
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
            card.IsSelected = _draft.Guilds.Contains(card.Name, StringComparer.OrdinalIgnoreCase);
            var alignmentOk = WouldStillHaveAnyAlignmentIfSelected(card.Name);
            var slotsResult = _slotRules.CanSelect(card.Type, card.Name, _draft.Guilds, _guildRecords);
            var availability = EvaluateAvailabilityForCurrentContext(card.Name);
            var selectable = alignmentOk && slotsResult.Allowed && availability.Allowed;

            // Allow already-selected guilds to stay selectable so the user can deselect them
            card.IsSelectable = _allowGuildSelection && (selectable || card.IsSelected);

            if (!alignmentOk)
                card.NotSelectableReason = "Conflicts with current alignment restrictions.";
            else if (!slotsResult.Allowed)
                card.NotSelectableReason = slotsResult.Reason;
            else if (!availability.Allowed)
                card.NotSelectableReason = availability.Reason;
            else
                card.NotSelectableReason = "";

            if (!_allowGuildSelection)
                card.NotSelectableReason = "";
        }

        RaiseSelectedGuildsChanged();
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

    private static bool MatchesOptionGroups(IEnumerable<GuildBenefitOptionGroupVm> groups, string text)
    {
        foreach (var group in groups ?? Enumerable.Empty<GuildBenefitOptionGroupVm>())
        {
            if (group.OptionLabels.Any(o => o.Contains(text, StringComparison.OrdinalIgnoreCase)))
                return true;

            if (group.SelectedLines.Any(l => l.Contains(text, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        return false;
    }

    private List<GuildBenefitOptionGroupVm> BuildBenefitOptionGroups(
        string guildName,
        string tier,
        IEnumerable<GuildBenefitEntry>? benefits)
    {
        var list = new List<GuildBenefitOptionGroupVm>();
        if (string.IsNullOrWhiteSpace(guildName))
            return list;

        var optionIndex = 0;
        foreach (var entry in benefits ?? Enumerable.Empty<GuildBenefitEntry>())
        {
            if (entry?.Options == null || entry.Options.Count == 0)
                continue;

            optionIndex++;
            var key = GuildBenefitKeys.BuildSelectionKey(guildName, tier, optionIndex);
            var options = entry.Options
                .Select(o => BuildOptionVm(o))
                .Where(o => o != null)
                .Cast<GuildBenefitOptionVm>()
                .ToList();

            if (options.Count == 0)
                continue;

            var selected = _draft.GuildBenefitSelections.TryGetValue(key, out var saved)
                ? saved
                : (int?)null;

            var vm = new GuildBenefitOptionGroupVm(key, options, selected, OnBenefitOptionSelectionChanged);
            list.Add(vm);
        }

        return list;
    }

    private GuildBenefitOptionVm? BuildOptionVm(GuildBenefitOption option)
    {
        if (option?.Abilities == null || option.Abilities.Count == 0)
            return null;

        var label = BuildOptionLabel(option.Abilities);
        var lines = option.Abilities
            .Select(FormatBenefit)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        return new GuildBenefitOptionVm
        {
            Label = label,
            Abilities = option.Abilities,
            Lines = lines
        };
    }

    private static string BuildOptionLabel(IEnumerable<AbilityDefinition> abilities)
    {
        var names = abilities
            .Select(a => (a?.Name ?? string.Empty).Trim())
            .Where(n => n.Length > 0)
            .ToList();

        if (names.Count == 0)
            return "Option";

        if (names.Count == 1)
            return names[0];

        return $"{names[0]} +{names.Count - 1}";
    }

    private void OnBenefitOptionSelectionChanged(GuildBenefitOptionGroupVm group)
    {
        if (group == null)
            return;

        if (group.SelectedIndex.HasValue)
            _draft.GuildBenefitSelections[group.SelectionKey] = group.SelectedIndex.Value;
        else
            _draft.GuildBenefitSelections.Remove(group.SelectionKey);

        Raise(nameof(IsComplete));
        _notifyWizardGatingChanged();

        if (_refreshDraftAbilitiesAsync != null)
            MainThread.BeginInvokeOnMainThread(async () => await _refreshDraftAbilitiesAsync());
    }

    private void ToggleSelected(GuildCardVm? item)
    {
        if (!_allowGuildSelection)
            return;

        if (item is null || item.IsLocked || (!item.IsSelectable && !item.IsSelected))
            return;

        var slotCheck = _slotRules.CanSelect(item.Type, item.Name, _draft.Guilds, _guildRecords);
        if (!item.IsSelected && !slotCheck.Allowed)
        {
            item.NotSelectableReason = slotCheck.Reason;
            return;
        }

        var availability = EvaluateAvailabilityForCurrentContext(item.Name);
        if (!item.IsSelected && !availability.Allowed)
        {
            item.NotSelectableReason = availability.Reason;
            return;
        }

        var alignmentOk = WouldStillHaveAnyAlignmentIfSelected(item.Name);
        if (!item.IsSelected && !alignmentOk)
        {
            item.NotSelectableReason = "Conflicts with current alignment restrictions.";
            return;
        }

        var idx = _draft.Guilds.FindIndex(g => string.Equals(g, item.Name, StringComparison.OrdinalIgnoreCase));
        var nowSelected = idx < 0;

        if (nowSelected) _draft.Guilds.Add(item.Name);
        else _draft.Guilds.RemoveAt(idx);

        item.IsSelected = nowSelected;

        ApplyAvailabilityToCurrentSelection();

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

    private string _name = "";
    public string Name
    {
        get => _name;
        set
        {
            if (_name == value) return;
            _name = value ?? string.Empty;
            Raise();
            Raise(nameof(DisplayName));
        }
    }

    public string DisplayName => _name.Length > 15 ? $"{_name[..12]}..." : _name;
    public string Type { get; set; } = "";
    public string Icon { get; set; } = "📜";

    public string Restrictions { get; set; } = "";
    public List<string> BasicBenefits { get; set; } = new();
    public List<string> IntermediateBenefits { get; set; } = new();
    public List<string> AdvancedBenefits { get; set; } = new();
    public List<GuildBenefitOptionGroupVm> BasicOptionGroups { get; set; } = new();
    public List<GuildBenefitOptionGroupVm> IntermediateOptionGroups { get; set; } = new();
    public List<GuildBenefitOptionGroupVm> AdvancedOptionGroups { get; set; } = new();
    public List<GuildMiracleRowVm> MiracleRows { get; set; } = new();

    public bool HasRestrictions => !string.IsNullOrWhiteSpace(Restrictions);

    public bool HasBasic => BasicBenefits.Count > 0 || BasicOptionGroups.Count > 0;
    public bool HasIntermediate => IntermediateBenefits.Count > 0 || IntermediateOptionGroups.Count > 0;
    public bool HasAdvanced => AdvancedBenefits.Count > 0 || AdvancedOptionGroups.Count > 0;
    public bool HasMiracles => MiracleRows.Count > 0;

    public bool HasAnyBenefits => HasBasic || HasIntermediate || HasAdvanced;
    public bool HasBasicOptions => BasicOptionGroups.Count > 0;
    public bool HasIntermediateOptions => IntermediateOptionGroups.Count > 0;
    public bool HasAdvancedOptions => AdvancedOptionGroups.Count > 0;
    public bool AreBenefitOptionsComplete =>
        BasicOptionGroups.All(g => g.HasSelection)
        && IntermediateOptionGroups.All(g => g.HasSelection)
        && AdvancedOptionGroups.All(g => g.HasSelection);

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

public sealed class GuildBenefitOptionGroupVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }

    private readonly Action<GuildBenefitOptionGroupVm> _onSelectionChanged;
    private bool _suppressNotify;
    private string? _selectedOption;
    private int? _selectedIndex;

    public string SelectionKey { get; }
    public ObservableCollection<string> OptionLabels { get; }
    public IReadOnlyList<GuildBenefitOptionVm> Options { get; }

    public string? SelectedOption
    {
        get => _selectedOption;
        set
        {
            var normalized = (value ?? string.Empty).Trim();
            if (normalized.Length == 0)
                normalized = null;

            if (!Set(ref _selectedOption, normalized))
                return;

            _selectedIndex = ResolveIndex(normalized);
            Raise(nameof(SelectedIndex));
            Raise(nameof(HasSelection));
            Raise(nameof(SelectedLines));

            if (!_suppressNotify)
                _onSelectionChanged(this);
        }
    }

    public int? SelectedIndex => _selectedIndex;

    public bool HasSelection => _selectedIndex.HasValue;

    public IEnumerable<string> SelectedLines
    {
        get
        {
            if (!_selectedIndex.HasValue)
                return Enumerable.Empty<string>();

            var idx = _selectedIndex.Value - 1;
            if (idx < 0 || idx >= Options.Count)
                return Enumerable.Empty<string>();

            return Options[idx].Lines;
        }
    }

    public GuildBenefitOptionGroupVm(
        string selectionKey,
        IReadOnlyList<GuildBenefitOptionVm> options,
        int? initialSelectionIndex,
        Action<GuildBenefitOptionGroupVm> onSelectionChanged)
    {
        SelectionKey = selectionKey ?? string.Empty;
        Options = options ?? new List<GuildBenefitOptionVm>();
        OptionLabels = new ObservableCollection<string>(Options.Select(o => o.Label));
        _onSelectionChanged = onSelectionChanged;

        if (initialSelectionIndex.HasValue)
        {
            var idx = initialSelectionIndex.Value - 1;
            if (idx >= 0 && idx < OptionLabels.Count)
            {
                _suppressNotify = true;
                SelectedOption = OptionLabels[idx];
                _suppressNotify = false;
            }
        }
    }

    private int? ResolveIndex(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return null;

        for (var i = 0; i < OptionLabels.Count; i++)
        {
            if (string.Equals(OptionLabels[i], label, StringComparison.OrdinalIgnoreCase))
                return i + 1;
        }

        return null;
    }
}

public sealed class GuildBenefitOptionVm
{
    public string Label { get; init; } = string.Empty;
    public List<AbilityDefinition> Abilities { get; init; } = new();
    public List<string> Lines { get; init; } = new();
}

public sealed class GuildMiracleRowVm
{
    public string Level { get; init; } = string.Empty;
    public string Miracles { get; init; } = string.Empty;
}

public sealed record GuildSelectability(bool Allowed, string Reason);
public sealed record AvailabilityResult(bool Allowed, string Reason)
{
    public static AvailabilityResult Ok() => new(true, "");
}

public sealed class GuildSlotRules
{
    public int PoliticalSlots { get; private set; } = 1;
    public int SocialSlots { get; private set; } = 1;
    public int ProfessionalSlots { get; private set; } = 1;
    public int CitySlots { get; private set; } = 0;

    public bool CityOnly { get; private set; }
    public bool AllGuildsBlocked { get; private set; }

    private readonly Dictionary<string, HashSet<string>> _allowedByType = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _forcedByType = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _peopleTypeByType = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Regex TradeCityRegex = new(@"trade\s+(political|social|professional)\s+for\s+city\s+(.+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    public HashSet<string> ForcedCityNames { get; } = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string[]> TypeTokenMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["baronial"] = new[] { "Claws of the Circle" }
    };

    public static GuildSlotRules Default() => new();

    public static GuildSlotRules FromAbilities(IEnumerable<AbilityDraft> abilities)
    {
        var rules = new GuildSlotRules();

        foreach (var ability in abilities ?? Array.Empty<AbilityDraft>())
        {
            var name = (ability?.Name ?? string.Empty).Trim();
            if (name.Length == 0) continue;

            var lower = name.ToLowerInvariant();

            if (ability?.GuildOverrides is { Count: > 0 })
            {
                rules.ApplyGuildOverrides(ability.GuildOverrides);
            }
            else if (ability?.AbilityType == AbilityType.GuildOverride)
            {
                rules.ApplyGuildOverride(name);
            }

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

    public static GuildSlotRules FromDraft(CharacterDraft draft)
    {
        var rules = FromAbilities(draft.Abilities);
        rules.ApplyOverrides(draft.GuildOverrideRules);
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

        foreach (var kvp in _forcedByType)
        {
            var type = kvp.Key;
            ApplyForcedByType(type, kvp.Value, kept, records);
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

        if (_allowedByType.TryGetValue(typeNorm, out var allowed) && allowed.Count > 0)
        {
            if (!allowed.Contains(guildName, StringComparer.OrdinalIgnoreCase))
                return isSelected;
        }

        return true;
    }

    public bool IsGuildLocked(string type, string guildName)
    {
        var typeNorm = NormalizeType(type);
        if (AllGuildsBlocked)
            return false;

        if (IsCity(typeNorm) && ForcedCityNames.Contains(guildName, StringComparer.OrdinalIgnoreCase))
            return true;

        if (_forcedByType.TryGetValue(typeNorm, out var forced) && forced.Contains(guildName, StringComparer.OrdinalIgnoreCase))
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

        if (_allowedByType.TryGetValue(typeNorm, out var allowed) && allowed.Count > 0
            && !allowed.Contains(guildName, StringComparer.OrdinalIgnoreCase))
            return new GuildSelectability(false, $"Only {string.Join(", ", allowed)} allowed.");

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

        if (_forcedByType.TryGetValue(typeNorm, out var forced) && forced.Contains(guildName, StringComparer.OrdinalIgnoreCase))
            return new GuildSelectability(true, "");

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

    public void ForceCity(string cityName)
    {
        if (string.IsNullOrWhiteSpace(cityName))
            return;

        ForcedCityNames.Add(cityName.Trim());
        CitySlots = Math.Max(1, CitySlots);
        Normalize();
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

    private void ApplyGuildOverride(string raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0) return;

        if (text.Equals("city bound", StringComparison.OrdinalIgnoreCase))
        {
            CityOnly = true;
            AllGuildsBlocked = false;
            return;
        }

        var parts = text.Split(':', 2);
        if (parts.Length == 0) return;

        var type = NormalizeTypePrefix(parts[0]);
        if (string.IsNullOrWhiteSpace(type))
            return;

        var value = parts.Length > 1 ? (parts[1] ?? string.Empty).Trim() : string.Empty;
        ApplyTypeOverride(type, value);
    }

    private void ApplyTypeOverride(string type, string value)
    {
        var typeNorm = NormalizeType(type);

        if (string.IsNullOrWhiteSpace(value))
        {
            SetLimitForType(typeNorm, 0);
            _allowedByType[typeNorm] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        var lower = value.Trim();
        if (lower.StartsWith("type-", StringComparison.OrdinalIgnoreCase))
        {
            var token = lower.Substring(5).Trim();
            var allowed = ResolveTokenGuilds(token);
            if (allowed.Count > 0)
            {
                _allowedByType[typeNorm] = allowed;
                EnsureSlotsForType(typeNorm, Math.Max(1, allowed.Count));
            }
            else if (!string.IsNullOrWhiteSpace(token))
            {
                _peopleTypeByType[typeNorm] = token;
            }
            return;
        }

        var forced = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { value };
        _forcedByType[typeNorm] = forced;
        _allowedByType[typeNorm] = forced;
        EnsureSlotsForType(typeNorm, forced.Count);
    }

    private void ApplyOverrides(GuildOverrideRules? overrides)
    {
        if (overrides == null)
            return;

        if (overrides.IsCityBound)
        {
            CityOnly = true;
            AllGuildsBlocked = false;
        }

        ApplyChannelOverride("political", overrides.Political);
        ApplyChannelOverride("professional", overrides.Professional);
        ApplyChannelOverride("social", overrides.Social);

        Normalize();
    }

    private void ApplyChannelOverride(string type, GuildOverrideChannel? channel)
    {
        if (channel == null) return;

        var typeNorm = NormalizeType(type);

        if (channel.CanJoin == false)
        {
            SetLimitForType(typeNorm, 0);
            return;
        }

        if (channel.ReplacedBy is { Count: > 0 })
        {
            var set = new HashSet<string>(channel.ReplacedBy.Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.OrdinalIgnoreCase);
            _forcedByType[typeNorm] = set;
            _allowedByType[typeNorm] = set;
            EnsureSlotsForType(typeNorm, Math.Max(1, set.Count));
            return;
        }

        if (!string.IsNullOrWhiteSpace(channel.GuildPeople))
        {
            _peopleTypeByType[typeNorm] = channel.GuildPeople.Trim();
            return;
        }
    }

    public void ResolvePeopleTypeOverrides(Dictionary<string, GuildRecord> records)
    {
        if (records == null || records.Count == 0 || _peopleTypeByType.Count == 0)
            return;

        foreach (var kvp in _peopleTypeByType)
        {
            var typeNorm = NormalizeType(kvp.Key);
            var peopleType = kvp.Value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(peopleType))
                continue;

            var normalizedToken = NormalizePeopleTypeToken(peopleType);
            var allowed = CollectAllowedGuildsByPeopleType(records, typeNorm, normalizedToken);

            if (allowed.Count == 0)
            {
                SetLimitForType(typeNorm, 0);
                _allowedByType[typeNorm] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _forcedByType.Remove(typeNorm);
                continue;
            }

            _allowedByType[typeNorm] = allowed;
            _forcedByType.Remove(typeNorm);
            EnsureSlotsForType(typeNorm, Math.Max(1, allowed.Count));
        }
    }

    private void ApplyGuildOverrides(IEnumerable<string> overrides)
    {
        foreach (var entry in overrides ?? Array.Empty<string>())
            ApplyGuildOverride(entry);
    }

    private void ApplyForcedByType(
        string type,
        IEnumerable<string> forcedByType,
        IList<string> kept,
        Dictionary<string, GuildRecord> records)
    {
        var limit = GetLimit(type);
        foreach (var forced in forcedByType ?? Array.Empty<string>())
        {
            if (CountOfType(type, kept, records) >= limit)
                break;

            if (records.ContainsKey(forced) && !kept.Contains(forced, StringComparer.OrdinalIgnoreCase))
                kept.Add(forced);
        }
    }

    private static HashSet<string> CollectAllowedGuildsByPeopleType(
        Dictionary<string, GuildRecord> records,
        string typeNorm,
        string normalizedToken)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rec in records)
        {
            var guild = rec.Value;
            if (guild == null)
                continue;

            var guildType = NormalizeType(guild.Type ?? string.Empty);
            if (!string.Equals(guildType, typeNorm, StringComparison.OrdinalIgnoreCase))
                continue;

            var whitelist = guild.Availability?.Whitelist?.PeopleType ?? new List<string>();
            foreach (var entry in whitelist)
            {
                if (NormalizePeopleTypeToken(entry) == normalizedToken)
                {
                    allowed.Add(rec.Key);
                    break;
                }
            }
        }

        return allowed;
    }

    private static string NormalizePeopleTypeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var chars = value.Where(char.IsLetterOrDigit).ToArray();
        return new string(chars).ToLowerInvariant();
    }

    private static string NormalizeTypePrefix(string prefix)
    {
        var p = NormalizeType(prefix);
        return p switch
        {
            "po" or "political" => "political",
            "pr" or "professional" => "professional",
            "so" or "social" => "social",
            _ => string.Empty
        };
    }

    private HashSet<string> ResolveTokenGuilds(string token)
    {
        if (TypeTokenMap.TryGetValue(token.Trim(), out var names))
            return new HashSet<string>(names ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private void SetLimitForType(string type, int value)
    {
        if (IsPolitical(type)) PoliticalSlots = value;
        else if (IsSocial(type)) SocialSlots = value;
        else if (IsProfessional(type)) ProfessionalSlots = value;
    }

    private void EnsureSlotsForType(string type, int minimum)
    {
        if (IsPolitical(type)) PoliticalSlots = Math.Max(PoliticalSlots, minimum);
        else if (IsSocial(type)) SocialSlots = Math.Max(SocialSlots, minimum);
        else if (IsProfessional(type)) ProfessionalSlots = Math.Max(ProfessionalSlots, minimum);
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

    private static int CountOfType(string type, IEnumerable<string> selected, Dictionary<string, GuildRecord> records)
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
