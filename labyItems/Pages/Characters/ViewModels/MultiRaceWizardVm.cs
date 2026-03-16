using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using labyItems.Models.Characters;
using labyItems.Models.ViewModels;
using labyItems.Models.Rules;
using labyItems.Services;
using Microsoft.Maui.Graphics;

namespace labyItems.Pages.Characters.ViewModels;

public sealed class MultiRaceWizardVm : INotifyPropertyChanged
{
    private const int SearchStep = 0;
    private const int DetailStep = 1;

    private static readonly Color PurchasedRowColor = Color.FromArgb("#DCFCE7");
    private static readonly Color DefaultRowColor = Colors.Transparent;

    private readonly CharacterDraft _draft;
    private readonly IAbilityAvailabilityService _abilityAvailabilityService;
    private readonly string _initialStorageKey;
    private readonly bool _openDetailOnLoad;

    private IReadOnlyDictionary<string, CharacterClassRecord> _classes
        = new Dictionary<string, CharacterClassRecord>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, PeopleRecord> _races
        = new Dictionary<string, PeopleRecord>(StringComparer.OrdinalIgnoreCase);

    private readonly ObservableCollection<MultiRaceSearchEntry> _allEntries = new();
    private readonly Dictionary<string, MultiRaceSearchEntry> _entriesByCardKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<LifeScalePoint>> _lifeScaleCache = new(StringComparer.OrdinalIgnoreCase);

    private int _stepIndex;
    private string _searchText = string.Empty;
    private MultiRaceSearchEntry? _selectedEntry;
    private int _selectedLevels;
    private bool _openSpecialisationAfterClose;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Func<Task>? CloseRequested;

    public ObservableCollection<ClassCardVm> FilteredClasses { get; } = new();
    public ObservableCollection<MultiRaceFilterChipVm> FilterChips { get; } = new();
    public ObservableCollection<MultiRaceLevelRowVm> SelectedLevelRows { get; } = new();

    public ICommand SelectClassCommand { get; }
    public ICommand ToggleExpandedCommand { get; }
    public ICommand ToggleFilterChipCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand NextCommand { get; }

    public MultiRaceWizardVm(
        CharacterDraft draft,
        IAbilityAvailabilityService? abilityAvailabilityService = null,
        string? initialStorageKey = null,
        bool openDetailOnLoad = false)
    {
        _draft = draft ?? throw new ArgumentNullException(nameof(draft));
        _abilityAvailabilityService = abilityAvailabilityService
                                      ?? ServiceHelper.ResolveService<IAbilityAvailabilityService>()
                                      ?? new AbilityAvailabilityService();
        _initialStorageKey = (initialStorageKey ?? string.Empty).Trim();
        _openDetailOnLoad = openDetailOnLoad;

        SelectClassCommand = new Command<ClassCardVm>(OnSelectClass);
        ToggleExpandedCommand = new Command<ClassCardVm>(card => _ = OnToggleExpandedAsync(card));
        ToggleFilterChipCommand = new Command<MultiRaceFilterChipVm>(ToggleFilterChip);
        BackCommand = new Command(() => _ = OnBackAsync());
        NextCommand = new Command(() => _ = OnNextAsync());
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

    public bool IsSearchStep => _stepIndex == SearchStep;
    public bool IsDetailStep => _stepIndex == DetailStep;

    public string StepDescription => IsSearchStep
        ? "Search and pick a multi-race"
        : "Review levels, costs, and save";

    public string StepCounterText => $"Step {_stepIndex + 1} of 2";

    public string NextButtonText => IsSearchStep
        ? "Next"
        : HasSpecialisationStep ? "Next" : "Save";

    public bool CanGoNext => IsSearchStep
        ? _selectedEntry != null
        : HasSelectedCard;

    public bool HasSelectedCard => _selectedEntry != null;
    public bool HasSpecialisationStep =>
        IsDetailStep
        && _selectedEntry != null
        && _selectedLevels > 0
        && HasChoiceSetRefsAtOrBelowLevel(_selectedEntry.Definition, _selectedLevels);
    public bool OpenSpecialisationAfterClose => _openSpecialisationAfterClose;

    public string SelectedCardName => _selectedEntry?.Card.Name ?? "No class selected";

    public string SelectedAvailabilityText => _selectedEntry == null
        ? string.Empty
        : _selectedEntry.AvailabilityDisplay;

    public bool ShowSelectedLifeScaleColumns => SelectedLevelRows.Any(row => row.HasLife);
    public bool HasAvailableClasses => FilteredClasses.Count > 0;

    public double SelectedMaxLevelDouble => _selectedEntry?.MaxLevel ?? 0;

    public double SelectedLevelsDouble
    {
        get => _selectedLevels;
        set
        {
            var rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);
            rounded = Math.Clamp(rounded, 0, _selectedEntry?.MaxLevel ?? 0);
            if (_selectedLevels == rounded)
                return;

            _selectedLevels = rounded;
            RefreshSelectedLevelHighlights();
            Raise(nameof(SelectedLevelsDouble));
            Raise(nameof(SelectedLevelsText));
            Raise(nameof(HasSpecialisationStep));
            Raise(nameof(NextButtonText));
        }
    }

