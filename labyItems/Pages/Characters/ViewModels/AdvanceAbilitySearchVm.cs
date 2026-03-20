using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Input;
using labyItems.Models.Characters;
using labyItems.Models.Abilities;
using labyItems.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Graphics;

namespace labyItems.Pages.Characters.ViewModels;

public sealed class AdvanceAbilitySearchVm : INotifyPropertyChanged, IDisposable
{
    private const int MaxVisibleResults = 100;
    private const int SearchDebounceMs = 500;
    private static readonly SemaphoreSlim AbilityCacheLock = new(1, 1);
    private static IReadOnlyList<CachedAbilityEntry>? _cachedAbilities;
    private static IReadOnlyDictionary<string, EvolutionService.AbilityResult>? _cachedAbilityLookup;
    private static IReadOnlyList<string>? _cachedSourceBooks;

    private readonly AdvanceCharacterVm _root;
    private readonly CharacterDraft _draft;
    private readonly IAbilityAvailabilityService _abilityAvailabilityService;
    private readonly IAbilityChoiceSetResolverService _abilityChoiceSetResolverService;
    private IReadOnlyDictionary<string, CharacterClassRecord> _classes
        = new Dictionary<string, CharacterClassRecord>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, PeopleRecord> _races
        = new Dictionary<string, PeopleRecord>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, AvailabilityCacheEntry> _availabilityByAbilityKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _availabilityCacheLock = new();
    private readonly HashSet<string> _selectedAbilityKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _focusedAbilityKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _activeSourceBookFilters = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<int> _activeTableFilters = new();
    private IReadOnlyList<CachedAbilityEntry> _abilityEntries = Array.Empty<CachedAbilityEntry>();
    private IReadOnlyDictionary<string, EvolutionService.AbilityResult> _abilityLookupByKey
        = new Dictionary<string, EvolutionService.AbilityResult>(StringComparer.OrdinalIgnoreCase);

    private string _searchText = string.Empty;
    private bool _isFilterModalOpen;
    private bool _pendingAvailableOnly = true;
    private bool _activeAvailableOnly = true;
    private int _selectedCount;
    private bool _showSelectedOnly;
    private int _refilterVersion;
    private CancellationTokenSource? _refilterCts;
    private bool _isDisposed;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<AdvanceAbilitySearchItemVm> FilteredAbilities { get; } = new();
    public ObservableCollection<AdvanceAbilitySearchFilterOptionVm<string>> SourceBookFilters { get; } = new();
    public ObservableCollection<AdvanceAbilitySearchFilterOptionVm<int>> TableFilters { get; } = new();

    public ICommand OpenFiltersCommand { get; }
    public ICommand CancelFiltersCommand { get; }
    public ICommand ApplyFiltersCommand { get; }
    public ICommand ToggleSourceBookFilterCommand { get; }
    public ICommand ToggleTableFilterCommand { get; }
    public ICommand ToggleSelectAbilityCommand { get; }
    public ICommand ToggleSelectedOnlyCommand { get; }

    public AdvanceAbilitySearchVm(
        AdvanceCharacterVm root,
        IAbilityAvailabilityService? abilityAvailabilityService = null,
        IAbilityChoiceSetResolverService? abilityChoiceSetResolverService = null)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _draft = _root.Draft;
        _abilityAvailabilityService = abilityAvailabilityService
            ?? ServiceHelper.ResolveService<IAbilityAvailabilityService>()
            ?? new AbilityAvailabilityService();
        _abilityChoiceSetResolverService = abilityChoiceSetResolverService
            ?? ServiceHelper.ResolveService<IAbilityChoiceSetResolverService>()
            ?? new AbilityChoiceSetResolverService();

