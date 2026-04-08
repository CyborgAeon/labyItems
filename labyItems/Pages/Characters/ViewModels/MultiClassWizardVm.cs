using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using labyItems.Models.Characters;
using labyItems.Models.ViewModels;
using labyItems.Models.Rules;
using labyItems.Services;
using labyItems.Helpers;
using Microsoft.Maui.Graphics;

namespace labyItems.Pages.Characters.ViewModels;

public sealed class MultiClassWizardVm : INotifyPropertyChanged
{
    private const int SearchStep = 0;
    private const int DetailStep = 1;
    private const string FilterKeyAll = "All";
    private const string FilterKeyAvailable = "Available";
    private const string ThirdBracketWarningMessage = "Non-standard warning: Can you add a third bracket to this character?";
    private const string RequiredBracketPurityWarningMessage = "Non-standard warning: This class requires bracket purity and your character currently has brackets outside the required bracket.";
    private const string ExistingBracketPurityWarningMessage = "Non-standard warning: This choice breaks bracket purity because this character already has a bracket-pure multi-class.";


    private static readonly Color PurchasedRowColor = Color.FromArgb("#DCFCE7");
    private static readonly Color DefaultRowColor = Colors.Transparent;
    private static readonly Color ThirdBracketCardColor = Color.FromArgb("#F3F4F6");
    private static readonly Color StandardCardColor = Colors.White;
    private static readonly HashSet<string> KnownBracketLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "Warrior",
        "Scout",
        "Priest",
        "Wizard",
        "Druid",
        "Neuro"
    };

    private readonly CharacterDraft _draft;
    private readonly IAbilityAvailabilityService _abilityAvailabilityService;
    private readonly string _initialStorageKey;
    private readonly bool _openDetailOnLoad;

    private IReadOnlyDictionary<string, CharacterClassRecord> _classes
        = new Dictionary<string, CharacterClassRecord>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, CharacterClassRecord> _classesByNormalizedKey
        = new Dictionary<string, CharacterClassRecord>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<KeyValuePair<string, CharacterClassRecord>> _classSearchIndex
        = Array.Empty<KeyValuePair<string, CharacterClassRecord>>();
    private IReadOnlyDictionary<string, PeopleRecord> _races
        = new Dictionary<string, PeopleRecord>(StringComparer.OrdinalIgnoreCase);

    private readonly ObservableCollection<MultiClassSearchEntry> _allEntries = new();
    private readonly Dictionary<string, MultiClassSearchEntry> _entriesByCardKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<LifeScalePoint>> _lifeScaleCache = new(StringComparer.OrdinalIgnoreCase);

    private int _stepIndex;
    private string _searchText = string.Empty;
    private MultiClassSearchEntry? _selectedEntry;
    private int _selectedLevels;
    private bool _enforceBracketGuard = true;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Func<Task>? CloseRequested;
    public event Func<string, Task>? OpenSpecialisationRequested;
    public event Func<string, Task<bool>>? NonStandardConfirmationRequested;

    public ObservableCollection<ClassCardVm> FilteredClasses { get; } = new();
    public ObservableCollection<MultiClassFilterChipVm> FilterChips { get; } = new();
    public ObservableCollection<MultiClassLevelRowVm> SelectedLevelRows { get; } = new();

    public ICommand SelectClassCommand { get; }
    public ICommand ToggleExpandedCommand { get; }
    public ICommand ToggleFilterChipCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand NextCommand { get; }

    public MultiClassWizardVm(
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
        ToggleFilterChipCommand = new Command<MultiClassFilterChipVm>(ToggleFilterChip);
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

    public bool EnforceBracketGuard
    {
        get => _enforceBracketGuard;
        set
        {
            if (!Set(ref _enforceBracketGuard, value))
                return;

            Refilter();
            Raise(nameof(CanGoNext));
        }
    }

    public bool IsSearchStep => _stepIndex == SearchStep;
    public bool IsDetailStep => _stepIndex == DetailStep;

    public string StepDescription => IsSearchStep
        ? "Search and pick a multi-class"
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
        var classesTask = ClassService.GetAllAsync();
        var racesTask = PeopleService.GetAllAsync();
        var catalogTask = MultiClassService.GetCatalogAsync();
        await Task.WhenAll(classesTask, racesTask, catalogTask);

        _classes = classesTask.Result;
        _races = racesTask.Result;
        BuildClassLookupIndex();

        _allEntries.Clear();
        _entriesByCardKey.Clear();

        var catalog = catalogTask.Result;
        foreach (var pair in catalog.MultiClasses.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            var entry = await BuildSearchEntryAsync(pair.Key, pair.Value);
            if (entry == null)
                continue;

            _allEntries.Add(entry);
            _entriesByCardKey[entry.Card.Key] = entry;
        }

        RefreshBracketAvailabilityFlags();
        BuildFilterChips();
        Refilter();

        var initial = FindEntryByStoredKey(_initialStorageKey);
        var firstSaved = (_draft.MultiClassLevels ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase))
            .Keys
            .Select(FindEntryByStoredKey)
            .FirstOrDefault(found => found != null);

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

    private async Task<MultiClassSearchEntry?> BuildSearchEntryAsync(string key, MultiClassDefinition definition)
    {
        var available = FindAvailableOption(definition.AvailabilityOptions);
        if (available == null)
            return null;

        var displayName = !string.IsNullOrWhiteSpace(definition.DisplayName)
            ? definition.DisplayName.Trim()
            : key;

        var maxLevel = ResolveMaxLevel(definition);
        var levelRows = new List<LevelAbilityRowVm>(maxLevel);
        var detailRows = new List<MultiClassLevelRowVm>(maxLevel);
        var lifeByLevel = new Dictionary<int, LifeScalePoint>();
        var costsByLevel = ResolveCostsByLevel(available, maxLevel);

        for (var level = 1; level <= maxLevel; level++)
        {
            var abilities = ResolveAbilitiesForLevel(definition, level);
            var life = await ResolveLifeForLevelAsync(definition, level);
            if (life.HasValue)
                lifeByLevel[level] = life.Value;

            var abilityNames = ResolveAbilityNames(abilities);
            var abilityKeys = ResolveAbilityLookupKeys(abilities);
            var detailAbilityEntries = BuildAbilityDetailEntries(abilities);

            levelRows.Add(BuildLevelAbilityRow(level, life, abilityNames, abilityKeys));

            detailRows.Add(new MultiClassLevelRowVm
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
        var targetBrackets = ResolveTargetBrackets(key, displayName, definition, parsedBracketTags.Count > 0 ? parsedBracketTags : bracketTags);

        var maxAc = ResolveMaxAc(definition);
        var firstLife = lifeByLevel.TryGetValue(1, out var lifeAtOne)
            ? lifeAtOne
            : default;

        var card = new ClassCardVm
        {
            Key = $"mc:{key}",
            Name = displayName,
            Category = category,
            Icon = icon,
            IconGlyph = (definition.IconGlyph ?? string.Empty).Trim(),
            IconBackground = IconPalette.PickRandomIconPastel(),
            Summary = BuildSummary(detailRows),
            MaxAc = maxAc,
            TBLP = firstLife.Body,
            PowerBase = ResolvePowerBase(definition),
            LevelRows = new ObservableCollection<LevelAbilityRowVm>(levelRows),
            BracketTags = parsedBracketTags.Count > 0 ? parsedBracketTags : bracketTags,
            ShowBodyAndLoc = lifeByLevel.Count > 0,
            RaceName = (_draft.Race ?? string.Empty).Trim(),
            CardBackgroundColor = StandardCardColor
        };

        return new MultiClassSearchEntry(
            key: key,
            card: card,
            definition: definition,
            maxLevel: maxLevel,
            availabilityDisplay: available.Display,
            costsByLevel: costsByLevel,
            detailRows: detailRows,
            targetBrackets: targetBrackets);
    }

    private static string ResolvePowerBase(MultiClassDefinition definition)
        => MultiPathWizardHelpers.ResolvePowerBase<
            MultiClassDefinition,
            MultiClassAvailabilityOption,
            MultiClassLevelAbility,
            MultiClassSystemEffect>(definition);

    private static int ResolveMaxLevel(MultiClassDefinition definition)
        => MultiPathWizardHelpers.ResolveMaxLevel<
            MultiClassDefinition,
            MultiClassAvailabilityOption,
            MultiClassLevelAbility,
            MultiClassSystemEffect>(definition);

    private MultiClassAvailabilityOption? FindAvailableOption(IEnumerable<MultiClassAvailabilityOption>? options)
        => MultiPathWizardHelpers.FindAvailableOption(
            options,
            rules => _abilityAvailabilityService.IsAvailable(rules, _draft, _classes, _races));

    private static Dictionary<int, int> ResolveCostsByLevel(MultiClassAvailabilityOption option, int maxLevel)
        => MultiPathWizardHelpers.ResolveCostsByLevel(option, maxLevel);

    private static IReadOnlyList<MultiClassLevelAbility> ResolveAbilitiesForLevel(MultiClassDefinition definition, int level)
        => MultiPathWizardHelpers.ResolveAbilitiesForLevel<
            MultiClassDefinition,
            MultiClassAvailabilityOption,
            MultiClassLevelAbility,
            MultiClassSystemEffect>(definition, level);

    private static string BuildSummary(IReadOnlyList<MultiClassLevelRowVm> detailRows)
        => MultiPathWizardHelpers.BuildSummary(detailRows, row => row.AbilityEffectsText);

    private static string BuildAbilityEffectsText(IReadOnlyList<MultiClassLevelAbility> abilities)
        => MultiPathWizardHelpers.BuildAbilityEffectsText(abilities);

    private static IReadOnlyList<string> ResolveAbilityNames(IEnumerable<MultiClassLevelAbility> abilities)
        => abilities
            .Select(ability => (ability.Name ?? string.Empty).Trim())
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static IReadOnlyList<string> ResolveAbilityLookupKeys(IEnumerable<MultiClassLevelAbility> abilities)
        => abilities
            .Select(ResolveAbilityLookupKey)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static IReadOnlyList<MultiClassAbilityDetailVm> BuildAbilityDetailEntries(
        IEnumerable<MultiClassLevelAbility> abilities)
        => abilities
            .Where(ability => !string.IsNullOrWhiteSpace((ability.Name ?? string.Empty).Trim())
                             || !string.IsNullOrWhiteSpace((ability.Effect ?? string.Empty).Trim()))
            .Select(ability =>
            {
                var lookupKey = ResolveAbilityLookupKey(ability);
                var displayName = (ability.Name ?? string.Empty).Trim();
                if (displayName.Length == 0)
                    displayName = lookupKey;

                return new MultiClassAbilityDetailVm(
                    DisplayName: displayName,
                    LookupKey: lookupKey);
            })
            .Where(entry => entry.IsValid)
            .ToList();

    private static string ResolveAbilityLookupKey(MultiClassLevelAbility ability)
        => !string.IsNullOrWhiteSpace(ability.AbilityRef)
            ? ability.AbilityRef.Trim()
            : (ability.Name ?? string.Empty).Trim();

    private static LevelAbilityRowVm BuildLevelAbilityRow(
        int level,
        LifeScalePoint? life,
        IReadOnlyList<string> abilityNames,
        IReadOnlyList<string> abilityKeys)
    {
        var body = life.HasValue ? life.Value.Body.ToString() : string.Empty;
        var loc = life.HasValue ? life.Value.Loc.ToString() : string.Empty;
        return new LevelAbilityRowVm
        {
            Level = level,
            Body = body,
            Loc = loc,
            AbilityNames = abilityNames,
            AbilityDetailKeys = abilityKeys
        };
    }

    private int ResolveMaxAc(MultiClassDefinition definition)
        => MultiPathWizardHelpers.ResolveMaxAc<
            MultiClassDefinition,
            MultiClassAvailabilityOption,
            MultiClassLevelAbility,
            MultiClassSystemEffect,
            MultiClassLifeScaleReference>(
            definition,
            MatchesRules);

    private async Task<LifeScalePoint?> ResolveLifeForLevelAsync(MultiClassDefinition definition, int level)
        => await MultiPathWizardHelpers.ResolveLifeForLevelAsync<
            MultiClassDefinition,
            MultiClassAvailabilityOption,
            MultiClassLevelAbility,
            MultiClassSystemEffect,
            MultiClassLifeScaleReference>(
            definition,
            level,
            MatchesRules,
            ResolveLifeScaleReferenceAsync);

    private async Task<LifeScalePoint?> ResolveLifeScaleReferenceAsync(
        MultiClassLifeScaleReference? reference,
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
        MultiClassDefinition definition,
        MultiClassAvailabilityOption availableOption)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddTrimmedBracketLabels(set, GetDefinitionBracketCandidates(definition));
        AddTrimmedBracketLabels(set, GetSourceClassBrackets(definition, availableOption));
        AddTrimmedBracketLabels(set, GetBracketRuleValues(availableOption.Rules));

        if (set.Count == 0)
            set.Add("Any");

        return set.ToList();
    }

    private IReadOnlyList<string> ResolveTargetBrackets(
        string key,
        string displayName,
        MultiClassDefinition definition,
        IReadOnlyList<string> fallbackBracketTags)
        => ResolveFirstNonEmptyCanonicalBracketSet(
            GetClassRecordBrackets(key, displayName),
            GetDefinitionBracketCandidates(definition),
            GetDefinitionAvailabilityBrackets(definition),
            fallbackBracketTags);

    private static IEnumerable<string> GetDefinitionBracketCandidates(MultiClassDefinition definition)
    {
        var pureBracket = (definition.RequiresBracketPure ?? string.Empty).Trim();
        if (pureBracket.Length > 0)
            yield return pureBracket;
    }

    private IEnumerable<string> GetSourceClassBrackets(
        MultiClassDefinition definition,
        MultiClassAvailabilityOption availableOption)
    {
        foreach (var sourceClass in ResolveAvailabilitySourceClasses(definition, availableOption))
        {
            foreach (var bracket in GetClassRecordBrackets(sourceClass))
                yield return bracket;
        }
    }

    private IEnumerable<string> GetClassRecordBrackets(string className)
    {
        var classRecord = ResolveClassRecord(className);
        if (classRecord?.Brackets == null)
            yield break;

        foreach (var bracket in classRecord.Brackets)
            yield return bracket;
    }

    private IEnumerable<string> GetClassRecordBrackets(string key, string displayName)
    {
        var classRecord = ResolveClassRecord(key) ?? ResolveClassRecord(displayName);
        if (classRecord?.Brackets == null)
            yield break;

        foreach (var bracket in classRecord.Brackets)
            yield return bracket;
    }

    private static IEnumerable<string> GetDefinitionAvailabilityBrackets(MultiClassDefinition definition)
        => (definition.AvailabilityOptions ?? Enumerable.Empty<MultiClassAvailabilityOption>())
            .SelectMany(option => GetBracketRuleValues(option.Rules));

    private static IEnumerable<string> GetBracketRuleValues(IEnumerable<RuleClause>? rules)
        => GetInRuleValues(
            rules,
            field => field.EndsWith("bracket", StringComparison.OrdinalIgnoreCase)
                     || field.EndsWith("brackets", StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> GetInRuleValues(
        IEnumerable<RuleClause>? rules,
        Func<string, bool> fieldPredicate)
    {
        foreach (var rule in rules ?? Enumerable.Empty<RuleClause>())
        {
            if (rule == null || (rule.Operator != RuleComparisonOp.In && rule.Operator != RuleComparisonOp.Only))
                continue;

            var field = NormalizeToken(rule.Field);
            if (!fieldPredicate(field))
                continue;

            foreach (var value in rule.Value ?? Enumerable.Empty<string>())
            {
                var trimmed = (value ?? string.Empty).Trim();
                if (trimmed.Length > 0)
                    yield return trimmed;
            }
        }
    }

    private static IReadOnlyList<string> ResolveFirstNonEmptyCanonicalBracketSet(
        params IEnumerable<string>[] candidates)
    {
        foreach (var candidate in candidates)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddCanonicalBracketLabels(set, candidate);
            if (set.Count > 0)
                return set.ToList();
        }

        return Array.Empty<string>();
    }

    private void RefreshBracketAvailabilityFlags()
    {
        foreach (var entry in _allEntries)
        {
            var currentBrackets = ResolveCurrentBracketLabels(excludedStorageKey: entry.StorageKey);
            var requiresThirdBracket = WouldRequireThirdBracket(currentBrackets, entry.TargetBrackets);
            var violatesRequiredBracketPurity = ViolatesRequiredBracketPurity(currentBrackets, entry.Definition.RequiresBracketPure);
            var hasPurchasedBracketPureClass = HasPurchasedBracketPureClass(excludedStorageKey: entry.StorageKey);
            var violatesExistingBracketPurity = hasPurchasedBracketPureClass
                                                && WouldIntroduceNewBracket(currentBrackets, entry.TargetBrackets);

            entry.RequiresThirdBracketApproval = requiresThirdBracket;
            entry.RequiresBracketPurityApproval = violatesRequiredBracketPurity || violatesExistingBracketPurity;
            entry.NonStandardWarningMessage = BuildNonStandardWarningMessage(
                requiresThirdBracket,
                violatesRequiredBracketPurity,
                violatesExistingBracketPurity);

            entry.Card.CardBackgroundColor = entry.RequiresNonStandardApproval
                ? ThirdBracketCardColor
                : StandardCardColor;
        }
    }

    private IReadOnlySet<string> ResolveCurrentBracketLabels(string? excludedStorageKey = null)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var baseClassName = (_draft.Class ?? string.Empty).Trim();
        if (baseClassName.Length > 0)
            AddCanonicalBracketLabels(set, GetClassRecordBrackets(baseClassName));

        foreach (var pair in _draft.MultiClassLevels ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase))
        {
            if (pair.Value <= 0)
                continue;

            var storedKey = (pair.Key ?? string.Empty).Trim();
            if (storedKey.Length == 0)
                continue;

            if (!string.IsNullOrWhiteSpace(excludedStorageKey)
                && storedKey.Equals(excludedStorageKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var entry = FindEntryByStoredKey(storedKey);
            if (entry != null)
            {
                AddCanonicalBracketLabels(set, entry.TargetBrackets);
                continue;
            }

            AddCanonicalBracketLabels(set, GetClassRecordBrackets(storedKey));
        }

        return set;
    }

    private static bool WouldRequireThirdBracket(
        IReadOnlyCollection<string> currentBrackets,
        IReadOnlyCollection<string> targetBrackets)
    {
        var current = new HashSet<string>(
            currentBrackets.Where(KnownBracketLabels.Contains),
            StringComparer.OrdinalIgnoreCase);

        var target = new HashSet<string>(
            targetBrackets.Where(KnownBracketLabels.Contains),
            StringComparer.OrdinalIgnoreCase);

        if (target.Count == 0)
            return false;

        var hasNewBracket = target.Any(bracket => !current.Contains(bracket));
        if (!hasNewBracket)
            return false;

        var unionCount = current.Union(target, StringComparer.OrdinalIgnoreCase).Count();
        return unionCount > 2;
    }

    private static bool WouldIntroduceNewBracket(
        IReadOnlyCollection<string> currentBrackets,
        IReadOnlyCollection<string> targetBrackets)
    {
        var current = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddCanonicalBracketLabels(current, currentBrackets);

        var target = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddCanonicalBracketLabels(target, targetBrackets);

        if (target.Count == 0)
            return false;

        return target.Any(bracket => !current.Contains(bracket));
    }

    private bool HasPurchasedBracketPureClass(string? excludedStorageKey = null)
    {
        foreach (var pair in _draft.MultiClassLevels ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase))
        {
            if (pair.Value <= 0)
                continue;

            var storageKey = (pair.Key ?? string.Empty).Trim();
            if (storageKey.Length == 0)
                continue;

            if (!string.IsNullOrWhiteSpace(excludedStorageKey)
                && storageKey.Equals(excludedStorageKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var entry = FindEntryByStoredKey(storageKey);
            if (entry == null)
                continue;

            if (!string.IsNullOrWhiteSpace((entry.Definition.RequiresBracketPure ?? string.Empty).Trim()))
                return true;
        }

        return false;
    }

    private static bool ViolatesRequiredBracketPurity(
        IReadOnlyCollection<string> currentBrackets,
        string? requiredBracketPure)
    {
        var required = ParseBracketPuritySet(requiredBracketPure);
        if (required.Count == 0)
            return false;

        var current = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddCanonicalBracketLabels(current, currentBrackets);

        if (current.Count == 0)
            return false;

        return current.Any(bracket => !required.Contains(bracket));
    }

    private static IReadOnlySet<string> ParseBracketPuritySet(string? requiredBracketPure)
    {
        var raw = (requiredBracketPure ?? string.Empty).Trim();
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (raw.Length == 0)
            return set;

        var normalized = raw
            .Replace(" and ", ",", StringComparison.OrdinalIgnoreCase)
            .Replace("&", ",", StringComparison.OrdinalIgnoreCase);

        var segments = normalized.Split(new[] { ',', '/', '|', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
            segments = new[] { normalized };

        foreach (var segment in segments)
            AddCanonicalBracketLabel(set, segment);

        return set;
    }

    private static string BuildNonStandardWarningMessage(
        bool requiresThirdBracket,
        bool violatesRequiredBracketPurity,
        bool violatesExistingBracketPurity)
    {
        if (requiresThirdBracket)
            return ThirdBracketWarningMessage;

        if (violatesRequiredBracketPurity)
            return RequiredBracketPurityWarningMessage;

        if (violatesExistingBracketPurity)
            return ExistingBracketPurityWarningMessage;

        return ThirdBracketWarningMessage;
    }

    private static IEnumerable<string> ResolveAvailabilitySourceClasses(
        MultiClassDefinition definition,
        MultiClassAvailabilityOption availableOption)
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

    private static IEnumerable<string> ResolveClassesFromOption(MultiClassAvailabilityOption? option)
    {
        if (option == null)
            yield break;

        if (!string.IsNullOrWhiteSpace(option.Source))
            yield return option.Source;

        foreach (var value in GetInRuleValues(
                     option.Rules,
                     field => field.Equals("class", StringComparison.OrdinalIgnoreCase)
                              || field.Equals("classes", StringComparison.OrdinalIgnoreCase)))
        {
            yield return value;
        }
    }

    private void BuildClassLookupIndex()
    {
        var normalizedLookup = new Dictionary<string, CharacterClassRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in _classes)
        {
            var normalizedKey = NormalizeToken(pair.Key);
            if (normalizedKey.Length == 0)
                continue;

            normalizedLookup[normalizedKey] = pair.Value;
        }

        _classesByNormalizedKey = normalizedLookup;
        _classSearchIndex = normalizedLookup.ToList();
    }

    private CharacterClassRecord? ResolveClassRecord(string sourceClass)
    {
        var trimmed = (sourceClass ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return null;

        if (_classes.TryGetValue(trimmed, out var direct))
            return direct;

        var normalized = NormalizeToken(trimmed);
        if (normalized.Length == 0)
            return null;

        if (_classesByNormalizedKey.TryGetValue(normalized, out var exactNormalized))
            return exactNormalized;

        foreach (var pair in _classSearchIndex)
        {
            if (pair.Key.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || normalized.Contains(pair.Key, StringComparison.OrdinalIgnoreCase))
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

    private static void AddTrimmedBracketLabels(ISet<string> set, IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            var label = ExtractBracketLabel(value).Trim();
            if (label.Length > 0)
                set.Add(label);
        }
    }

    private static void AddCanonicalBracketLabels(ISet<string> set, IEnumerable<string> values)
    {
        foreach (var value in values)
            AddCanonicalBracketLabel(set, value);
    }

    private static void AddCanonicalBracketLabel(ISet<string> set, string? raw)
    {
        var label = ExtractBracketLabel(raw).Trim();
        if (label.Length == 0)
            return;

        if (label.Equals("warrior", StringComparison.OrdinalIgnoreCase))
            set.Add("Warrior");
        else if (label.Equals("scout", StringComparison.OrdinalIgnoreCase))
            set.Add("Scout");
        else if (label.Equals("priest", StringComparison.OrdinalIgnoreCase))
            set.Add("Priest");
        else if (label.Equals("wizard", StringComparison.OrdinalIgnoreCase))
            set.Add("Wizard");
        else if (label.Equals("druid", StringComparison.OrdinalIgnoreCase))
            set.Add("Druid");
        else if (label.Equals("neuro", StringComparison.OrdinalIgnoreCase))
            set.Add("Neuro");
        else
            set.Add(label);
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
        FilterChips.Add(new MultiClassFilterChipVm(FilterKeyAll, "All", false));
        FilterChips.Add(new MultiClassFilterChipVm(FilterKeyAvailable, "Available", true));

        var labels = _allEntries
            .SelectMany(entry => entry.Card.BracketLabels)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(label => label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var label in labels)
            FilterChips.Add(new MultiClassFilterChipVm(label, label, false));
    }

    private void ToggleFilterChip(MultiClassFilterChipVm? chip)
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
        var filters = BuildSearchFilterState();
        var selectedKey = _selectedEntry?.Card.Key ?? string.Empty;

        var filtered = _allEntries.Where(entry => IsVisibleForFilters(entry, filters));

        FilteredClasses.Clear();
        foreach (var entry in filtered.OrderBy(entry => entry.Card.Name, StringComparer.OrdinalIgnoreCase))
            FilteredClasses.Add(entry.Card);

        Raise(nameof(HasAvailableClasses));

        if (selectedKey.Length > 0 && _entriesByCardKey.TryGetValue(selectedKey, out var selected))
            SetSelectedEntry(selected, preserveStep: true);
    }

    private SearchFilterState BuildSearchFilterState()
    {
        var query = (_searchText ?? string.Empty).Trim();
        var selectedFilterKeys = FilterChips
            .Where(option => option.IsSelected && !option.IsAll)
            .Select(option => option.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var allSelected = selectedFilterKeys.Count == 0
                          || FilterChips.FirstOrDefault(option => option.IsAll)?.IsSelected == true;
        var availableSelected = !allSelected
                                && selectedFilterKeys.Contains(FilterKeyAvailable);
        var selectedBracketFilters = selectedFilterKeys
            .Where(filter => !filter.Equals(FilterKeyAvailable, StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new SearchFilterState(
            query,
            allSelected,
            availableSelected,
            selectedBracketFilters);
    }

    private bool IsVisibleForFilters(MultiClassSearchEntry entry, SearchFilterState filters)
    {
        var matchesQuery = filters.Query.Length == 0
                           || entry.Card.Name.Contains(filters.Query, StringComparison.OrdinalIgnoreCase);
        if (!matchesQuery)
            return false;

        if (filters.AvailableOnly && entry.RequiresNonStandardApproval)
            return false;

        if (EnforceBracketGuard && entry.RequiresNonStandardApproval)
            return false;

        if (filters.AllSelected || filters.SelectedBracketFilters.Count == 0)
            return true;

        return entry.Card.BracketLabels.Any(label => filters.SelectedBracketFilters.Contains(label));
    }

    private void OnSelectClass(ClassCardVm? card)
    {
        if (card == null)
            return;

        if (!_entriesByCardKey.TryGetValue(card.Key, out var entry))
            return;

        SetSelectedEntry(entry);
    }

    private void SetSelectedEntry(MultiClassSearchEntry entry, bool preserveStep = false)
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

            if (_selectedEntry.RequiresNonStandardApproval)
            {
                var canProceed = true;
                var warningMessage = string.IsNullOrWhiteSpace(_selectedEntry.NonStandardWarningMessage)
                    ? ThirdBracketWarningMessage
                    : _selectedEntry.NonStandardWarningMessage;
                if (NonStandardConfirmationRequested != null)
                    canProceed = await NonStandardConfirmationRequested.Invoke(warningMessage);

                if (!canProceed)
                    return;
            }

            LoadSelectedEntryRows(_selectedEntry);
            _stepIndex = DetailStep;
            RaiseStepState();
            return;
        }

        if (_selectedEntry == null)
            return;

        _draft.MultiClassLevels ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        _draft.MultiClassChoiceSelections ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (_selectedLevels <= 0)
        {
            _draft.MultiClassLevels.Remove(_selectedEntry.StorageKey);
            RemoveChoiceSelectionsForClass(_selectedEntry.StorageKey);
        }
        else
        {
            _draft.MultiClassLevels[_selectedEntry.StorageKey] = _selectedLevels;
            if (HasChoiceSetRefsAtOrBelowLevel(_selectedEntry.Definition, _selectedLevels))
            {
                if (OpenSpecialisationRequested != null)
                    await OpenSpecialisationRequested.Invoke(_selectedEntry.StorageKey);

                return;
            }
        }

        if (CloseRequested != null)
            await CloseRequested.Invoke();
    }

    private void LoadSelectedEntryRows(MultiClassSearchEntry entry)
    {
        SelectedLevelRows.Clear();
        foreach (var row in entry.DetailRows)
            SelectedLevelRows.Add(row.Clone());

        var saved = 0;
        if (_draft.MultiClassLevels != null
            && _draft.MultiClassLevels.TryGetValue(entry.StorageKey, out var storedLevel))
        {
            saved = storedLevel;
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

    public MultiClassSearchEntry? GetSelectedEntry()
        => _selectedEntry;

    public MultiClassSearchEntry? FindEntryByCard(ClassCardVm? card)
    {
        if (card == null)
            return null;

        return _entriesByCardKey.TryGetValue(card.Key, out var found)
            ? found
            : null;
    }

    public MultiClassSearchEntry? FindEntryByStoredKey(string? storedKey)
    {
        var key = (storedKey ?? string.Empty).Trim();
        if (key.Length == 0)
            return null;

        return _allEntries.FirstOrDefault(entry =>
            entry.StorageKey.Equals(key, StringComparison.OrdinalIgnoreCase)
            || entry.Card.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<MultiClassAbilityDetailVm> GetAbilityDetailsForRow(MultiClassLevelRowVm? row)
        => row?.AbilityDetails ?? Array.Empty<MultiClassAbilityDetailVm>();

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

    private void RemoveChoiceSelectionsForClass(string classKey)
    {
        var prefix = $"{classKey}::";
        var staleKeys = _draft.MultiClassChoiceSelections
            .Keys
            .Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var staleKey in staleKeys)
            _draft.MultiClassChoiceSelections.Remove(staleKey);
    }

    private static bool HasChoiceSetRefsAtOrBelowLevel(MultiClassDefinition definition, int selectedLevel)
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

    private readonly record struct SearchFilterState(
        string Query,
        bool AllSelected,
        bool AvailableOnly,
        IReadOnlySet<string> SelectedBracketFilters);

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

    public sealed class MultiClassSearchEntry
    {
        public MultiClassSearchEntry(
            string key,
            ClassCardVm card,
            MultiClassDefinition definition,
            int maxLevel,
            string availabilityDisplay,
            IReadOnlyDictionary<int, int> costsByLevel,
            IReadOnlyList<MultiClassLevelRowVm> detailRows,
            IReadOnlyList<string> targetBrackets)
        {
            Key = key;
            StorageKey = key;
            Card = card;
            Definition = definition;
            MaxLevel = maxLevel;
            AvailabilityDisplay = availabilityDisplay;
            CostsByLevel = costsByLevel;
            DetailRows = detailRows;
            TargetBrackets = targetBrackets;
        }

        public string Key { get; }
        public string StorageKey { get; }
        public ClassCardVm Card { get; }
        public MultiClassDefinition Definition { get; }
        public int MaxLevel { get; }
        public string AvailabilityDisplay { get; }
        public IReadOnlyDictionary<int, int> CostsByLevel { get; }
        public IReadOnlyList<MultiClassLevelRowVm> DetailRows { get; }
        public IReadOnlyList<string> TargetBrackets { get; }
        public bool RequiresThirdBracketApproval { get; set; }
        public bool RequiresBracketPurityApproval { get; set; }
        public string NonStandardWarningMessage { get; set; } = string.Empty;
        public bool RequiresNonStandardApproval => RequiresThirdBracketApproval || RequiresBracketPurityApproval;
    }
}

public sealed class MultiClassFilterChipVm : INotifyPropertyChanged
{
    private bool _isSelected;

    public event PropertyChangedEventHandler? PropertyChanged;

    public MultiClassFilterChipVm(string key, string label, bool isSelected)
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

public sealed class MultiClassLevelRowVm : INotifyPropertyChanged
{
    private Color _rowBackgroundColor = Colors.Transparent;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Level { get; set; }
    public string BodyText { get; set; } = string.Empty;
    public string LocText { get; set; } = string.Empty;
    public int Cost { get; set; }
    public string AbilityEffectsText { get; set; } = string.Empty;
    public IReadOnlyList<MultiClassAbilityDetailVm> AbilityDetails { get; set; } = Array.Empty<MultiClassAbilityDetailVm>();

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

    public MultiClassLevelRowVm Clone()
    {
        return new MultiClassLevelRowVm
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

public sealed record MultiClassAbilityDetailVm(string DisplayName, string LookupKey)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(DisplayName) || !string.IsNullOrWhiteSpace(LookupKey);
}