    public string SelectedLevelsText => _selectedEntry == null
        ? "0 / 0"
        : $"{_selectedLevels} / {_selectedEntry.MaxLevel}";

    public async Task LoadAsync()
    {
        _classes = await ClassService.GetAllAsync();
        _races = await PeopleService.GetAllAsync();

        _allEntries.Clear();
        _entriesByCardKey.Clear();

        var catalog = await MultiRaceService.GetCatalogAsync();
        foreach (var pair in catalog.MultiRaces.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            var entry = await BuildSearchEntryAsync(pair.Key, pair.Value);
            if (entry == null)
                continue;

            _allEntries.Add(entry);
            _entriesByCardKey[entry.Card.Key] = entry;
        }

        BuildFilterChips();
        Refilter();

        var initial = FindEntryByStoredKey(_initialStorageKey);
        var savedKey = (_draft.MultiRaceKey ?? string.Empty).Trim();
        var firstSaved = FindEntryByStoredKey(savedKey);

        var selected = initial ?? firstSaved;
        if (selected != null)
        {
            SetSelectedEntry(selected);
            if (_openDetailOnLoad)
            {
                LoadSelectedEntryRows(selected);
                _stepIndex = DetailStep;
                RaiseStepState();
            }
        }

        Raise(nameof(CanGoNext));
    }

