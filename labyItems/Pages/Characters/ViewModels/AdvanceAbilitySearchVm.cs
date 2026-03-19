using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Input;
using labyItems.Models.Characters;
using labyItems.Models.Abilities;
using labyItems.Services;
using Microsoft.Maui.Graphics;

namespace labyItems.Pages.Characters.ViewModels;

public sealed class AdvanceAbilitySearchVm : INotifyPropertyChanged
{
    private const int MaxVisibleResults = 100;
    private static readonly SemaphoreSlim AbilityCacheLock = new(1, 1);
    private static IReadOnlyList<CachedAbilityEntry>? _cachedAbilities;
    private static IReadOnlyDictionary<string, EvolutionService.AbilityResult>? _cachedAbilityLookup;
    private static IReadOnlyList<string>? _cachedSourceBooks;

    private readonly AdvanceCharacterVm _root;
    private readonly CharacterDraft _draft;
    private readonly IAbilityAvailabilityService _abilityAvailabilityService;
    private IReadOnlyDictionary<string, CharacterClassRecord> _classes
        = new Dictionary<string, CharacterClassRecord>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, PeopleRecord> _races
        = new Dictionary<string, PeopleRecord>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, bool> _availabilityByAbilityKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _selectedAbilityKeys = new(StringComparer.OrdinalIgnoreCase);
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

    public AdvanceAbilitySearchVm(
        AdvanceCharacterVm root,
        IAbilityAvailabilityService? abilityAvailabilityService = null)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _draft = _root.Draft;
        _abilityAvailabilityService = abilityAvailabilityService
            ?? ServiceHelper.ResolveService<IAbilityAvailabilityService>()
            ?? new AbilityAvailabilityService();

        OpenFiltersCommand = new Command(OpenFilters);
        CancelFiltersCommand = new Command(CancelFilters);
        ApplyFiltersCommand = new Command(ApplyFilters);
        ToggleSourceBookFilterCommand = new Command<AdvanceAbilitySearchFilterOptionVm<string>>(ToggleSourceBookFilter);
        ToggleTableFilterCommand = new Command<AdvanceAbilitySearchFilterOptionVm<int>>(ToggleTableFilter);
        ToggleSelectAbilityCommand = new Command<AdvanceAbilitySearchItemVm>(ToggleSelectAbility);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!Set(ref _searchText, value ?? string.Empty))
                return;

            Refilter();
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
        ? $"Selected: {SelectedCount}"
        : "No abilities selected";

    public async Task LoadAsync()
    {
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
        Refilter();
        UpdateSelectedCount();
    }

    public void CommitSelection()
        => _root.AddAdvancementAbilities(GetSelectedAbilities());

    public void ClearSelections()
    {
        _selectedAbilityKeys.Clear();

        foreach (var ability in FilteredAbilities)
            ability.IsSelected = false;

        UpdateSelectedCount();
    }

    public IReadOnlyList<EvolutionService.AbilityResult> GetSelectedAbilities()
        => _selectedAbilityKeys
            .Select(key => _abilityLookupByKey.TryGetValue(key, out var ability) ? ability : null)
            .Where(ability => ability is not null)
            .Cast<EvolutionService.AbilityResult>()
            .OrderBy(ability => ability.Index, StringComparer.OrdinalIgnoreCase)
            .ToList();

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
        Refilter();
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

    private void BuildFilterOptions()
    {
        SourceBookFilters.Clear();
        foreach (var sourceBook in _cachedSourceBooks ?? Array.Empty<string>())
            SourceBookFilters.Add(new AdvanceAbilitySearchFilterOptionVm<string>(sourceBook, sourceBook));

        TableFilters.Clear();
        for (var table = 0; table <= 12; table++)
            TableFilters.Add(new AdvanceAbilitySearchFilterOptionVm<int>(table.ToString(), table));
    }

    private void Refilter()
    {
        var query = SearchText.Trim();
        var filtered = new List<AdvanceAbilitySearchItemVm>(MaxVisibleResults);
        foreach (var ability in _abilityEntries)
        {
            if (query.Length > 0
                && !ability.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (_activeSourceBookFilters.Count > 0
                && !_activeSourceBookFilters.Contains(ability.SourceBook))
            {
                continue;
            }

            if (_activeTableFilters.Count > 0
                && !_activeTableFilters.Contains(ability.Table))
            {
                continue;
            }

            var isAvailable = GetAvailability(ability);
            if (isAvailable != _activeAvailableOnly)
                continue;

            var item = new AdvanceAbilitySearchItemVm(
                ability.Ability,
                ability.SourceBook,
                isAvailable,
                _selectedAbilityKeys.Contains(ability.Key));
            item.PropertyChanged += OnAbilityItemPropertyChanged;
            filtered.Add(item);
            if (filtered.Count >= MaxVisibleResults)
                break;
        }

        ReplaceFilteredItems(filtered);

        Raise(nameof(HasResults));
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
    }

    private void UpdateSelectedCount()
        => SelectedCount = _selectedAbilityKeys.Count;

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
        if (_availabilityByAbilityKey.TryGetValue(ability.Key, out var cached))
            return cached;

        var resolved = _abilityAvailabilityService.IsAvailable(
            ability.Ability.AvailabilityRules,
            _draft,
            _classes,
            _races);
        _availabilityByAbilityKey[ability.Key] = resolved;
        return resolved;
    }

    private void ReplaceFilteredItems(IReadOnlyList<AdvanceAbilitySearchItemVm> source)
    {
        foreach (var item in FilteredAbilities)
            item.PropertyChanged -= OnAbilityItemPropertyChanged;

        ReplaceItems(FilteredAbilities, source);
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
