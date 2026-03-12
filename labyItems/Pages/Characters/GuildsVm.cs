using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Input;
using labyItems.Models.Characters;
using labyItems.Models.Rules;
using labyItems.Services;
using Microsoft.Maui.ApplicationModel;

namespace labyItems.Pages.Characters;

public sealed class GuildsVm : INotifyPropertyChanged
{
    private const string AllTypeFilterValue = "All";
    private static readonly IReadOnlyDictionary<string, GuildMiracleDefinition> EmptyMiracleLookup =
        new Dictionary<string, GuildMiracleDefinition>(StringComparer.OrdinalIgnoreCase);
    private static readonly Regex MiracleListLoreBlockRegex = new(
        @"(?is)(?:^|\n\s*\n)[^\n]*?\b(?:[A-Z]+\s+)*MIRACLE LIST\b.*?(?=(\n\s*\n|$))",
        RegexOptions.Compiled);
    private static readonly Regex DenominationalMiracleLoreBlockRegex = new(
        @"(?is)(?:^|\n\s*\n)[^\n]*?\bDENOMINATIONAL MIRACLE\b.*?(?=(\n\s*\n|$))",
        RegexOptions.Compiled);
    private static readonly Regex MiracleStatLoreBlockRegex = new(
        @"(?is)(?:^|\n\s*\n)[^\n]*?\bLevel:\b[^\n]*\bAlignment:\b[^\n]*\bDuration:\b[^\n]*\bRange:\b.*?(?=(\n\s*\n|$))",
        RegexOptions.Compiled);
    private static readonly Regex ListPrefixRegex = new(
        @"^(?:[•\-\*]|(?:\d+[\.\)]\s)|(?:\d+(?:st|nd|rd|th)\b))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private Dictionary<string, GuildRecord> _guildRecords =
        new(StringComparer.OrdinalIgnoreCase);
    private string _currentClassName = "";
    private HashSet<string> _currentClassBrackets = new(StringComparer.OrdinalIgnoreCase);
    private string _currentRaceName = "";
    private HashSet<string> _currentPeopleTypes = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _currentRaceSelections = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, GuildMiracleDefinition>? _miracleLookupCache;
    private Task<IReadOnlyDictionary<string, GuildMiracleDefinition>>? _miracleLookupTask;
    private readonly Dictionary<string, Task> _detailLoadTasks = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _detailLoadGate = new();
    private readonly Func<Task>? _refreshDraftAbilitiesAsync;
    private readonly bool _applyCharacterAvailabilityFilters;
    private readonly bool _allowGuildSelection;
    private readonly bool _searchByNameOnly;
    private readonly bool _useMultiTypeFilters;
    private readonly HashSet<string> _selectedTypeFilters = new(StringComparer.OrdinalIgnoreCase);
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
        bool searchByNameOnly = false,
        bool useMultiTypeFilters = false,
        bool autoReload = true)
    {
        _draft = draft;
        _notifyWizardGatingChanged = notifyWizardGatingChanged;
        _getNonGuildRules = getNonGuildRules ?? (() => Enumerable.Empty<AlignmentRule?>());
        _refreshDraftAbilitiesAsync = refreshDraftAbilitiesAsync;
        _applyCharacterAvailabilityFilters = applyCharacterAvailabilityFilters;
        _allowGuildSelection = allowGuildSelection;
        _searchByNameOnly = searchByNameOnly;
        _useMultiTypeFilters = useMultiTypeFilters;
        _creationDataService = creationDataService
            ?? ServiceHelper.ResolveService<ICharacterCreationDataService>()
            ?? new CharacterCreationDataService();

        TypeFilters = new ObservableCollection<string> { AllTypeFilterValue };
        _selectedTypeFilter = AllTypeFilterValue;

        AllGuilds = new ObservableCollection<GuildCardVm>();
        FilteredGuilds = new ObservableCollection<GuildCardVm>();
        TypeFilterChips = new ObservableCollection<GuildTypeFilterChipVm>();

        ToggleExpandedCommand = new Command<GuildCardVm>(item => _ = ToggleExpandedAsync(item));
        ToggleSelectedCommand = new Command<GuildCardVm>(item => _ = ToggleSelectedAsync(item));
        ToggleTypeFilterChipCommand = new Command<GuildTypeFilterChipVm>(ToggleTypeFilterChip);

        SelectTypeFilterCommand = new Command<string>(s =>
        {
            SelectedTypeFilter = string.IsNullOrWhiteSpace(s) ? AllTypeFilterValue : s;
        });

        if (autoReload)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await ReloadAsync();
            });
        }
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
    public ObservableCollection<GuildTypeFilterChipVm> TypeFilterChips { get; }
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
    public ICommand ToggleTypeFilterChipCommand { get; }

    public async Task ReloadAsync()
    {
        _guildRecords = await _creationDataService.GetGuildsAsync() ?? new Dictionary<string, GuildRecord>(StringComparer.OrdinalIgnoreCase);
        lock (_detailLoadGate)
            _detailLoadTasks.Clear();

        await RefreshContextAsync();

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
        TypeFilters.Add(AllTypeFilterValue);
        foreach (var t in types)
            TypeFilters.Add(t);

        if (string.IsNullOrWhiteSpace(SelectedTypeFilter) || !TypeFilters.Contains(SelectedTypeFilter))
            SelectedTypeFilter = AllTypeFilterValue;

        if (_useMultiTypeFilters)
            RebuildTypeFilterChips();
        else
            TypeFilterChips.Clear();

        var ordered = _guildRecords
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        AllGuilds.Clear();
        var preloadDetails = new List<Task>();
        for (var i = 0; i < ordered.Count; i++)
        {
            var name = ordered[i].Key;
            var rec = ordered[i].Value ?? new GuildRecord();

            var isSelected = _draft.Guilds.Contains(name, StringComparer.OrdinalIgnoreCase);
            var availability = EvaluateAvailabilityForCurrentContext(rec, name);
            var selectable = availability.Allowed;
            var reason = availability.Reason;
            var cardSelectable = !_allowGuildSelection || (selectable || isSelected);

            var vm = new GuildCardVm
            {
                Id = i + 1,
                Name = name,
                Type = rec.Type ?? "",
                Logo = NormalizeLogoPath(rec.Logo),
                IsSelected = isSelected,
                IsExpanded = false,
                IsSelectable = cardSelectable,
                NotSelectableReason = cardSelectable ? "" : (_allowGuildSelection ? reason : ""),
                IsLocked = false,
            };

            vm.Icon = IconForType(vm.Type);
            AllGuilds.Add(vm);

            if (isSelected)
                preloadDetails.Add(EnsureCardDetailsLoadedAsync(vm, rec));
        }

        if (preloadDetails.Count > 0)
            await Task.WhenAll(preloadDetails);

        Refilter();
        RecomputeDraftAlignments();
        _notifyWizardGatingChanged();
    }

    private async Task EnsureCardDetailsLoadedAsync(GuildCardVm card, GuildRecord? record = null)
    {
        if (card.DetailsLoaded)
            return;

        Task pendingTask;
        lock (_detailLoadGate)
        {
            if (card.DetailsLoaded)
                return;

            if (_detailLoadTasks.TryGetValue(card.Name, out var existingTask))
            {
                pendingTask = existingTask;
            }
            else
            {
                pendingTask = EnsureCardDetailsLoadedCoreAsync(card, record);
                _detailLoadTasks[card.Name] = pendingTask;
            }
        }

        await pendingTask;
    }

    private async Task EnsureCardDetailsLoadedCoreAsync(GuildCardVm card, GuildRecord? record)
    {
        card.IsDetailsLoading = true;
        try
        {
            var rec = record;
            if (rec == null && !_guildRecords.TryGetValue(card.Name, out rec))
                rec = new GuildRecord();

            var details = await BuildCardDetailsAsync(card.Name, rec ?? new GuildRecord());
            card.ApplyDetails(details);
        }
        finally
        {
            card.IsDetailsLoading = false;
            lock (_detailLoadGate)
                _detailLoadTasks.Remove(card.Name);
        }
    }

    private async Task<GuildCardDetailsVm> BuildCardDetailsAsync(string guildName, GuildRecord record)
    {
        var rec = record ?? new GuildRecord();
        var benefits = rec.Benefits ?? new GuildBenefits();

        var denominatorRef = (rec.DenominationalMiracle?.Ref ?? string.Empty).Trim();
        var miracleLookup = denominatorRef.Length > 0
            ? await GetMiracleLookupAsync()
            : EmptyMiracleLookup;

        return new GuildCardDetailsVm
        {
            PreRequisites = rec.PreRequisites ?? string.Empty,
            Restrictions = rec.Restrictions ?? string.Empty,
            Ethos = rec.Ethos ?? string.Empty,
            Background = rec.Background ?? string.Empty,
            LoreSections = BuildLoreSections(rec),
            BasicBenefits = FormatBenefitList(benefits.Basic),
            IntermediateBenefits = FormatBenefitList(benefits.Intermediate),
            AdvancedBenefits = FormatBenefitList(benefits.Advanced),
            BasicOptionGroups = BuildBenefitOptionGroups(guildName, "Basic", benefits.Basic),
            IntermediateOptionGroups = BuildBenefitOptionGroups(guildName, "Intermediate", benefits.Intermediate),
            AdvancedOptionGroups = BuildBenefitOptionGroups(guildName, "Advanced", benefits.Advanced),
            MiracleRows = BuildMiracleRows(rec.MiracleList),
            DenominationalMiracle = BuildDenominationalMiracle(rec, miracleLookup)
        };
    }

    private async Task<IReadOnlyDictionary<string, GuildMiracleDefinition>> GetMiracleLookupAsync()
    {
        if (_miracleLookupCache != null)
            return _miracleLookupCache;

        _miracleLookupTask ??= LoadMiracleLookupAsync();
        _miracleLookupCache = await _miracleLookupTask;
        return _miracleLookupCache;
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
                AddBrackets(rec.Brackets);

                foreach (var pathClassName in ExtractPathClassNames(rec))
                {
                    if (!_creationDataService.TryGetByName(classMap, pathClassName, out var pathRecord) || pathRecord == null)
                        continue;

                    AddBrackets(pathRecord.Brackets);
                }
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

        void AddBrackets(IEnumerable<string>? brackets)
        {
            foreach (var b in brackets ?? Enumerable.Empty<string>())
            {
                var normalized = NormalizeLookupKey(b);
                if (normalized.Length > 0)
                    _currentClassBrackets.Add(normalized);
            }
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

        if (!string.IsNullOrWhiteSpace(availability.RequiredGuild)
            && !_draft.Guilds.Contains(availability.RequiredGuild, StringComparer.OrdinalIgnoreCase))
        {
            return new AvailabilityResult(false, $"Requires {availability.RequiredGuild}.");
        }

        if (availability.Rules is { Count: > 0 })
        {
            if (!MeetsAvailabilityRules(availability.Rules))
                return new AvailabilityResult(false, "Does not meet availability rule.");

            return AvailabilityResult.Ok();
        }

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

    private bool MeetsAvailabilityRules(IEnumerable<RuleClause> rules)
    {
        var availableAlignments = CharacterDraft.ComputeAvailableAlignments(_getNonGuildRules());
        if (availableAlignments.Count == 0 && _draft.Alignment is Alignment selectedAlignment)
            availableAlignments.Add(selectedAlignment);

        IEnumerable<string> ResolveValues(string field)
        {
            var tokens = SplitRuleFieldTokens(field);
            if (tokens.Count == 0)
                return Array.Empty<string>();

            if (tokens.Count == 1)
                return ResolveSingleField(tokens[0]);

            var merged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var token in tokens)
            {
                foreach (var value in ResolveSingleField(token))
                {
                    if (!string.IsNullOrWhiteSpace(value))
                        merged.Add(value);
                }
            }

            return merged;

            IEnumerable<string> ResolveSingleField(string normalizedField)
                => normalizedField switch
                {
                    "class" or "classes" => ToSingleValue(_currentClassName),
                    "bracket" or "brackets" => _currentClassBrackets,
                    "race" or "races" => ToSingleValue(_currentRaceName),
                    "racesubtype" or "subtype" => _currentRaceSelections,
                    "peopletype" or "peopletypes" => _currentPeopleTypes,
                    "alignmentorder" => availableAlignments.Select(a => a.Order.ToString()),
                    "alignmentmoral" => availableAlignments.Select(a => a.Moral.ToString()),
                    "status" or "statuses" => Array.Empty<string>(),
                    "guild" or "guilds" => _draft.Guilds ?? new List<string>(),
                    _ => Array.Empty<string>()
                };
        }

        return RuleTreeEvaluator.Evaluate(rules, ResolveValues, NormalizeLookupKey);
    }

    private static List<string> SplitRuleFieldTokens(string? field)
    {
        var raw = (field ?? string.Empty).Trim();
        if (raw.Length == 0)
            return new List<string>();

        return raw
            .Split(new[] { '|', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeRuleField)
            .Where(token => token.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static IEnumerable<string> ExtractPathClassNames(CharacterClassRecord classRecord)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddName(classRecord.Path);

        foreach (var buyAs in classRecord.BuyAs ?? Enumerable.Empty<string>())
            AddName(ParsePathClassFromBuyAs(buyAs));

        return names;

        void AddName(string? value)
        {
            var trimmed = (value ?? string.Empty).Trim();
            if (trimmed.Length > 0)
                names.Add(trimmed);
        }
    }

    private static string ParsePathClassFromBuyAs(string? buyAs)
    {
        var raw = (buyAs ?? string.Empty).Trim();
        if (raw.Length == 0)
            return string.Empty;

        var match = Regex.Match(raw, @"\bclass\s+(.+)$", RegexOptions.IgnoreCase);
        if (match.Success)
            return match.Groups[1].Value.Trim();

        return raw;
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

    private static string NormalizeRuleField(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return new string(value
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
    }

    private static IEnumerable<string> ToSingleValue(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        return trimmed.Length == 0
            ? Array.Empty<string>()
            : new[] { trimmed };
    }

    private static string IconForType(string type)
    {
        var t = (type ?? "").Trim().ToLowerInvariant();
        if (t == "political") return "🏛️";
        if (t == "professional") return "🛠️";
        if (t.Contains("relig")) return "⛪";
        return "📜";
    }

    private static string NormalizeLogoPath(string? rawPath)
    {
        var value = (rawPath ?? string.Empty).Trim();
        if (value.Length == 0)
            return string.Empty;

        value = value.Replace('\\', '/');

        if (value.StartsWith("~/", StringComparison.Ordinal))
            value = value[2..];

        const string imagesPrefix = "Resources/Images/";
        if (value.StartsWith(imagesPrefix, StringComparison.OrdinalIgnoreCase))
            value = value[imagesPrefix.Length..];

        var lastSlash = value.LastIndexOf('/');
        if (lastSlash >= 0 && lastSlash < value.Length - 1)
            value = value[(lastSlash + 1)..];

        return value.Trim();
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
                .Select(m => m.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(m => m, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (names.Count == 0)
                continue;

            foreach (var name in names)
            {
                rows.Add(new GuildMiracleRowVm
                {
                    LevelText = entry.Level?.ToString() ?? entry.Key,
                    Name = name
                });
            }
        }

        for (var i = 0; i < rows.Count; i++)
            rows[i].RowBackgroundColor = i % 2 == 0 ? "#FFFFFF" : "#F9FAFB";

        return rows;
    }

    private static async Task<IReadOnlyDictionary<string, GuildMiracleDefinition>> LoadMiracleLookupAsync()
    {
        try
        {
            var json = await ServiceHelper.ReadPackageTextAsync("words_from_above/miracles.json");
            var list = JsonSerializer.Deserialize<List<GuildMiracleDefinition>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<GuildMiracleDefinition>();

            return list
                .Where(m => !string.IsNullOrWhiteSpace(m.name))
                .GroupBy(m => m.name.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, GuildMiracleDefinition>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static GuildDenominationalMiracleVm? BuildDenominationalMiracle(
        GuildRecord record,
        IReadOnlyDictionary<string, GuildMiracleDefinition> miracleLookup)
    {
        var reference = (record.DenominationalMiracle?.Ref ?? string.Empty).Trim();
        if (reference.Length == 0)
            return null;

        var note = (record.DenominationalMiracleNote ?? string.Empty).Trim();
        if (!miracleLookup.TryGetValue(reference, out var miracle))
        {
            return new GuildDenominationalMiracleVm
            {
                Name = reference,
                Cost = "todo",
                Alignment = "todo",
                Duration = "todo",
                Range = "todo",
                Description = "todo",
                Verbal = "todo",
                Sphere = "todo",
                Gesture = "todo",
                IsAdvancedText = "todo",
                Note = note
            };
        }

        var cost = (miracle.level ?? string.Empty).Trim();
        if (cost.Length == 0 && miracle.power > 0)
            cost = $"{miracle.power}sp";

        return new GuildDenominationalMiracleVm
        {
            Name = (miracle.name ?? string.Empty).Trim(),
            Cost = cost,
            Alignment = (miracle.alignment ?? string.Empty).Trim(),
            Duration = (miracle.duration ?? string.Empty).Trim(),
            Range = (miracle.range ?? string.Empty).Trim(),
            Description = (miracle.description ?? string.Empty).Trim(),
            Verbal = (miracle.verbal ?? string.Empty).Trim(),
            Sphere = (miracle.sphere ?? string.Empty).Trim(),
            Gesture = (miracle.gesture ?? string.Empty).Trim(),
            IsAdvancedText = miracle.isAdvanced ? "Yes" : "No",
            Note = note
        };
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

    private void RebuildTypeFilterChips()
    {
        var distinctTypes = TypeFilters
            .Where(t => !string.IsNullOrWhiteSpace(t) && !string.Equals(t, AllTypeFilterValue, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _selectedTypeFilters.RemoveWhere(type => !distinctTypes.Contains(type, StringComparer.OrdinalIgnoreCase));
        var isAllSelected = _selectedTypeFilters.Count == 0;

        TypeFilterChips.Clear();
        TypeFilterChips.Add(new GuildTypeFilterChipVm(AllTypeFilterValue, isAllSelected));
        foreach (var type in distinctTypes)
        {
            TypeFilterChips.Add(new GuildTypeFilterChipVm(
                type,
                _selectedTypeFilters.Contains(type)));
        }
    }

    private void ToggleTypeFilterChip(GuildTypeFilterChipVm? chip)
    {
        if (!_useMultiTypeFilters || chip == null)
            return;

        if (string.Equals(chip.Type, AllTypeFilterValue, StringComparison.OrdinalIgnoreCase))
        {
            _selectedTypeFilters.Clear();
        }
        else
        {
            if (!_selectedTypeFilters.Add(chip.Type))
                _selectedTypeFilters.Remove(chip.Type);
        }

        RebuildTypeFilterChips();
        Refilter();
    }

    private void Refilter()
    {
        var text = (SearchText ?? "").Trim();
        var type = (SelectedTypeFilter ?? AllTypeFilterValue).Trim();

        bool Matches(GuildCardVm g)
        {
            if (_useMultiTypeFilters)
            {
                if (_selectedTypeFilters.Count > 0 && !_selectedTypeFilters.Contains(g.Type))
                    return false;
            }
            else
            {
                if (!string.Equals(type, AllTypeFilterValue, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(g.Type, type, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            if (string.IsNullOrWhiteSpace(text))
                return true;

            if (_searchByNameOnly)
                return g.Name?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false;

            if (_guildRecords.TryGetValue(g.Name, out var rec) && rec != null)
                return RecordMatchesSearch(g.Name, rec, text);

            return g.Name?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false;
        }

        var list = AllGuilds.Where(Matches)
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        FilteredGuilds.Clear();
        foreach (var g in list)
            FilteredGuilds.Add(g);

        Raise(nameof(SelectedCount));
    }
    public int SelectedCount => _draft.Guilds.Count;

    private void RecomputeDraftAlignments()
    {
        _draft.SetAvailableAlignmentsFromRules(_getNonGuildRules());

        // Refresh selectability now that the world changed
        foreach (var card in AllGuilds)
        {
            card.IsSelected = _draft.Guilds.Contains(card.Name, StringComparer.OrdinalIgnoreCase);
            var availability = EvaluateAvailabilityForCurrentContext(card.Name);
            var selectable = availability.Allowed;

            // Allow already-selected guilds to stay selectable so the user can deselect them
            card.IsSelectable = !_allowGuildSelection || (selectable || card.IsSelected);

            if (!availability.Allowed)
                card.NotSelectableReason = availability.Reason;
            else
                card.NotSelectableReason = "";

            if (!_allowGuildSelection)
                card.NotSelectableReason = "";
        }

        RaiseSelectedGuildsChanged();
    }

    private async Task ToggleExpandedAsync(GuildCardVm? item)
    {
        if (item == null || !item.CanToggleSelection)
            return;

        foreach (var g in FilteredGuilds)
        {
            if (!ReferenceEquals(g, item) && g.IsExpanded)
                g.IsExpanded = false;
        }

        if (!item.IsExpanded)
            await EnsureCardDetailsLoadedAsync(item);

        item.IsExpanded = !item.IsExpanded;
    }

    private static bool RecordMatchesSearch(string guildName, GuildRecord record, string text)
    {
        if (guildName.Contains(text, StringComparison.OrdinalIgnoreCase))
            return true;

        var rec = record ?? new GuildRecord();
        if ((rec.PreRequisites ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase))
            return true;

        if ((rec.Restrictions ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase))
            return true;

        if ((rec.Ethos ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase))
            return true;

        if ((rec.Background ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase))
            return true;

        if ((rec.DenominationalMiracle?.Ref ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase))
            return true;

        if ((rec.DenominationalMiracleNote ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase))
            return true;

        if (MiracleListContainsText(rec.MiracleList, text))
            return true;

        var benefits = rec.Benefits ?? new GuildBenefits();
        return BenefitEntriesContainText(benefits.Basic, text)
               || BenefitEntriesContainText(benefits.Intermediate, text)
               || BenefitEntriesContainText(benefits.Advanced, text);
    }

    private static bool MiracleListContainsText(Dictionary<string, List<string>>? miracleList, string text)
    {
        foreach (var pair in miracleList ?? new Dictionary<string, List<string>>())
        {
            if ((pair.Key ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase))
                return true;

            foreach (var name in pair.Value ?? new List<string>())
            {
                if ((name ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    private static bool BenefitEntriesContainText(IEnumerable<GuildBenefitEntry>? entries, string text)
    {
        foreach (var entry in entries ?? Enumerable.Empty<GuildBenefitEntry>())
        {
            var direct = FormatBenefit(entry?.Ability);
            if (direct.Contains(text, StringComparison.OrdinalIgnoreCase))
                return true;

            foreach (var option in entry?.Options ?? new List<GuildBenefitOption>())
            {
                foreach (var ability in option.Abilities ?? new List<AbilityDefinition>())
                {
                    var optionLine = FormatBenefit(ability);
                    if (optionLine.Contains(text, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
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

    private static List<GuildLoreSectionVm> BuildLoreSections(GuildRecord rec)
    {
        var sections = new List<GuildLoreSectionVm>();

        AddIfPresent("PRE-REQUISITES", rec.PreRequisites);
        AddIfPresent("RESTRICTIONS", rec.Restrictions);
        AddIfPresent("ETHOS", rec.Ethos);
        AddIfPresent("BACKGROUND", rec.Background);

        return sections;

        void AddIfPresent(string header, string? text)
        {
            var value = CleanLoreText(text);
            if (value.Length == 0)
                return;

            sections.Add(new GuildLoreSectionVm(header, value));
        }
    }

    private static string CleanLoreText(string? text)
    {
        var value = (text ?? string.Empty).Replace("\r\n", "\n").Trim();
        if (value.Length == 0)
            return string.Empty;

        value = MiracleListLoreBlockRegex.Replace(value, "\n");
        value = DenominationalMiracleLoreBlockRegex.Replace(value, "\n");
        value = MiracleStatLoreBlockRegex.Replace(value, "\n");
        value = Regex.Replace(value, @"\n{3,}", "\n\n");
        value = ExpandParagraphSpacing(value);
        return value.Trim();
    }

    private static string ExpandParagraphSpacing(string value)
    {
        var lines = value.Split('\n');
        if (lines.Length < 2)
            return value;

        var sb = new StringBuilder(value.Length + 64);
        for (var i = 0; i < lines.Length; i++)
        {
            var current = lines[i];
            sb.Append(current);

            if (i >= lines.Length - 1)
                continue;

            var currentTrimmed = current.Trim();
            var nextTrimmed = lines[i + 1].Trim();

            if (currentTrimmed.Length == 0 || nextTrimmed.Length == 0)
            {
                sb.Append('\n');
                continue;
            }

            var currentIsList = ListPrefixRegex.IsMatch(currentTrimmed);
            var nextIsList = ListPrefixRegex.IsMatch(nextTrimmed);
            sb.Append(currentIsList || nextIsList ? '\n' : "\n\n");
        }

        return Regex.Replace(sb.ToString(), @"\n{3,}", "\n\n");
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

    private async Task ToggleSelectedAsync(GuildCardVm? item)
    {
        if (!_allowGuildSelection)
        {
            await ToggleExpandedAsync(item);
            return;
        }

        if (item is null || (!item.IsSelectable && !item.IsSelected))
            return;

        var availability = EvaluateAvailabilityForCurrentContext(item.Name);
        if (!item.IsSelected && !availability.Allowed)
        {
            item.NotSelectableReason = availability.Reason;
            return;
        }

        var idx = _draft.Guilds.FindIndex(g => string.Equals(g, item.Name, StringComparison.OrdinalIgnoreCase));
        var nowSelected = idx < 0;

        if (nowSelected)
        {
            _draft.Guilds.Add(item.Name);
            item.IsSelected = true;
            if (_guildRecords.TryGetValue(item.Name, out var rec) && rec != null)
                await EnsureCardDetailsLoadedAsync(item, rec);
            else
                await EnsureCardDetailsLoadedAsync(item);
        }
        else
        {
            _draft.Guilds.RemoveAt(idx);
            item.IsSelected = false;
        }

        ApplyAvailabilityToCurrentSelection();

        Raise(nameof(SelectedCount));
        RecomputeDraftAlignments();
        _notifyWizardGatingChanged();

        if (_refreshDraftAbilitiesAsync != null)
            await _refreshDraftAbilitiesAsync();
    }
}

public sealed class GuildTypeFilterChipVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _isSelected;

    public GuildTypeFilterChipVm(string type, bool isSelected)
    {
        Type = (type ?? string.Empty).Trim();
        _isSelected = isSelected;
    }

    public string Type { get; }
    public string Label => Type;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;

            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }
}

public sealed class GuildCardDetailsVm
{
    public string PreRequisites { get; init; } = string.Empty;
    public string Restrictions { get; init; } = string.Empty;
    public string Ethos { get; init; } = string.Empty;
    public string Background { get; init; } = string.Empty;
    public List<GuildLoreSectionVm> LoreSections { get; init; } = new();
    public List<string> BasicBenefits { get; init; } = new();
    public List<string> IntermediateBenefits { get; init; } = new();
    public List<string> AdvancedBenefits { get; init; } = new();
    public List<GuildBenefitOptionGroupVm> BasicOptionGroups { get; init; } = new();
    public List<GuildBenefitOptionGroupVm> IntermediateOptionGroups { get; init; } = new();
    public List<GuildBenefitOptionGroupVm> AdvancedOptionGroups { get; init; } = new();
    public List<GuildMiracleRowVm> MiracleRows { get; init; } = new();
    public GuildDenominationalMiracleVm? DenominationalMiracle { get; init; }
}

public sealed class GuildCardVm : INotifyPropertyChanged
{
    public GuildCardVm()
    {
        ToggleMiracleListCommand = new Command(() => IsMiracleListExpanded = !IsMiracleListExpanded);
        ToggleBenefitsCommand = new Command(() => IsBenefitsExpanded = !IsBenefitsExpanded);
        ToggleDenominationalMiracleCommand = new Command(() => IsDenominationalMiracleExpanded = !IsDenominationalMiracleExpanded);
    }

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
            Raise(nameof(CanToggleSelection));
            Raise(nameof(HasNotSelectableReason));
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
            Raise(nameof(HasNotSelectableReason));
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
    public string SelectionActionGlyph => IsSelected ? "\uf068" : "\uf067";
    public bool CanToggleSelection => IsSelectable || IsSelected;
    public bool HasNotSelectableReason => !CanToggleSelection && !string.IsNullOrWhiteSpace(NotSelectableReason);
    public string Type { get; set; } = "";
    public string Icon { get; set; } = "📜";
    public string Logo { get; set; } = "";
    public bool HasLogo => !string.IsNullOrWhiteSpace(Logo);

    private bool _detailsLoaded;
    public bool DetailsLoaded
    {
        get => _detailsLoaded;
        private set
        {
            if (_detailsLoaded == value)
                return;

            _detailsLoaded = value;
            Raise();
        }
    }

    private bool _isDetailsLoading;
    public bool IsDetailsLoading
    {
        get => _isDetailsLoading;
        set
        {
            if (_isDetailsLoading == value)
                return;

            _isDetailsLoading = value;
            Raise();
        }
    }

    public string PreRequisites { get; set; } = "";
    public string Restrictions { get; set; } = "";
    public string Ethos { get; set; } = "";
    public string Background { get; set; } = "";
    public List<GuildLoreSectionVm> LoreSections { get; set; } = new();
    public List<string> BasicBenefits { get; set; } = new();
    public List<string> IntermediateBenefits { get; set; } = new();
    public List<string> AdvancedBenefits { get; set; } = new();
    public List<GuildBenefitOptionGroupVm> BasicOptionGroups { get; set; } = new();
    public List<GuildBenefitOptionGroupVm> IntermediateOptionGroups { get; set; } = new();
    public List<GuildBenefitOptionGroupVm> AdvancedOptionGroups { get; set; } = new();
    public List<GuildMiracleRowVm> MiracleRows { get; set; } = new();
    public GuildDenominationalMiracleVm? DenominationalMiracle { get; set; }

    public void ApplyDetails(GuildCardDetailsVm? details)
    {
        var source = details ?? new GuildCardDetailsVm();

        PreRequisites = source.PreRequisites ?? string.Empty;
        Restrictions = source.Restrictions ?? string.Empty;
        Ethos = source.Ethos ?? string.Empty;
        Background = source.Background ?? string.Empty;
        LoreSections = source.LoreSections ?? new List<GuildLoreSectionVm>();
        BasicBenefits = source.BasicBenefits ?? new List<string>();
        IntermediateBenefits = source.IntermediateBenefits ?? new List<string>();
        AdvancedBenefits = source.AdvancedBenefits ?? new List<string>();
        BasicOptionGroups = source.BasicOptionGroups ?? new List<GuildBenefitOptionGroupVm>();
        IntermediateOptionGroups = source.IntermediateOptionGroups ?? new List<GuildBenefitOptionGroupVm>();
        AdvancedOptionGroups = source.AdvancedOptionGroups ?? new List<GuildBenefitOptionGroupVm>();
        MiracleRows = source.MiracleRows ?? new List<GuildMiracleRowVm>();
        DenominationalMiracle = source.DenominationalMiracle;

        DetailsLoaded = true;

        Raise(nameof(PreRequisites));
        Raise(nameof(Restrictions));
        Raise(nameof(Ethos));
        Raise(nameof(Background));
        Raise(nameof(LoreSections));
        Raise(nameof(BasicBenefits));
        Raise(nameof(IntermediateBenefits));
        Raise(nameof(AdvancedBenefits));
        Raise(nameof(BasicOptionGroups));
        Raise(nameof(IntermediateOptionGroups));
        Raise(nameof(AdvancedOptionGroups));
        Raise(nameof(MiracleRows));
        Raise(nameof(DenominationalMiracle));
        Raise(nameof(HasLore));
        Raise(nameof(HasRestrictions));
        Raise(nameof(HasBasic));
        Raise(nameof(HasIntermediate));
        Raise(nameof(HasAdvanced));
        Raise(nameof(HasMiracles));
        Raise(nameof(HasDenominationalMiracle));
        Raise(nameof(HasAnyBenefits));
        Raise(nameof(HasBasicOptions));
        Raise(nameof(HasIntermediateOptions));
        Raise(nameof(HasAdvancedOptions));
        Raise(nameof(AreBenefitOptionsComplete));
    }

    public bool HasLore => LoreSections.Count > 0;
    public bool HasRestrictions => !string.IsNullOrWhiteSpace(Restrictions);

    public bool HasBasic => BasicBenefits.Count > 0 || BasicOptionGroups.Count > 0;
    public bool HasIntermediate => IntermediateBenefits.Count > 0 || IntermediateOptionGroups.Count > 0;
    public bool HasAdvanced => AdvancedBenefits.Count > 0 || AdvancedOptionGroups.Count > 0;
    public bool HasMiracles => MiracleRows.Count > 0;
    public bool HasDenominationalMiracle => DenominationalMiracle != null;

    public bool HasAnyBenefits => HasBasic || HasIntermediate || HasAdvanced;
    public bool HasBasicOptions => BasicOptionGroups.Count > 0;
    public bool HasIntermediateOptions => IntermediateOptionGroups.Count > 0;
    public bool HasAdvancedOptions => AdvancedOptionGroups.Count > 0;
    public bool AreBenefitOptionsComplete =>
        BasicOptionGroups.All(g => g.HasSelection)
        && IntermediateOptionGroups.All(g => g.HasSelection)
        && AdvancedOptionGroups.All(g => g.HasSelection);

    public double ChevronRotation => IsExpanded ? 180 : 0;
    public double MiracleListChevronRotation => IsMiracleListExpanded ? 180 : 0;
    public double BenefitsChevronRotation => IsBenefitsExpanded ? 180 : 0;
    public double DenominationalMiracleChevronRotation => IsDenominationalMiracleExpanded ? 180 : 0;

    public ICommand ToggleMiracleListCommand { get; }
    public ICommand ToggleBenefitsCommand { get; }
    public ICommand ToggleDenominationalMiracleCommand { get; }

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

    private bool _isMiracleListExpanded;
    public bool IsMiracleListExpanded
    {
        get => _isMiracleListExpanded;
        set
        {
            if (_isMiracleListExpanded == value) return;
            _isMiracleListExpanded = value;
            Raise();
            Raise(nameof(MiracleListChevronRotation));
        }
    }

    private bool _isBenefitsExpanded = true;
    public bool IsBenefitsExpanded
    {
        get => _isBenefitsExpanded;
        set
        {
            if (_isBenefitsExpanded == value) return;
            _isBenefitsExpanded = value;
            Raise();
            Raise(nameof(BenefitsChevronRotation));
        }
    }

    private bool _isDenominationalMiracleExpanded = true;
    public bool IsDenominationalMiracleExpanded
    {
        get => _isDenominationalMiracleExpanded;
        set
        {
            if (_isDenominationalMiracleExpanded == value) return;
            _isDenominationalMiracleExpanded = value;
            Raise();
            Raise(nameof(DenominationalMiracleChevronRotation));
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
            Raise(nameof(SelectionActionGlyph));
            Raise(nameof(CanToggleSelection));
            Raise(nameof(HasNotSelectableReason));
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
    public string LevelText { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string RowBackgroundColor { get; set; } = "#FFFFFF";
}

public sealed class GuildDenominationalMiracleVm
{
    public string Name { get; init; } = string.Empty;
    public string Cost { get; init; } = string.Empty;
    public string Alignment { get; init; } = string.Empty;
    public string Duration { get; init; } = string.Empty;
    public string Range { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Verbal { get; init; } = string.Empty;
    public string Sphere { get; init; } = string.Empty;
    public string Gesture { get; init; } = string.Empty;
    public string IsAdvancedText { get; init; } = string.Empty;
    public string Note { get; init; } = string.Empty;
    public bool HasNote => !string.IsNullOrWhiteSpace(Note);

    public bool Contains(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return Name.Contains(text, StringComparison.OrdinalIgnoreCase)
               || Cost.Contains(text, StringComparison.OrdinalIgnoreCase)
               || Alignment.Contains(text, StringComparison.OrdinalIgnoreCase)
               || Duration.Contains(text, StringComparison.OrdinalIgnoreCase)
               || Range.Contains(text, StringComparison.OrdinalIgnoreCase)
               || Description.Contains(text, StringComparison.OrdinalIgnoreCase)
               || Verbal.Contains(text, StringComparison.OrdinalIgnoreCase)
               || Sphere.Contains(text, StringComparison.OrdinalIgnoreCase)
               || Gesture.Contains(text, StringComparison.OrdinalIgnoreCase)
               || IsAdvancedText.Contains(text, StringComparison.OrdinalIgnoreCase)
               || Note.Contains(text, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class GuildMiracleDefinition
{
    public int power { get; set; }
    public string name { get; set; } = string.Empty;
    public string description { get; set; } = string.Empty;
    public string verbal { get; set; } = string.Empty;
    public string range { get; set; } = string.Empty;
    public string duration { get; set; } = string.Empty;
    public string gesture { get; set; } = string.Empty;
    public string level { get; set; } = string.Empty;
    public string sphere { get; set; } = string.Empty;
    public bool isAdvanced { get; set; }
    public string alignment { get; set; } = string.Empty;
}

public sealed class GuildLoreSectionVm : INotifyPropertyChanged
{
    private const int CollapsedLines = 4;

    private bool _isExpanded;

    public GuildLoreSectionVm(string header, string text)
    {
        Header = (header ?? string.Empty).Trim();
        Text = (text ?? string.Empty).Trim();
        ToggleCommand = new Command(ToggleExpanded);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Header { get; }
    public string Text { get; }
    public ICommand ToggleCommand { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
                return;

            _isExpanded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MaxLines)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ToggleLabel)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowToggle)));
        }
    }

    public int MaxLines => IsExpanded ? -1 : CollapsedLines;

    public bool ShowToggle => CanExpand || IsExpanded;
    public string ToggleLabel => IsExpanded ? "... see less" : "... see more";

    private bool CanExpand
    {
        get
        {
            if (Text.Length > 260)
                return true;

            var lineCount = Text.Count(c => c == '\n') + 1;
            return lineCount > CollapsedLines;
        }
    }

    private void ToggleExpanded()
        => IsExpanded = !IsExpanded;
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

            var availability = guild.Availability ?? new GuildAvailability();
            if (RuleTreeEvaluator.ContainsPositiveInValue(
                    availability.Rules,
                    field: "PeopleType",
                    value: normalizedToken,
                    normalizeField: NormalizePeopleTypeToken,
                    normalizeValue: NormalizePeopleTypeToken))
            {
                allowed.Add(rec.Key);
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