    private async Task<MultiRaceSearchEntry?> BuildSearchEntryAsync(string key, MultiRaceDefinition definition)
    {
        var available = FindAvailableOption(definition.AvailabilityOptions);
        if (available == null)
            return null;

        var displayName = !string.IsNullOrWhiteSpace(definition.DisplayName)
            ? definition.DisplayName.Trim()
            : key;

        var maxLevel = ResolveMaxLevel(definition);
        var levelRows = new List<LevelAbilityRowVm>(maxLevel);
        var detailRows = new List<MultiRaceLevelRowVm>(maxLevel);
        var lifeByLevel = new Dictionary<int, LifeScalePoint>();
        var costsByLevel = ResolveCostsByLevel(available, maxLevel);

        for (var level = 1; level <= maxLevel; level++)
        {
            var abilities = ResolveAbilitiesForLevel(definition, level);
            var life = await ResolveLifeForLevelAsync(definition, level);
            if (life.HasValue)
                lifeByLevel[level] = life.Value;

            var abilityNames = abilities
                .Select(ability => (ability.Name ?? string.Empty).Trim())
                .Where(name => name.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var abilityKeys = abilities
                .Select(ability => !string.IsNullOrWhiteSpace(ability.AbilityRef)
                    ? ability.AbilityRef.Trim()
                    : (ability.Name ?? string.Empty).Trim())
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            levelRows.Add(new LevelAbilityRowVm
            {
                Level = level,
                Body = life.HasValue ? life.Value.Body.ToString() : string.Empty,
                Loc = life.HasValue ? life.Value.Loc.ToString() : string.Empty,
                AbilityNames = abilityNames,
                AbilityDetailKeys = abilityKeys
            });

            var detailAbilityEntries = abilities
                .Where(ability => !string.IsNullOrWhiteSpace((ability.Name ?? string.Empty).Trim())
                                 || !string.IsNullOrWhiteSpace((ability.Effect ?? string.Empty).Trim()))
                .Select(ability =>
                {
                    var lookupKey = !string.IsNullOrWhiteSpace(ability.AbilityRef)
                        ? ability.AbilityRef.Trim()
                        : (ability.Name ?? string.Empty).Trim();
                    var displayName = (ability.Name ?? string.Empty).Trim();
                    if (displayName.Length == 0)
                        displayName = lookupKey;

                    return new MultiRaceAbilityDetailVm(
                        DisplayName: displayName,
                        LookupKey: lookupKey);
                })
                .Where(entry => entry.IsValid)
                .ToList();

            detailRows.Add(new MultiRaceLevelRowVm
            {
                Level = level,
                BodyText = life.HasValue ? life.Value.Body.ToString() : string.Empty,
                LocText = life.HasValue ? life.Value.Loc.ToString() : string.Empty,
                Cost = costsByLevel.TryGetValue(level, out var cost) ? cost : 0,
                AbilityEffectsText = BuildAbilityEffectsText(abilities),
                AbilityDetails = detailAbilityEntries,
                RowBackgroundColor = DefaultRowColor
            });
        }

        var bracketTags = ResolveBracketTags(definition, available);
        var (icon, category, parsedBracketTags) = ClassCardVm.ParseBrackets(bracketTags);

        var maxAc = ResolveMaxAc(definition);
        var firstLife = lifeByLevel.TryGetValue(1, out var lifeAtOne)
            ? lifeAtOne
            : default;

        var card = new ClassCardVm
        {
            Key = $"mr:{key}",
            Name = displayName,
            Category = category,
            Icon = icon,
            Summary = BuildSummary(detailRows),
            MaxAc = maxAc,
            TBLP = firstLife.Body,
            PowerBase = ResolvePowerBase(definition),
            LevelRows = new ObservableCollection<LevelAbilityRowVm>(levelRows),
            BracketTags = parsedBracketTags.Count > 0 ? parsedBracketTags : bracketTags,
            ShowBodyAndLoc = lifeByLevel.Count > 0,
            RaceName = (_draft.Race ?? string.Empty).Trim()
        };

        return new MultiRaceSearchEntry(
            key: key,
            card: card,
            definition: definition,
            maxLevel: maxLevel,
            availabilityDisplay: available.Display,
            costsByLevel: costsByLevel,
            detailRows: detailRows);
    }

    private static string ResolvePowerBase(MultiRaceDefinition definition)
        => MultiPathWizardHelpers.ResolvePowerBase<
            MultiRaceDefinition,
            MultiRaceAvailabilityOption,
            MultiRaceLevelAbility,
            MultiRaceSystemEffect>(definition);

    private static int ResolveMaxLevel(MultiRaceDefinition definition)
        => MultiPathWizardHelpers.ResolveMaxLevel<
            MultiRaceDefinition,
            MultiRaceAvailabilityOption,
            MultiRaceLevelAbility,
            MultiRaceSystemEffect>(definition);

    private MultiRaceAvailabilityOption? FindAvailableOption(IEnumerable<MultiRaceAvailabilityOption>? options)
        => MultiPathWizardHelpers.FindAvailableOption(
            options,
            rules => _abilityAvailabilityService.IsAvailable(rules, _draft, _classes, _races));

    private static Dictionary<int, int> ResolveCostsByLevel(MultiRaceAvailabilityOption option, int maxLevel)
        => MultiPathWizardHelpers.ResolveCostsByLevel(option, maxLevel);

    private static IReadOnlyList<MultiRaceLevelAbility> ResolveAbilitiesForLevel(MultiRaceDefinition definition, int level)
        => MultiPathWizardHelpers.ResolveAbilitiesForLevel<
            MultiRaceDefinition,
            MultiRaceAvailabilityOption,
            MultiRaceLevelAbility,
            MultiRaceSystemEffect>(definition, level);

    private static string BuildSummary(IReadOnlyList<MultiRaceLevelRowVm> detailRows)
        => MultiPathWizardHelpers.BuildSummary(detailRows, row => row.AbilityEffectsText);

    private static string BuildAbilityEffectsText(IReadOnlyList<MultiRaceLevelAbility> abilities)
        => MultiPathWizardHelpers.BuildAbilityEffectsText(abilities);

    private int ResolveMaxAc(MultiRaceDefinition definition)
        => MultiPathWizardHelpers.ResolveMaxAc<
            MultiRaceDefinition,
            MultiRaceAvailabilityOption,
            MultiRaceLevelAbility,
            MultiRaceSystemEffect,
            MultiRaceLifeScaleReference>(
            definition,
            MatchesRules);

    private async Task<LifeScalePoint?> ResolveLifeForLevelAsync(MultiRaceDefinition definition, int level)
        => await MultiPathWizardHelpers.ResolveLifeForLevelAsync<
            MultiRaceDefinition,
            MultiRaceAvailabilityOption,
            MultiRaceLevelAbility,
            MultiRaceSystemEffect,
            MultiRaceLifeScaleReference>(
            definition,
            level,
            MatchesRules,
            ResolveLifeScaleReferenceAsync);

    private async Task<LifeScalePoint?> ResolveLifeScaleReferenceAsync(
        MultiRaceLifeScaleReference? reference,
        int fallbackLevel)
    {
        if (reference == null || string.IsNullOrWhiteSpace(reference.Class))
            return null;

        var className = reference.Class.Trim();
        if (!_lifeScaleCache.TryGetValue(className, out var points))
        {
            var race = (_draft.Race ?? string.Empty).Trim();
            points = await LifeScalesService.GetLifeScaleAsync(race, className);
            _lifeScaleCache[className] = points;
        }

        if (points.Count == 0)
            return null;

        var level = reference.Level > 0 ? reference.Level : fallbackLevel;
        if (level < 1 || level > points.Count)
            return null;

        return points[level - 1];
    }

    private bool MatchesRules(IEnumerable<RuleClause>? rules)
    {
        var list = rules?.Where(rule => rule != null && rule.IsValid).ToList();
        if (list == null || list.Count == 0)
            return true;

        return _abilityAvailabilityService.IsAvailable(list, _draft, _classes, _races);
    }

    private IReadOnlyList<string> ResolveBracketTags(
        MultiRaceDefinition definition,
        MultiRaceAvailabilityOption availableOption)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in availableOption.Rules ?? new List<RuleClause>())
        {
            var field = NormalizeToken(rule.Field);
            if (!field.Equals("bracket", StringComparison.OrdinalIgnoreCase)
                && !field.Equals("brackets", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var value in rule.Value ?? new List<string>())
            {
                var trimmed = (value ?? string.Empty).Trim();
                if (trimmed.Length > 0)
                    set.Add(trimmed);
            }
        }

        foreach (var sourceClass in ResolveAvailabilitySourceClasses(definition, availableOption))
        {
            var classRecord = ResolveClassRecord(sourceClass);
            if (classRecord?.Brackets == null)
                continue;

            foreach (var bracket in classRecord.Brackets)
            {
                var label = ExtractBracketLabel(bracket);
                if (!string.IsNullOrWhiteSpace(label))
                    set.Add(label);
            }
        }

        if (set.Count == 0
            && availableOption.Rules.Any(rule =>
                NormalizeToken(rule.Field).Equals("race", StringComparison.OrdinalIgnoreCase)
                || NormalizeToken(rule.Field).Equals("races", StringComparison.OrdinalIgnoreCase)))
        {
            set.Add("Race");
        }

        if (set.Count == 0)
            set.Add("Any");

        return set.ToList();
    }