        OpenFiltersCommand = new Command(OpenFilters);
        CancelFiltersCommand = new Command(CancelFilters);
        ApplyFiltersCommand = new Command(ApplyFilters);
        ToggleSourceBookFilterCommand = new Command<AdvanceAbilitySearchFilterOptionVm<string>>(ToggleSourceBookFilter);
        ToggleTableFilterCommand = new Command<AdvanceAbilitySearchFilterOptionVm<int>>(ToggleTableFilter);
        ToggleSelectAbilityCommand = new Command<AdvanceAbilitySearchItemVm>(ToggleSelectAbility);
        ToggleSelectedOnlyCommand = new Command(ToggleSelectedOnly);
    }

    public CharacterDraft Draft => _draft;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!Set(ref _searchText, value ?? string.Empty))
                return;

            ScheduleRefilter(debounce: true);
        }
    }

    public bool IsFilterModalOpen
    {
        get => _isFilterModalOpen;
        private set => Set(ref _isFilterModalOpen, value);
    }

    public bool PendingAvailableOnly
    {
        get => _pendingAvailableOnly;
        set => Set(ref _pendingAvailableOnly, value);
    }

    public bool HasResults => FilteredAbilities.Count > 0;

    public int SelectedCount
    {
        get => _selectedCount;
        private set
        {
            if (!Set(ref _selectedCount, value))
                return;

            Raise(nameof(HasSelectedAbilities));
            Raise(nameof(SelectedSummaryText));
        }
    }

    public bool HasSelectedAbilities => SelectedCount > 0;

    public string SelectedSummaryText => HasSelectedAbilities
        ? ShowSelectedOnly
            ? $"Selected: {SelectedCount} (showing selected)"
            : $"Selected: {SelectedCount}"
        : "Selected: 0";

    public bool ShowSelectedOnly
    {
        get => _showSelectedOnly;
        private set
        {
            if (!Set(ref _showSelectedOnly, value))
                return;

            Raise(nameof(SelectedSummaryText));
        }
    }

    public async Task LoadAsync()
    {
        var dbInitializer = ServiceHelper.ResolveService<IDatabaseInitializer>();
        if (dbInitializer != null)
            await dbInitializer.InitializeAsync();

        var classesTask = ClassService.GetAllAsync();
        var racesTask = PeopleService.GetAllAsync();
        var abilityCacheTask = EnsureAbilityCacheAsync();
        await Task.WhenAll(classesTask, racesTask, abilityCacheTask);
        _classes = classesTask.Result;
        _races = racesTask.Result;
        _abilityEntries = _cachedAbilities ?? Array.Empty<CachedAbilityEntry>();
        _abilityLookupByKey = _cachedAbilityLookup
                              ?? new Dictionary<string, EvolutionService.AbilityResult>(StringComparer.OrdinalIgnoreCase);
        _availabilityByAbilityKey.Clear();
        _selectedAbilityKeys.Clear();

        BuildFilterOptions();
        ScheduleRefilter(debounce: false);
        UpdateSelectedCount();
    }

    public void CommitSelection()
        => CommitSelection(GetSelectedAbilities());

    public void CommitSelection(IEnumerable<EvolutionService.AbilityResult>? selectedAbilities)
        => _root.AddAdvancementAbilities(selectedAbilities);

    public void ClearSelections()
    {
        _selectedAbilityKeys.Clear();
        _focusedAbilityKeys.Clear();
        ShowSelectedOnly = false;

        foreach (var ability in FilteredAbilities)
            ability.IsSelected = false;

        UpdateSelectedCount();
        ScheduleRefilter(debounce: false);
    }

    public IReadOnlyList<EvolutionService.AbilityResult> GetSelectedAbilities()
        => _selectedAbilityKeys
            .Select(key => _abilityLookupByKey.TryGetValue(key, out var ability) ? ability : null)
            .Where(ability => ability is not null)
            .Cast<EvolutionService.AbilityResult>()
            .OrderBy(ability => ability.Index, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public async Task<IReadOnlyList<AdvanceAbilitySpecialisationRequest>> BuildSpecialisationRequestsAsync(
        IEnumerable<EvolutionService.AbilityResult>? selectedAbilities = null,
        CancellationToken cancellationToken = default)
    {
        var selected = (selectedAbilities ?? GetSelectedAbilities())
            .Where(ability => ability != null)
            .ToList();
        if (selected.Count == 0)
            return Array.Empty<AdvanceAbilitySpecialisationRequest>();

        var occurrencesByAbilityKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var requests = new List<AdvanceAbilitySpecialisationRequest>();

        foreach (var ability in selected)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var refs = await _abilityChoiceSetResolverService.ResolveChoiceSetRefsAsync(ability, cancellationToken);
            if (refs.Count == 0)
                continue;

            var abilityKey = BuildAbilityKey(ability);
            var displayName = EvolutionService.NormalizeAbilityDisplayText(ability.Index);
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = abilityKey;
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = "Ability";

            if (!occurrencesByAbilityKey.TryGetValue(abilityKey, out var previous))
                previous = 0;

            var occurrence = previous + 1;
            occurrencesByAbilityKey[abilityKey] = occurrence;

            requests.Add(new AdvanceAbilitySpecialisationRequest(
                abilityKey,
                displayName,
                (ability.AbilityRef ?? string.Empty).Trim(),
                occurrence,
                refs.ToList()));
        }

        return requests;
    }

    public AbilityPrerequisiteCheckResult GetSelectionPrerequisiteIssues()
        => AbilityPrerequisiteService.Evaluate(
            GetSelectedAbilities(),
            BuildKnownAbilityTerms(),
            _abilityEntries.Select(entry => entry.Ability));

    public string BuildMissingPrerequisiteMessage(AbilityPrerequisiteCheckResult result)
    {
        if (!result.HasIssues)
            return string.Empty;

        var lines = result.Issues
            .Select(issue => $"{issue.AbilityName}: {string.Join(", ", issue.MissingPrerequisites)}")
            .ToList();
        return string.Join(Environment.NewLine, lines);
    }

    public void FocusMissingPrerequisites(AbilityPrerequisiteCheckResult result)
    {
        _focusedAbilityKeys.Clear();
        foreach (var key in result.MissingPrerequisiteKeys ?? Array.Empty<string>())
        {
            var normalized = (key ?? string.Empty).Trim();
            if (normalized.Length == 0)
                continue;

            var directKey = _abilityLookupByKey.ContainsKey(normalized)
                ? normalized
                : AbilityKey.Build(ResolveAbilityByTerm(normalized));

            if (!string.IsNullOrWhiteSpace(directKey))
                _focusedAbilityKeys.Add(directKey);
        }

        ShowSelectedOnly = false;
        SearchText = string.Empty;
        ScheduleRefilter(debounce: false);
    }

    private void OpenFilters()
    {
        PendingAvailableOnly = _activeAvailableOnly;

        foreach (var filter in SourceBookFilters)
            filter.IsSelected = _activeSourceBookFilters.Contains(filter.Value);

        foreach (var filter in TableFilters)
            filter.IsSelected = _activeTableFilters.Contains(filter.Value);

        IsFilterModalOpen = true;
    }

    private void CancelFilters()
        => IsFilterModalOpen = false;

    private void ApplyFilters()
    {
        _activeAvailableOnly = PendingAvailableOnly;

        _activeSourceBookFilters.Clear();
        foreach (var filter in SourceBookFilters.Where(filter => filter.IsSelected))
            _activeSourceBookFilters.Add(filter.Value);

        _activeTableFilters.Clear();
        foreach (var filter in TableFilters.Where(filter => filter.IsSelected))
            _activeTableFilters.Add(filter.Value);

        IsFilterModalOpen = false;
        ScheduleRefilter(debounce: false);
    }

    private static void ToggleSourceBookFilter(AdvanceAbilitySearchFilterOptionVm<string>? filter)
    {
        if (filter == null)
            return;

        filter.IsSelected = !filter.IsSelected;
    }

    private static void ToggleTableFilter(AdvanceAbilitySearchFilterOptionVm<int>? filter)
    {
        if (filter == null)
            return;

        filter.IsSelected = !filter.IsSelected;
    }

    private static void ToggleSelectAbility(AdvanceAbilitySearchItemVm? ability)
    {
        if (ability == null)
            return;

        ability.IsSelected = !ability.IsSelected;
    }

    private void ToggleSelectedOnly()
    {
        _focusedAbilityKeys.Clear();
        if (SelectedCount <= 0)
        {
            ShowSelectedOnly = false;
            ScheduleRefilter(debounce: false);
            return;
        }

        ShowSelectedOnly = !ShowSelectedOnly;
        ScheduleRefilter(debounce: false);
    }

    private void BuildFilterOptions()
    {
        SourceBookFilters.Clear();
        foreach (var sourceBook in _cachedSourceBooks ?? Array.Empty<string>())
            SourceBookFilters.Add(new AdvanceAbilitySearchFilterOptionVm<string>(sourceBook, sourceBook));

        TableFilters.Clear();
        for (var table = 0; table <= 12; table++)
            TableFilters.Add(new AdvanceAbilitySearchFilterOptionVm<int>(table.ToString(), table));
    }

    private IReadOnlyList<AdvanceAbilitySearchItemVm> ComputeFilteredItems(
        string query,
        IReadOnlySet<string> sourceBooks,
        IReadOnlySet<int> tables,
        bool availableOnly,
        bool selectedOnly,
        IReadOnlySet<string> focusedAbilityKeys,
        IReadOnlySet<string> selectedAbilityKeys,
        CancellationToken token)
    {
        var hasQuery = query.Length > 0;
        var filtered = new List<AdvanceAbilitySearchItemVm>(MaxVisibleResults);
        foreach (var ability in _abilityEntries)
        {
            if (token.IsCancellationRequested)
                break;

            if (query.Length > 0
                && !ability.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (focusedAbilityKeys.Count > 0
                && !focusedAbilityKeys.Contains(ability.Key))
            {
                continue;
            }

            if (selectedOnly
                && !selectedAbilityKeys.Contains(ability.Key))
            {
                continue;
            }

            var bypassCatalogFilters = focusedAbilityKeys.Count > 0 || selectedOnly;

            if (!bypassCatalogFilters
                && sourceBooks.Count > 0
                && !sourceBooks.Contains(ability.SourceBook))
            {
                continue;
            }

            if (!bypassCatalogFilters
                && tables.Count > 0
                && !tables.Contains(ability.Table))
            {
                continue;
            }

            var isAvailable = GetAvailability(ability);
            if (!bypassCatalogFilters
                && availableOnly
                && !isAvailable)
                continue;

            var item = new AdvanceAbilitySearchItemVm(
                ability.Ability,
                ability.SourceBook,
                isAvailable,
                selectedAbilityKeys.Contains(ability.Key));
            filtered.Add(item);
            if (!hasQuery && filtered.Count >= MaxVisibleResults)
                break;
        }

        if (hasQuery && filtered.Count > MaxVisibleResults)
        {
            filtered = filtered
                .OrderByDescending(item => item.Name.Equals(query, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(item => item.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                .ThenBy(item => item.Table)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Take(MaxVisibleResults)
                .ToList();
        }

        return filtered;
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        var cts = Interlocked.Exchange(ref _refilterCts, null);
        if (cts != null)
        {
            cts.Cancel();
            cts.Dispose();
        }

        foreach (var item in FilteredAbilities)
            item.PropertyChanged -= OnAbilityItemPropertyChanged;
    }

    private void ScheduleRefilter(bool debounce)
    {
        if (_isDisposed)
            return;

        Interlocked.Increment(ref _refilterVersion);

        var nextCts = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _refilterCts, nextCts);
        if (previous != null)
        {
            previous.Cancel();
            previous.Dispose();
        }

        _ = RunScheduledRefilterAsync(_refilterVersion, debounce, nextCts.Token);
    }

    private async Task RunScheduledRefilterAsync(int version, bool debounce, CancellationToken token)
    {
        try
        {
            if (debounce)
                await Task.Delay(SearchDebounceMs, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (_isDisposed || token.IsCancellationRequested || version != _refilterVersion)
            return;

        var query = SearchText.Trim();
        var sourceBooks = new HashSet<string>(_activeSourceBookFilters, StringComparer.OrdinalIgnoreCase);
        var tables = new HashSet<int>(_activeTableFilters);
        var selectedAbilityKeys = new HashSet<string>(_selectedAbilityKeys, StringComparer.OrdinalIgnoreCase);
        var focusedAbilityKeys = new HashSet<string>(_focusedAbilityKeys, StringComparer.OrdinalIgnoreCase);
        var availableOnly = _activeAvailableOnly;
        var selectedOnly = ShowSelectedOnly;

        IReadOnlyList<AdvanceAbilitySearchItemVm> filtered;
        try
        {
            filtered = await Task.Run(
                () => ComputeFilteredItems(
                    query,
                    sourceBooks,
                    tables,
                    availableOnly,
                    selectedOnly,
                    focusedAbilityKeys,
                    selectedAbilityKeys,
                    token),
                token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (_isDisposed || token.IsCancellationRequested || version != _refilterVersion)
            return;

        if (MainThread.IsMainThread)
        {
            ReplaceFilteredItems(filtered);
            Raise(nameof(HasResults));
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (_isDisposed || token.IsCancellationRequested || version != _refilterVersion)
                return;

            ReplaceFilteredItems(filtered);
            Raise(nameof(HasResults));
        });
    }

    private void OnAbilityItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(AdvanceAbilitySearchItemVm.IsSelected), StringComparison.Ordinal))
            return;

        if (sender is not AdvanceAbilitySearchItemVm ability)
            return;

        var key = BuildAbilityKey(ability.Ability);
        if (ability.IsSelected)
            _selectedAbilityKeys.Add(key);
        else
            _selectedAbilityKeys.Remove(key);

        UpdateSelectedCount();
        if (ShowSelectedOnly || _focusedAbilityKeys.Count > 0)
            ScheduleRefilter(debounce: false);
    }

    private void UpdateSelectedCount()
    {
        SelectedCount = _selectedAbilityKeys.Count;
        if (SelectedCount > 0 || !ShowSelectedOnly)
            return;

        ShowSelectedOnly = false;
        ScheduleRefilter(debounce: false);
    }

    private static string NormalizeSourceBook(string? sourceBook)
    {
        var value = (sourceBook ?? string.Empty).Trim();
        return value.Length == 0 ? "Unknown" : value;
    }

    private async Task EnsureAbilityCacheAsync()
    {
        if (_cachedAbilities is not null
            && _cachedAbilityLookup is not null
            && _cachedSourceBooks is not null)
        {
            return;
        }

        await AbilityCacheLock.WaitAsync();
        try
        {
            if (_cachedAbilities is not null
                && _cachedAbilityLookup is not null
                && _cachedSourceBooks is not null)
            {
                return;
            }

            var abilities = await EvolutionService.GetAllAbilitiesAsync();
            var entries = abilities
                .OrderBy(ability => ability.Index, StringComparer.OrdinalIgnoreCase)
                .Select(ability => new CachedAbilityEntry(
                    BuildAbilityKey(ability),
                    ability,
                    NormalizeSourceBook(ability.SourceBook)))
                .ToList();

            var lookup = new Dictionary<string, EvolutionService.AbilityResult>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries)
                lookup[entry.Key] = entry.Ability;

            var sourceBooks = entries
                .Select(entry => entry.SourceBook)
                .Where(sourceBook => !string.IsNullOrWhiteSpace(sourceBook))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(sourceBook => sourceBook, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _cachedAbilities = entries;
            _cachedAbilityLookup = lookup;
            _cachedSourceBooks = sourceBooks;
        }
        finally
        {
            AbilityCacheLock.Release();
        }
    }

    private bool GetAvailability(CachedAbilityEntry ability)
    {
        var contextSignature = BuildAvailabilityContextSignature();
        lock (_availabilityCacheLock)
        {
            if (_availabilityByAbilityKey.TryGetValue(ability.Key, out var cached)
                && string.Equals(cached.ContextSignature, contextSignature, StringComparison.Ordinal))
            {
                return cached.IsAvailable;
            }
        }

        var resolved = _abilityAvailabilityService.IsAvailable(
            ability.Ability.AvailabilityRules,
            _draft,
            _classes,
            _races);

        lock (_availabilityCacheLock)
        {
            _availabilityByAbilityKey[ability.Key] = new AvailabilityCacheEntry(contextSignature, resolved);
        }

        return resolved;
    }

    private string BuildAvailabilityContextSignature()
    {
        var className = (_draft.Class ?? string.Empty).Trim();
        var raceName = (_draft.Race ?? string.Empty).Trim();
        var secondClasses = (_draft.MultiClassLevels ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase))
            .Where(pair => pair.Value > 0 && !string.IsNullOrWhiteSpace(pair.Key))
            .Select(pair => pair.Key.Trim())
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase);

        return $"{className}|{raceName}|{string.Join(",", secondClasses)}";
    }

    private void ReplaceFilteredItems(IReadOnlyList<AdvanceAbilitySearchItemVm> source)
    {
        foreach (var item in FilteredAbilities)
            item.PropertyChanged -= OnAbilityItemPropertyChanged;

        ReplaceItems(FilteredAbilities, source);
        foreach (var item in FilteredAbilities)
            item.PropertyChanged += OnAbilityItemPropertyChanged;
    }

    private EvolutionService.AbilityResult? ResolveAbilityByTerm(string? rawKeyOrName)
    {
        var normalized = (rawKeyOrName ?? string.Empty).Trim();
        if (normalized.Length == 0)
            return null;

        if (_abilityLookupByKey.TryGetValue(normalized, out var byKey))
            return byKey;

        return _abilityEntries
            .Select(entry => entry.Ability)
            .FirstOrDefault(ability =>
                string.Equals(ability.AbilityRef, normalized, StringComparison.OrdinalIgnoreCase)
                || string.Equals(ability.Index, normalized, StringComparison.OrdinalIgnoreCase)
                || string.Equals(AbilityKey.BuildEvolutionFallback(ability), normalized, StringComparison.OrdinalIgnoreCase));
    }

    private IEnumerable<string> BuildKnownAbilityTerms()
    {
        foreach (var raw in _draft.AdvancementAbilities ?? new List<string>())
            yield return raw;

        foreach (var ability in _draft.Abilities ?? new List<AbilityDraft>())
        {
            yield return ability?.AbilityKey ?? string.Empty;
            yield return ability?.Name ?? string.Empty;
            yield return ability?.BattleboardNameOverride ?? string.Empty;
            yield return ability?.UpdateKey ?? string.Empty;
            yield return ability?.OverwriteKey ?? string.Empty;
        }
    }

    private static string BuildAbilityKey(EvolutionService.AbilityResult ability)
        => AbilityKey.Build(ability);

    private static void ReplaceItems<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source)
            target.Add(item);
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        Raise(propertyName);
        return true;
    }

    private void Raise([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

internal sealed record CachedAbilityEntry(
    string Key,
    EvolutionService.AbilityResult Ability,
    string SourceBook)
{
    public string Name => Ability.Index;
    public int Table => Ability.Table;
}

internal sealed record AvailabilityCacheEntry(
    string ContextSignature,
    bool IsAvailable);

public sealed record AdvanceAbilitySpecialisationRequest(
    string AbilityKey,
    string AbilityName,
    string AbilityRef,
    int Occurrence,
    IReadOnlyList<string> ChoiceSetRefs)
{
    public string StableId => BuildStableId(AbilityKey, Occurrence);

    public static string BuildStableId(string abilityKey, int occurrence)
    {
        var trimmedKey = (abilityKey ?? string.Empty).Trim();
        var resolvedOccurrence = Math.Max(1, occurrence);
        return $"{trimmedKey}::{resolvedOccurrence}";
    }

    public string BuildStorageKey(string choiceSetRef)
    {
        var choiceSet = (choiceSetRef ?? string.Empty).Trim();
        return $"advancement::{StableId}::{choiceSet}";
    }
}

public sealed class AdvanceAbilitySearchItemVm : INotifyPropertyChanged
{
    private bool _isSelected;

    public event PropertyChangedEventHandler? PropertyChanged;

    public AdvanceAbilitySearchItemVm(
        EvolutionService.AbilityResult ability,
        string sourceBook,
        bool isAvailable,
        bool isSelected = false)
    {
        Ability = ability;
        SourceBook = sourceBook;
        IsAvailable = isAvailable;
        _isSelected = isSelected;
    }

    public EvolutionService.AbilityResult Ability { get; }
    public string Name => Ability.Index;
    public string Description => Ability.Description;
    public int Cost => Ability.Cost;
    public int Table => Ability.Table;
    public string SourceBook { get; }
    public bool IsAvailable { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;

            _isSelected = value;
            Raise();
            Raise(nameof(SelectionGlyph));
            Raise(nameof(SelectionColor));
        }
    }

    public string SelectionGlyph => IsSelected ? "\uF058" : "\uF111";

    public Color SelectionColor => IsSelected
        ? Color.FromArgb("#7F1D1D")
        : Color.FromArgb("#6B7280");

    public string MetaText => $"Table {Math.Max(0, Table)} · Cost {Math.Max(0, Cost)}";

    public string AvailabilityText => IsAvailable ? "Available" : "Unavailable";

    private void Raise([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class AdvanceAbilitySearchFilterOptionVm<T> : INotifyPropertyChanged
{
    private bool _isSelected;

    public event PropertyChangedEventHandler? PropertyChanged;

    public AdvanceAbilitySearchFilterOptionVm(string label, T value)
    {
        Label = label ?? string.Empty;
        Value = value;
    }

    public string Label { get; }
    public T Value { get; }

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