    private static IEnumerable<string> ResolveAvailabilitySourceClasses(
        MultiRaceDefinition definition,
        MultiRaceAvailabilityOption availableOption)
    {
        var all = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in definition.AvailabilityOptions
                     .SelectMany(ResolveClassesFromOption)
                     .Concat(ResolveClassesFromOption(availableOption)))
        {
            var trimmed = (source ?? string.Empty).Trim();
            if (trimmed.Length > 0)
                all.Add(trimmed);
        }

        return all;
    }

    private static IEnumerable<string> ResolveClassesFromOption(MultiRaceAvailabilityOption? option)
    {
        if (option == null)
            yield break;

        if (!string.IsNullOrWhiteSpace(option.Source))
            yield return option.Source;

        foreach (var rule in option.Rules ?? new List<RuleClause>())
        {
            var field = NormalizeToken(rule.Field);
            if (!field.Equals("class", StringComparison.OrdinalIgnoreCase)
                && !field.Equals("classes", StringComparison.OrdinalIgnoreCase)
                && !field.Equals("sourceclass", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var value in rule.Value ?? new List<string>())
                yield return value;
        }
    }

    private CharacterClassRecord? ResolveClassRecord(string sourceClass)
    {
        var trimmed = (sourceClass ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return null;

        if (_classes.TryGetValue(trimmed, out var direct))
            return direct;

        var normalized = NormalizeToken(trimmed);
        foreach (var pair in _classes)
        {
            if (NormalizeToken(pair.Key).Equals(normalized, StringComparison.OrdinalIgnoreCase))
                return pair.Value;
        }

        foreach (var pair in _classes)
        {
            var key = NormalizeToken(pair.Key);
            if (key.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || normalized.Contains(key, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }

    private static string ExtractBracketLabel(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
            return string.Empty;

        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length <= 1)
            return text;

        var first = parts[0];
        var looksLikeEmoji = first.Any(ch => !char.IsLetterOrDigit(ch));
        return looksLikeEmoji ? string.Join(" ", parts.Skip(1)) : text;
    }

    private static string NormalizeToken(string? raw)
        => new string((raw ?? string.Empty)
            .Trim()
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

    private void BuildFilterChips()
    {
        FilterChips.Clear();
        FilterChips.Add(new MultiRaceFilterChipVm("All", "All", true));

        var labels = _allEntries
            .SelectMany(entry => entry.Card.BracketLabels)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(label => label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var label in labels)
            FilterChips.Add(new MultiRaceFilterChipVm(label, label, false));
    }

    private void ToggleFilterChip(MultiRaceFilterChipVm? chip)
    {
        if (chip == null)
            return;

        if (chip.IsAll)
        {
            foreach (var option in FilterChips)
                option.IsSelected = option.IsAll;

            Refilter();
            return;
        }

        chip.IsSelected = !chip.IsSelected;
        var allChip = FilterChips.FirstOrDefault(option => option.IsAll);
        if (allChip != null)
            allChip.IsSelected = false;

        if (!FilterChips.Any(option => !option.IsAll && option.IsSelected) && allChip != null)
            allChip.IsSelected = true;

        Refilter();
    }

    private void Refilter()
    {
        var query = (_searchText ?? string.Empty).Trim();
        var selectedFilters = FilterChips
            .Where(option => option.IsSelected && !option.IsAll)
            .Select(option => option.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var allSelected = selectedFilters.Count == 0
            || FilterChips.FirstOrDefault(option => option.IsAll)?.IsSelected == true;

        var selectedKey = _selectedEntry?.Card.Key ?? string.Empty;

        var filtered = _allEntries.Where(entry =>
        {
            var card = entry.Card;
            var matchesQuery = query.Length == 0
                               || card.Name.Contains(query, StringComparison.OrdinalIgnoreCase);
            if (!matchesQuery)
                return false;

            if (allSelected)
                return true;

            return card.BracketLabels.Any(label => selectedFilters.Contains(label));
        });

        FilteredClasses.Clear();
        foreach (var entry in filtered.OrderBy(entry => entry.Card.Name, StringComparer.OrdinalIgnoreCase))
            FilteredClasses.Add(entry.Card);

        Raise(nameof(HasAvailableClasses));

        if (selectedKey.Length > 0 && _entriesByCardKey.TryGetValue(selectedKey, out var selected))
            SetSelectedEntry(selected, preserveStep: true);
    }

    private void OnSelectClass(ClassCardVm? card)
    {
        if (card == null)
            return;

        if (!_entriesByCardKey.TryGetValue(card.Key, out var entry))
            return;

        SetSelectedEntry(entry);
    }

    private void SetSelectedEntry(MultiRaceSearchEntry entry, bool preserveStep = false)
    {
        _selectedEntry = entry;
        foreach (var item in _allEntries)
            item.Card.IsSelected = ReferenceEquals(item, entry);

        if (!preserveStep)
        {
            _stepIndex = SearchStep;
            RaiseStepState();
        }

        Raise(nameof(HasSelectedCard));
        Raise(nameof(SelectedCardName));
        Raise(nameof(SelectedAvailabilityText));
        Raise(nameof(CanGoNext));
    }

    private async Task OnToggleExpandedAsync(ClassCardVm? card)
    {
        if (card == null)
            return;

        foreach (var entry in _allEntries)
        {
            if (ReferenceEquals(entry.Card, card))
                continue;

            entry.Card.IsExpanded = false;
        }

        card.IsExpanded = !card.IsExpanded;
        await Task.CompletedTask;
    }

    private async Task OnBackAsync()
    {
        _openSpecialisationAfterClose = false;
        if (IsDetailStep)
        {
            _stepIndex = SearchStep;
            RaiseStepState();
            return;
        }

        if (CloseRequested != null)
            await CloseRequested.Invoke();
    }

    private async Task OnNextAsync()
    {
        if (IsSearchStep)
        {
            if (_selectedEntry == null)
                return;

            LoadSelectedEntryRows(_selectedEntry);
            _stepIndex = DetailStep;
            RaiseStepState();
            return;
        }

        if (_selectedEntry == null)
            return;

        _openSpecialisationAfterClose = false;
        if (_selectedLevels <= 0)
        {
            _draft.MultiRaceKey = string.Empty;
            _draft.MultiRaceLevel = 0;
            _draft.MultiRaceChoiceSelections ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _draft.MultiRaceChoiceSelections.Clear();
        }
        else
        {
            if (!_draft.MultiRaceKey.Equals(_selectedEntry.StorageKey, StringComparison.OrdinalIgnoreCase))
            {
                _draft.MultiRaceChoiceSelections ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _draft.MultiRaceChoiceSelections.Clear();
            }

            _draft.MultiRaceKey = _selectedEntry.StorageKey;
            _draft.MultiRaceLevel = _selectedLevels;
            _openSpecialisationAfterClose =
                HasChoiceSetRefsAtOrBelowLevel(_selectedEntry.Definition, _selectedLevels);
        }

        if (CloseRequested != null)
            await CloseRequested.Invoke();
    }

    private void LoadSelectedEntryRows(MultiRaceSearchEntry entry)
    {
        SelectedLevelRows.Clear();
        foreach (var row in entry.DetailRows)
            SelectedLevelRows.Add(row.Clone());

        var saved = 0;
        if (_draft.MultiRaceLevel > 0
            && _draft.MultiRaceKey.Equals(entry.StorageKey, StringComparison.OrdinalIgnoreCase))
        {
            saved = _draft.MultiRaceLevel;
        }

        _selectedLevels = Math.Clamp(saved, 0, entry.MaxLevel);
        RefreshSelectedLevelHighlights();

        Raise(nameof(SelectedLevelsDouble));
        Raise(nameof(SelectedLevelsText));
        Raise(nameof(SelectedMaxLevelDouble));
        Raise(nameof(ShowSelectedLifeScaleColumns));
        Raise(nameof(HasSelectedCard));
        Raise(nameof(SelectedCardName));
        Raise(nameof(SelectedAvailabilityText));
        Raise(nameof(HasSpecialisationStep));
        Raise(nameof(NextButtonText));
    }

    private void RefreshSelectedLevelHighlights()
    {
        foreach (var row in SelectedLevelRows)
        {
            row.RowBackgroundColor = row.Level <= _selectedLevels
                ? PurchasedRowColor
                : DefaultRowColor;
        }
    }

    public MultiRaceSearchEntry? GetSelectedEntry()
        => _selectedEntry;

    public MultiRaceSearchEntry? FindEntryByCard(ClassCardVm? card)
    {
        if (card == null)
            return null;

        return _entriesByCardKey.TryGetValue(card.Key, out var found)
            ? found
            : null;
    }

    public MultiRaceSearchEntry? FindEntryByStoredKey(string? storedKey)
    {
        var key = (storedKey ?? string.Empty).Trim();
        if (key.Length == 0)
            return null;

        return _allEntries.FirstOrDefault(entry =>
            entry.StorageKey.Equals(key, StringComparison.OrdinalIgnoreCase)
            || entry.Card.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<MultiRaceAbilityDetailVm> GetAbilityDetailsForRow(MultiRaceLevelRowVm? row)
        => row?.AbilityDetails ?? Array.Empty<MultiRaceAbilityDetailVm>();

    private void RaiseStepState()
    {
        Raise(nameof(IsSearchStep));
        Raise(nameof(IsDetailStep));
        Raise(nameof(StepDescription));
        Raise(nameof(StepCounterText));
        Raise(nameof(HasSpecialisationStep));
        Raise(nameof(NextButtonText));
        Raise(nameof(CanGoNext));
    }

    private static bool HasChoiceSetRefsAtOrBelowLevel(MultiRaceDefinition definition, int selectedLevel)
    {
        if (selectedLevel <= 0)
            return false;

        for (var level = 1; level <= selectedLevel; level++)
        {
            if (!definition.Levels.TryGetValue(level.ToString(), out var list) || list == null)
                continue;

            foreach (var ability in list)
            {
                if (ability?.ChoiceSetRefs == null)
                    continue;

                if (ability.ChoiceSetRefs.Any(item => !string.IsNullOrWhiteSpace(item)))
                    return true;
            }
        }

        return false;
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

    public sealed class MultiRaceSearchEntry
    {
        public MultiRaceSearchEntry(
            string key,
            ClassCardVm card,
            MultiRaceDefinition definition,
            int maxLevel,
            string availabilityDisplay,
            IReadOnlyDictionary<int, int> costsByLevel,
            IReadOnlyList<MultiRaceLevelRowVm> detailRows)
        {
            Key = key;
            StorageKey = key;
            Card = card;
            Definition = definition;
            MaxLevel = maxLevel;
            AvailabilityDisplay = availabilityDisplay;
            CostsByLevel = costsByLevel;
            DetailRows = detailRows;
        }

        public string Key { get; }
        public string StorageKey { get; }
        public ClassCardVm Card { get; }
        public MultiRaceDefinition Definition { get; }
        public int MaxLevel { get; }
        public string AvailabilityDisplay { get; }
        public IReadOnlyDictionary<int, int> CostsByLevel { get; }
        public IReadOnlyList<MultiRaceLevelRowVm> DetailRows { get; }
    }
}

public sealed class MultiRaceFilterChipVm : INotifyPropertyChanged
{
    private bool _isSelected;

    public event PropertyChangedEventHandler? PropertyChanged;

    public MultiRaceFilterChipVm(string key, string label, bool isSelected)
    {
        Key = key ?? string.Empty;
        Label = label ?? string.Empty;
        _isSelected = isSelected;
    }

    public string Key { get; }
    public string Label { get; }

    public bool IsAll => Key.Equals("All", StringComparison.OrdinalIgnoreCase);

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

public sealed class MultiRaceLevelRowVm : INotifyPropertyChanged
{
    private Color _rowBackgroundColor = Colors.Transparent;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Level { get; set; }
    public string BodyText { get; set; } = string.Empty;
    public string LocText { get; set; } = string.Empty;
    public int Cost { get; set; }
    public string AbilityEffectsText { get; set; } = string.Empty;
    public IReadOnlyList<MultiRaceAbilityDetailVm> AbilityDetails { get; set; } = Array.Empty<MultiRaceAbilityDetailVm>();

    public bool HasAbilityDetails => AbilityDetails.Any(item => item.IsValid);
    public bool HasLife => !string.IsNullOrWhiteSpace(BodyText) || !string.IsNullOrWhiteSpace(LocText);
    public string CostText => Cost > 0 ? Cost.ToString() : "-";

    public Color RowBackgroundColor
    {
        get => _rowBackgroundColor;
        set
        {
            if (_rowBackgroundColor == value)
                return;

            _rowBackgroundColor = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RowBackgroundColor)));
        }
    }

    public MultiRaceLevelRowVm Clone()
    {
        return new MultiRaceLevelRowVm
        {
            Level = Level,
            BodyText = BodyText,
            LocText = LocText,
            Cost = Cost,
            AbilityEffectsText = AbilityEffectsText,
            AbilityDetails = AbilityDetails.ToList(),
            RowBackgroundColor = RowBackgroundColor
        };
    }
}

public sealed record MultiRaceAbilityDetailVm(string DisplayName, string LookupKey)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(DisplayName) || !string.IsNullOrWhiteSpace(LookupKey);
}
