using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using labyItems.Models.Characters;
using labyItems.Models.Enums;
using labyItems.Services;
using labyItems.Services.Specialisations;

namespace labyItems.Pages.NonStandard;

public sealed class NonStandardClassCreateVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private static readonly JsonSerializerOptions PrettyJson = new()
    {
        WriteIndented = true
    };

    private static readonly IReadOnlyDictionary<string, string> PowerbaseDisplayToRaw = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["🌍 Earthpower"] = "Earthpower",
        ["🪄 Magic"] = "Magic",
        ["✨ Spirit"] = "Spirit",
        ["🧠 Neuronic"] = "Neuro"
    };

    private static readonly IReadOnlyDictionary<string, string> ArmourDisplayToRaw = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["🪽 Light"] = "light",
        ["💪 Medium"] = "medium",
        ["🏋️‍♀️ Heavy"] = "heavy"
    };

    private static readonly IReadOnlyDictionary<string, string> WeaponSkillDisplayToCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["O (one handed)"] = "O",
        ["B (two handed)"] = "B",
        ["E (either hand)"] = "E",
        ["UAC (unarmed combat)"] = "UAC",
        ["H (thrown)"] = "H",
        ["MP (machine propelled)"] = "MP",
        ["U (shield)"] = "U"
    };

    private readonly Dictionary<string, CharacterClassRecord> _allClasses = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AbilityDefinition> _abilityLookup = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _allRaceNames = new();
    private readonly List<string> _allBuyAsValues = new();
    private readonly List<NonStandardLifeScaleOption> _lifeScaleOptions = new();
    private readonly Dictionary<string, string> _raceAssignmentOptions = new(StringComparer.OrdinalIgnoreCase);

    private bool _initialized;
    private bool _isBusy;
    private string _saveStatus = string.Empty;

    private string _baseClassName = string.Empty;
    private CharacterClassRecord? _baseClassRecord;

    private string _pathClassName = string.Empty;
    private string _name = string.Empty;
    private bool _isRebirth;
    private string _maxAcText = string.Empty;
    private string _buyAsText = string.Empty;
    private string _armourRestrictionInput = string.Empty;
    private string _weaponSkillRestrictionInput = string.Empty;
    private string _lifeScaleAssignmentRaceName = string.Empty;
    private bool _isLifescaleExpanded;
    private bool _useCustomLifeScale;
    private string _lifeScaleClassFilterText = string.Empty;
    private string _lifeScaleRaceFilterText = string.Empty;
    private bool _isLifeScaleAdvancedExpanded;
    private bool _isLifeScaleSearchExpanded = true;
    private bool _isPostEighthExpanded;

    private NonStandardLifeScaleOption? _selectedLifeScaleOption;

    public ObservableCollection<string> BracketOptions { get; } = new();
    public ObservableCollection<string> SelectedBrackets { get; } = new();

    public ObservableCollection<string> ArmourOptions { get; } =
    [
        "🪽 Light",
        "💪 Medium",
        "🏋️‍♀️ Heavy"
    ];

    public ObservableCollection<string> SelectedArmourOptions { get; } = new();
    public ObservableCollection<string> ArmourRestrictions { get; } = new();
    public ObservableCollection<TextRowVm> ArmourRestrictionRows { get; } = new();

    public ObservableCollection<string> BuyAsOptions { get; } = new();
    public ObservableCollection<string> SelectedBuyAsOptions { get; } = new();

    public ObservableCollection<string> WeaponSkillOptions { get; } =
    [
        "O (one handed)",
        "B (two handed)",
        "E (either hand)",
        "UAC (unarmed combat)",
        "H (thrown)",
        "MP (machine propelled)",
        "U (shield)"
    ];

    public ObservableCollection<WeaponSkillLevelVm> WeaponSkillLevels { get; } = new();
    public ObservableCollection<string> WeaponSkillRestrictions { get; } = new();

    public ObservableCollection<AbilityLevelVm> AbilityLevels { get; } = new();
    public ObservableCollection<int> LevelOptions { get; } = [1, 2, 3, 4, 5, 6, 7, 8];

    public ObservableCollection<string> PowerbaseOptions { get; } =
    [
        "🌍 Earthpower",
        "🪄 Magic",
        "✨ Spirit",
        "🧠 Neuronic"
    ];

    public ObservableCollection<string> SelectedPowerbases { get; } = new();
    public ObservableCollection<PowerCalculationRowVm> PowerCalculations { get; } = new();

    public ObservableCollection<CasterLevelRowVm> CasterLevelRows { get; } = new();
    public ObservableCollection<string> CasterColourOptions { get; } = new();

    public ObservableCollection<Alignment> SelectedAlignments { get; } = new();

    public ObservableCollection<LifeScalePointVm> ExpandedLifeScaleRows { get; } = new();
    public ObservableCollection<CustomLifeScalePointVm> CustomLifeScaleRows { get; } = new();
    public ObservableCollection<LifeScaleSearchOptionVm> FilteredLifeScaleOptions { get; } = new();
    public ObservableCollection<LifeScaleAssignmentVm> LifeScaleAssignments { get; } = new();
    public ObservableCollection<string> RaceWhitelist { get; } = new();
    public ObservableCollection<string> RaceBlacklist { get; } = new();
    public ObservableCollection<PostEighthEntryVm> PostEighthEntries { get; } = new();
    public ObservableCollection<string> PostEighthMultiRaces { get; } = new();
    public ObservableCollection<string> PostEighthMultiClasses { get; } = new();
    public ObservableCollection<string> PostEighthRestrictions { get; } = new();

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!Set(ref _isBusy, value))
                return;

            Raise(nameof(CanSave));
        }
    }

    public string SaveStatus
    {
        get => _saveStatus;
        private set
        {
            if (!Set(ref _saveStatus, value ?? string.Empty))
                return;

            Raise(nameof(HasSaveStatus));
        }
    }

    public bool HasSaveStatus => !string.IsNullOrWhiteSpace((SaveStatus ?? string.Empty).Trim());

    public string BaseClassName
    {
        get => _baseClassName;
        private set
        {
            if (!Set(ref _baseClassName, value ?? string.Empty))
                return;

            Raise(nameof(HasBaseSelection));
        }
    }

    public bool HasBaseSelection => !string.IsNullOrWhiteSpace((BaseClassName ?? string.Empty).Trim());

    public string PathClassName
    {
        get => _pathClassName;
        private set => Set(ref _pathClassName, value ?? string.Empty);
    }

    public string Name
    {
        get => _name;
        set
        {
            if (!Set(ref _name, value ?? string.Empty))
                return;

            RefreshLifeScaleAssignmentClassName();
            Raise(nameof(PowerbaseExplanation));
            Raise(nameof(CanSave));
        }
    }

    public bool IsRebirth
    {
        get => _isRebirth;
        set
        {
            if (!Set(ref _isRebirth, value))
                return;

            Raise(nameof(TagsPreview));
        }
    }

    public string TagsPreview
    {
        get
        {
            var tags = new List<string> { "Non-standard" };
            if (IsRebirth)
                tags.Add("Rebirth");

            if (!string.IsNullOrWhiteSpace(BaseClassName))
                tags.Add($"Base:{BaseClassName}");

            return string.Join(" | ", tags);
        }
    }

    public string MaxAcText
    {
        get => _maxAcText;
        set
        {
            if (!Set(ref _maxAcText, value ?? string.Empty))
                return;

            Raise(nameof(ShowMaxAcWarning));
            Raise(nameof(CanSave));
        }
    }

    public bool ShowMaxAcWarning => ParseInt(MaxAcText) > 12;

    public string BuyAsText
    {
        get => _buyAsText;
        set => Set(ref _buyAsText, value ?? string.Empty);
    }

    public string ArmourRestrictionInput
    {
        get => _armourRestrictionInput;
        set => Set(ref _armourRestrictionInput, value ?? string.Empty);
    }

    public string WeaponSkillRestrictionInput
    {
        get => _weaponSkillRestrictionInput;
        set => Set(ref _weaponSkillRestrictionInput, value ?? string.Empty);
    }

    public bool HasBracketWarning => SelectedBrackets.Count > 2;
    public string BracketWarningText => "unable to be in more than 2 brackets";
    public bool BracketSelectionHasIssue => HasBracketWarning;

    public bool HasMagicPowerbase => SelectedPowerbases.Any(IsMagicPowerbaseDisplay);

    public string PowerbaseExplanation
    {
        get
        {
            var className = string.IsNullOrWhiteSpace(Name) ? "This class" : Name.Trim();
            var selected = SelectedPowerbases
                .Select(MapPowerbaseDisplayToRaw)
                .Where(x => x.Length > 0)
                .ToList();

            if (selected.Count == 0)
                return $"{className} has no powerbase selected yet.";

            return $"{className} has {string.Join(", ", selected)}. Example calc: CL^2+CL (ClassLevel squared + ClassLevel).";
        }
    }

    public string LifescaleSummary => BuildCurrentLifeScaleSummary();

    public bool HasLifeScaleSelection => LifescaleSummary.Length > 0;

    public string LifeScaleAssignmentRaceName
    {
        get => _lifeScaleAssignmentRaceName;
        private set
        {
            if (!Set(ref _lifeScaleAssignmentRaceName, value ?? string.Empty))
                return;

            Raise(nameof(CanAddLifeScaleAssignment));
        }
    }

    public bool CanAddLifeScaleAssignment
        => HasLifeScaleSelection
        && !string.IsNullOrWhiteSpace((LifeScaleAssignmentRaceName ?? string.Empty).Trim())
        && GetEffectiveLifeScalePoints().Count >= 8;

    public Dictionary<string, string> RaceAssignmentOptions => _raceAssignmentOptions;

    public bool IsLifescaleExpanded
    {
        get => _isLifescaleExpanded;
        set => Set(ref _isLifescaleExpanded, value);
    }

    public string LifescaleChevronText => IsLifescaleExpanded ? "▴" : "▾";

    public bool UseCustomLifeScale
    {
        get => _useCustomLifeScale;
        private set
        {
            if (!Set(ref _useCustomLifeScale, value))
                return;

            RebuildExpandedLifeScaleRows(GetEffectiveLifeScalePoints());
            Raise(nameof(LifeScaleSearchOpacity));
            Raise(nameof(LifescaleSummary));
            Raise(nameof(HasLifeScaleSelection));
            Raise(nameof(CanAddLifeScaleAssignment));
            Raise(nameof(CanSave));
        }
    }

    public string LifeScaleClassFilterText
    {
        get => _lifeScaleClassFilterText;
        set
        {
            if (!Set(ref _lifeScaleClassFilterText, value ?? string.Empty))
                return;

            ApplyLifeScaleSearchFilters();
        }
    }

    public string LifeScaleRaceFilterText
    {
        get => _lifeScaleRaceFilterText;
        set
        {
            if (!Set(ref _lifeScaleRaceFilterText, value ?? string.Empty))
                return;

            ApplyLifeScaleSearchFilters();
        }
    }

    public double LifeScaleSearchOpacity => UseCustomLifeScale || IsLifeScaleAdvancedExpanded ? 0.45 : 1.0;

    public bool IsLifeScaleSearchExpanded
    {
        get => _isLifeScaleSearchExpanded;
        set
        {
            if (!Set(ref _isLifeScaleSearchExpanded, value))
                return;

            Raise(nameof(LifeScaleSearchChevronText));
        }
    }

    public string LifeScaleSearchChevronText => IsLifeScaleSearchExpanded ? "▴" : "▾";

    public bool IsLifeScaleAdvancedExpanded
    {
        get => _isLifeScaleAdvancedExpanded;
        set
        {
            if (!Set(ref _isLifeScaleAdvancedExpanded, value))
                return;

            Raise(nameof(LifeScaleAdvancedChevronText));
            Raise(nameof(LifeScaleSearchOpacity));

            if (_isLifeScaleAdvancedExpanded)
                IsLifeScaleSearchExpanded = false;
        }
    }

    public string LifeScaleAdvancedChevronText => IsLifeScaleAdvancedExpanded ? "▴" : "▾";

    public bool IsPostEighthExpanded
    {
        get => _isPostEighthExpanded;
        set
        {
            if (!Set(ref _isPostEighthExpanded, value))
                return;

            Raise(nameof(PostEighthChevronText));
        }
    }

    public string PostEighthChevronText => IsPostEighthExpanded ? "▴" : "▾";

    public string PostEighthSummary
    {
        get
        {
            if (PostEighthEntries.Count == 0)
                return "No post-8 progression configured.";

            return string.Join(" | ", PostEighthEntries.Select(entry => entry.ReviewSummary));
        }
    }

    public bool CanSave
    {
        get
        {
            if (IsBusy)
                return false;

            if (string.IsNullOrWhiteSpace((Name ?? string.Empty).Trim()))
                return false;

            if (SelectedBrackets.Count == 0)
                return false;

            if (LifeScaleAssignments.Count == 0)
                return false;

            return true;
        }
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        _initialized = true;

        SelectedBrackets.CollectionChanged += OnSelectedBracketsChanged;
        SelectedPowerbases.CollectionChanged += OnSelectedPowerbasesChanged;
        ArmourRestrictions.CollectionChanged += OnArmourRestrictionsChanged;
        PostEighthEntries.CollectionChanged += OnPostEighthEntriesChanged;

        _allClasses.Clear();
        var classes = await ClassService.GetAllAsync();
        foreach (var entry in classes)
            _allClasses[entry.Key] = entry.Value;

        _allRaceNames.Clear();
        var races = await PeopleService.GetAllAsync();
        _allRaceNames.AddRange(races.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        _raceAssignmentOptions.Clear();
        foreach (var race in _allRaceNames)
            _raceAssignmentOptions[race] = race;
        Raise(nameof(RaceAssignmentOptions));

        _allBuyAsValues.Clear();
        _allBuyAsValues.AddRange(
            classes.Values
                .SelectMany(entry => entry.BuyAs ?? new List<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase));

        BuyAsOptions.Clear();
        foreach (var value in _allBuyAsValues)
            BuyAsOptions.Add(value);

        BuildBracketOptions();
        InitializeAlignmentSelection();
        BuildCasterColourOptions();
        BuildLifeScaleOptions(await LifeScalesService.GetAllAsync());
        RebuildArmourRestrictionRows();
        EnsureAbilityLevels();

        await BuildAbilityLookupAsync();

        var defaultBase = _allClasses.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(defaultBase))
            await SetBaseClassAsync(defaultBase);

        RebuildExpandedLifeScaleRows(GetEffectiveLifeScalePoints());
        IsLifeScaleAdvancedExpanded = false;
        IsPostEighthExpanded = false;
    }

    public async Task SearchBuyAsAsync(INavigation navigation)
    {
        if (navigation == null || _allBuyAsValues.Count == 0)
            return;

        var options = _allBuyAsValues
            .Select(value => new NonStandardSearchOption(value, "Buy-as", value))
            .ToList();

        var selected = await NonStandardSearchPage.PickManyAsync(navigation, "Select Buy-as (multiple allowed)", options);
        if (selected == null || selected.Count == 0)
            return;

        SelectedBuyAsOptions.Clear();
        foreach (var item in selected)
        {
            var value = (item.Value ?? string.Empty).Trim();
            if (value.Length > 0)
                SelectedBuyAsOptions.Add(value);
        }
        
        // Keep BuyAsText for backward compatibility
        BuyAsText = string.Join(", ", SelectedBuyAsOptions);
        Raise(nameof(CanSave));
    }

    public async Task LoadFromWalletEntryAsync(NonStandardWalletEntry? entry)
    {
        if (entry == null)
            return;

        await LoadFromSavedClassAsync(entry.Name, entry.DataJson);
    }

    public async Task LoadFromSavedClassAsync(string? className, string? payloadJson)
    {
        await InitializeAsync();

        var requestedName = (className ?? string.Empty).Trim();
        if (requestedName.Length == 0)
            return;

        JsonObject? payload = TryParsePayload(payloadJson);
        var baseNameFromTags = payload == null ? string.Empty : ExtractBaseClassTag(payload);

        if (baseNameFromTags.Length > 0 && _allClasses.TryGetValue(baseNameFromTags, out var baseRecord))
        {
            _baseClassRecord = baseRecord;
            BaseClassName = baseNameFromTags;
            ApplyBaseClass(baseRecord);
        }
        else if (_allClasses.TryGetValue(requestedName, out var existingRecord))
        {
            _baseClassRecord = existingRecord;
            BaseClassName = requestedName;
            ApplyBaseClass(existingRecord);
        }
        else
        {
            _baseClassRecord = null;
            BaseClassName = baseNameFromTags.Length > 0 ? baseNameFromTags : requestedName;
            IsRebirth = false;
            ReplaceCollection(SelectedBrackets, Array.Empty<string>());
            ReplaceCollection(SelectedArmourOptions, Array.Empty<string>());
            ReplaceCollection(ArmourRestrictions, Array.Empty<string>());
            ReplaceCollection(WeaponSkillRestrictions, Array.Empty<string>());
            WeaponSkillLevels.Clear();
            for (var level = 1; level <= 8; level++)
                WeaponSkillLevels.Add(new WeaponSkillLevelVm { Level = level });
            ClearAbilityLevels();
        }

        Name = requestedName;
        PathClassName = requestedName;

        if (payload != null)
            ApplyPayloadOverrides(payload);

        await ApplyLifeScaleForClassAsync(requestedName);
        SaveStatus = string.Empty;
        Raise(nameof(CanSave));
    }

    public async Task SearchBaseClassAsync(INavigation navigation)
    {
        if (navigation == null)
            return;

        var selected = await NonStandardClassSearchPage.PickAsync(navigation, BaseClassName);
        if (string.IsNullOrWhiteSpace(selected))
            return;

        await SetBaseClassAsync(selected);
    }

    public async Task SearchPathClassAsync(INavigation navigation)
    {
        if (navigation == null)
            return;

        var selected = await NonStandardClassSearchPage.PickAsync(navigation, PathClassName);
        if (string.IsNullOrWhiteSpace(selected))
            return;

        PathClassName = selected;
    }

    public void AddArmourRestriction()
    {
        var value = (ArmourRestrictionInput ?? string.Empty).Trim();
        if (value.Length == 0)
            return;

        if (!ArmourRestrictions.Any(x => x.Equals(value, StringComparison.OrdinalIgnoreCase)))
            ArmourRestrictions.Add(value);

        ArmourRestrictionInput = string.Empty;
    }

    public void RemoveArmourRestriction(string? restriction)
    {
        var value = (restriction ?? string.Empty).Trim();
        if (value.Length == 0)
            return;

        var existing = ArmourRestrictions.FirstOrDefault(x => x.Equals(value, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            ArmourRestrictions.Remove(existing);
    }

    private void OnArmourRestrictionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RebuildArmourRestrictionRows();

    private void RebuildArmourRestrictionRows()
    {
        ArmourRestrictionRows.Clear();
        for (var index = 0; index < ArmourRestrictions.Count; index++)
        {
            ArmourRestrictionRows.Add(new TextRowVm
            {
                Text = ArmourRestrictions[index],
                RowBackgroundHex = index % 2 == 0 ? "#F8FAFC" : "#FFFFFF"
            });
        }
    }

    public void AddWeaponSkillRestriction()
    {
        var value = (WeaponSkillRestrictionInput ?? string.Empty).Trim();
        if (value.Length == 0)
            return;

        if (!WeaponSkillRestrictions.Any(x => x.Equals(value, StringComparison.OrdinalIgnoreCase)))
            WeaponSkillRestrictions.Add(value);

        WeaponSkillRestrictionInput = string.Empty;
    }

    public void RemoveWeaponSkillRestriction(string? restriction)
    {
        var value = (restriction ?? string.Empty).Trim();
        if (value.Length == 0)
            return;

        var existing = WeaponSkillRestrictions.FirstOrDefault(x => x.Equals(value, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            WeaponSkillRestrictions.Remove(existing);
    }

    public async Task AddAbilityToLevelAsync(INavigation navigation, AbilityLevelVm? levelRow)
    {
        if (navigation == null || levelRow == null)
            return;

        var options = _abilityLookup
            .Keys
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Select(name => new NonStandardSearchOption(
                Title: name,
                Subtitle: _abilityLookup.TryGetValue(name, out var def) ? (def.Type ?? string.Empty) : string.Empty,
                Value: name))
            .ToList();

        var currentSelections = levelRow.Abilities
            .Select(row => (row.AbilityName ?? string.Empty).Trim())
            .Where(name => name.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var selected = await NonStandardSearchPage.PickManyAsync(
            navigation,
            "Select Abilities",
            options,
            currentSelections);

        if (selected.Count == 0)
            return;

        var selectedNames = selected
            .Select(item => (item.Value ?? string.Empty).Trim())
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var index = levelRow.Abilities.Count - 1; index >= 0; index--)
        {
            if (!selectedNames.Contains((levelRow.Abilities[index].AbilityName ?? string.Empty).Trim()))
                levelRow.Abilities.RemoveAt(index);
        }

        foreach (var item in selected)
        {
            var abilityName = (item.Value ?? string.Empty).Trim();
            if (abilityName.Length == 0)
                continue;

            if (levelRow.Abilities.Any(row => row.AbilityName.Equals(abilityName, StringComparison.OrdinalIgnoreCase)))
                continue;

            levelRow.Abilities.Add(CreateAbilityRow(levelRow.Level, abilityName));
        }

        ReindexAbilityRows(levelRow);
    }

    public void RemoveAbilityFromLevel(AbilityLevelVm? levelRow, ClassAbilityRowVm? row)
    {
        if (levelRow == null || row == null)
            return;

        levelRow.Abilities.Remove(row);
        ReindexAbilityRows(levelRow);
    }

    public void ReplaceAbilitiesForLevel(AbilityLevelVm? levelRow, IEnumerable<EvolutionService.AbilityResult>? selectedAbilities)
    {
        if (levelRow == null)
            return;

        var selectedNames = (selectedAbilities ?? Array.Empty<EvolutionService.AbilityResult>())
            .Select(ability => (ability.Index ?? string.Empty).Trim())
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var index = levelRow.Abilities.Count - 1; index >= 0; index--)
        {
            if (!selectedNames.Contains((levelRow.Abilities[index].AbilityName ?? string.Empty).Trim()))
                levelRow.Abilities.RemoveAt(index);
        }

        foreach (var abilityName in selectedNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            if (levelRow.Abilities.Any(row => row.AbilityName.Equals(abilityName, StringComparison.OrdinalIgnoreCase)))
                continue;

            levelRow.Abilities.Add(CreateAbilityRow(levelRow.Level, abilityName));
        }

        ReindexAbilityRows(levelRow);
    }

    public void AddCasterColourRow()
    {
        var used = CasterLevelRows
            .Select(row => row.Colour)
            .Where(colour => !string.Equals(colour, "All", StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var next = CasterColourOptions.FirstOrDefault(colour => !used.Contains(colour));
        if (string.IsNullOrWhiteSpace(next))
            return;

        CasterLevelRows.Add(new CasterLevelRowVm
        {
            Colour = next,
            LevelText = ""
        });
    }

    public void RemoveCasterColourRow(CasterLevelRowVm? row)
    {
        if (row == null)
            return;

        CasterLevelRows.Remove(row);
    }

    public async Task SearchLifeScaleAsync(INavigation navigation)
    {
        if (navigation == null || _lifeScaleOptions.Count == 0)
            return;

        var options = _lifeScaleOptions
            .Select(option => new NonStandardSearchOption(
                Title: option.DisplayTitle,
                Subtitle: option.DisplaySubtitle,
                Value: option.Key))
            .ToList();

        var selected = await NonStandardSearchPage.PickAsync(navigation, "Select Lifescale", options);
        if (selected == null)
            return;

        var match = _lifeScaleOptions.FirstOrDefault(option => option.Key.Equals(selected.Value, StringComparison.OrdinalIgnoreCase));
        if (match == null)
            return;

        _selectedLifeScaleOption = match;
        ApplyLifeScaleSearchFilters();
        Raise(nameof(LifescaleSummary));
        Raise(nameof(HasLifeScaleSelection));
        Raise(nameof(CanAddLifeScaleAssignment));
        Raise(nameof(CanSave));

        EnsureCustomLifeScaleRows(match.Points);

        RebuildExpandedLifeScaleRows(GetEffectiveLifeScalePoints());
    }

    public void SelectLifeScaleSearchOption(LifeScaleSearchOptionVm? option)
    {
        if (option == null)
            return;

        _selectedLifeScaleOption = option.Source;
        ApplyLifeScaleSearchFilters();
        EnsureCustomLifeScaleRows(option.Source.Points);
        RebuildExpandedLifeScaleRows(GetEffectiveLifeScalePoints());
        Raise(nameof(LifescaleSummary));
        Raise(nameof(HasLifeScaleSelection));
        Raise(nameof(CanAddLifeScaleAssignment));
        Raise(nameof(CanSave));
    }

    public async Task SearchLifeScaleRaceAsync(INavigation navigation)
    {
        if (navigation == null || _allRaceNames.Count == 0)
            return;

        var usedRaces = LifeScaleAssignments
            .Select(item => item.RaceName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var options = _allRaceNames
            .Where(race => !usedRaces.Contains(race))
            .Select(race => new NonStandardSearchOption(race, "Race", race))
            .ToList();

        if (options.Count == 0)
            return;

        var selected = await NonStandardSearchPage.PickAsync(navigation, "Assign Lifescale Race", options);
        if (selected == null)
            return;

        var raceName = (selected.Value ?? string.Empty).Trim();
        if (raceName.Length == 0)
            return;

        LifeScaleAssignmentRaceName = raceName;
    }

    public void AddLifeScaleAssignment()
    {
        if (!TryCreateCurrentLifeScaleAssignment(out var assignment))
            return;

        var existing = LifeScaleAssignments
            .FirstOrDefault(item => item.RaceName.Equals(assignment.RaceName, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            LifeScaleAssignments.Remove(existing);

        LifeScaleAssignments.Add(assignment);
        ReindexLifeScaleAssignments();
        LifeScaleAssignmentRaceName = string.Empty;
        Raise(nameof(CanSave));
    }

    public void AddLifeScaleAssignment(string? raceName)
    {
        var race = (raceName ?? string.Empty).Trim();
        if (race.Length == 0)
            return;

        LifeScaleAssignmentRaceName = race;
        AddLifeScaleAssignment();
    }

    public void RemoveLifeScaleAssignment(string? raceName)
    {
        var race = (raceName ?? string.Empty).Trim();
        if (race.Length == 0)
            return;

        var existing = LifeScaleAssignments
            .FirstOrDefault(item => item.RaceName.Equals(race, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
            return;

        LifeScaleAssignments.Remove(existing);
        ReindexLifeScaleAssignments();
        Raise(nameof(CanSave));
    }

    public void ToggleLifeScaleExpanded()
    {
        IsLifescaleExpanded = !IsLifescaleExpanded;
        Raise(nameof(LifescaleChevronText));
    }

    public async Task AddRaceToWhitelistAsync(INavigation navigation)
        => await AddRaceToListAsync(navigation, RaceWhitelist, RaceBlacklist);

    public async Task AddRaceToBlacklistAsync(INavigation navigation)
        => await AddRaceToListAsync(navigation, RaceBlacklist, RaceWhitelist);

    public async Task AddPostEighthRaceAsync(INavigation navigation)
    {
        if (navigation == null)
            return;

        var catalog = await MultiRaceService.GetCatalogAsync();
        var options = catalog.MultiRaces
            .Keys
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Select(value => new NonStandardSearchOption(value, "Multi-race", value))
            .ToList();

        var selected = await NonStandardSearchPage.PickAsync(navigation, "Select Multi-race", options);
        if (selected == null)
            return;

        AddPostEighthEntry(PostEighthEntryKind.MultiRace, selected.Value);
    }

    public async Task AddPostEighthClassAsync(INavigation navigation)
    {
        if (navigation == null)
            return;

        var catalog = await MultiClassService.GetCatalogAsync();
        var options = catalog.MultiClasses.Keys
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Select(value => new NonStandardSearchOption(value, "Multi-class", value))
            .ToList();

        var selected = await NonStandardSearchPage.PickAsync(navigation, "Select Multi-class", options);
        if (selected == null)
            return;

        AddPostEighthEntry(PostEighthEntryKind.MultiClass, selected.Value);
    }

    public void RemovePostEighthEntry(PostEighthEntryVm? entry)
    {
        if (entry == null)
            return;

        PostEighthEntries.Remove(entry);
        ReindexPostEighthEntries();
        SyncLegacyPostEighthCollections();
        Raise(nameof(PostEighthSummary));
    }

    public void ToggleLifeScaleAdvancedExpanded()
        => IsLifeScaleAdvancedExpanded = !IsLifeScaleAdvancedExpanded;

    public void ToggleLifeScaleSearchExpanded()
        => IsLifeScaleSearchExpanded = !IsLifeScaleSearchExpanded;

    public void TogglePostEighthExpanded()
        => IsPostEighthExpanded = !IsPostEighthExpanded;

    private async Task AddRaceToListAsync(INavigation navigation, ObservableCollection<string> target, ObservableCollection<string> opposite)
    {
        if (navigation == null || _allRaceNames.Count == 0)
            return;

        var options = _allRaceNames
            .Select(race => new NonStandardSearchOption(race, "Race", race))
            .ToList();

        var selected = await NonStandardSearchPage.PickAsync(navigation, "Select Race", options);
        if (selected == null)
            return;

        var raceName = (selected.Value ?? string.Empty).Trim();
        if (raceName.Length == 0)
            return;

        var existingOpposite = opposite.FirstOrDefault(x => x.Equals(raceName, StringComparison.OrdinalIgnoreCase));
        if (existingOpposite != null)
            opposite.Remove(existingOpposite);

        if (!target.Any(x => x.Equals(raceName, StringComparison.OrdinalIgnoreCase)))
            target.Add(raceName);
    }

    private void AddPostEighthEntry(PostEighthEntryKind kind, string? rawName, int tablesPerGain = 2, int startingTable = 5)
    {
        var name = (rawName ?? string.Empty).Trim();
        if (name.Length == 0)
            return;

        if (PostEighthEntries.Any(entry => entry.Kind == kind && entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return;

        var entry = new PostEighthEntryVm
        {
            Kind = kind,
            Name = name,
            TablesPerGainText = Math.Max(1, tablesPerGain).ToString(),
            StartingTableText = Math.Max(1, startingTable).ToString()
        };
        PostEighthEntries.Add(entry);

        ReindexPostEighthEntries();
        SyncLegacyPostEighthCollections();
        Raise(nameof(PostEighthSummary));
    }

    private void OnPostEighthEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems.OfType<PostEighthEntryVm>())
                item.PropertyChanged -= OnPostEighthEntryPropertyChanged;
        }

        if (e.NewItems != null)
        {
            foreach (var item in e.NewItems.OfType<PostEighthEntryVm>())
                item.PropertyChanged += OnPostEighthEntryPropertyChanged;
        }

        ReindexPostEighthEntries();
        SyncLegacyPostEighthCollections();
        Raise(nameof(PostEighthSummary));
    }

    private void OnPostEighthEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Raise(nameof(PostEighthSummary));
    }

    private void ReindexPostEighthEntries()
    {
        for (var index = 0; index < PostEighthEntries.Count; index++)
            PostEighthEntries[index].RowBackgroundHex = index % 2 == 0 ? "#F8FAFC" : "#FFFFFF";
    }

    private void SyncLegacyPostEighthCollections()
    {
        ReplaceCollection(PostEighthMultiRaces,
            PostEighthEntries
                .Where(entry => entry.Kind == PostEighthEntryKind.MultiRace)
                .Select(entry => entry.Name));

        ReplaceCollection(PostEighthMultiClasses,
            PostEighthEntries
                .Where(entry => entry.Kind == PostEighthEntryKind.MultiClass)
                .Select(entry => entry.Name));
    }

    public void RemoveFromWhitelist(string? race)
        => RemoveFromCollection(RaceWhitelist, race);

    public void RemoveFromBlacklist(string? race)
        => RemoveFromCollection(RaceBlacklist, race);

    public void MoveWhitelistToBlacklist(string? race)
        => MoveBetweenCollections(RaceWhitelist, RaceBlacklist, race);

    public void MoveBlacklistToWhitelist(string? race)
        => MoveBetweenCollections(RaceBlacklist, RaceWhitelist, race);

    public async Task SaveAsync()
    {
        if (!CanSave)
            throw new InvalidOperationException("Please complete required fields before saving.");

        var lifeScaleAssignments = BuildLifeScaleAssignmentsForSave();
        if (lifeScaleAssignments.Count == 0)
            throw new InvalidOperationException("Add at least one race lifescale assignment before saving.");

        IsBusy = true;
        SaveStatus = string.Empty;
        try
        {
            var payload = BuildClassPayload();
            var request = new NonStandardSaveRequest
            {
                EntityType = NonStandardEntityType.CharacterClass,
                Name = (Name ?? string.Empty).Trim(),
                DataJson = payload.ToJsonString(PrettyJson),
                LifeScaleAssignments = lifeScaleAssignments
            };

            await NonStandardContentService.SaveAsync(request);
            SaveStatus = $"Saved class '{request.Name}' as non-standard.";

            await LoadFromSavedClassAsync(request.Name, request.DataJson);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SetBaseClassAsync(string className)
    {
        var key = (className ?? string.Empty).Trim();
        if (key.Length == 0)
            return;

        if (!_allClasses.TryGetValue(key, out var record))
            return;

        _baseClassRecord = record;
        BaseClassName = key;
        Name = key;
        PathClassName = key;
        LifeScaleClassFilterText = key;

        ApplyBaseClass(record);
        Raise(nameof(TagsPreview));
        Raise(nameof(PowerbaseExplanation));
        Raise(nameof(CanSave));

        await Task.CompletedTask;
    }

    private void ApplyBaseClass(CharacterClassRecord record)
    {
        ReplaceCollection(SelectedBrackets, (record.Brackets ?? new List<string>())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2));

        IsRebirth = false;

        MaxAcText = ParseInt(record.MaxAC).ToString();
        BuyAsText = record.BuyAs?.FirstOrDefault() ?? string.Empty;

        var selectedArmour = new List<string>();
        foreach (var wearable in record.Armour?.Wearable ?? new List<string>())
        {
            foreach (var map in ArmourDisplayToRaw)
            {
                if (map.Value.Equals((wearable ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
                    selectedArmour.Add(map.Key);
            }
        }

        ReplaceCollection(SelectedArmourOptions, selectedArmour.Distinct(StringComparer.OrdinalIgnoreCase));
        ReplaceCollection(ArmourRestrictions, record.Armour?.Restrictions ?? new List<string>());

        BuildWeaponSkillRows(record);
        BuildAbilityRows(record);

        ApplyAlignmentRule(record.AlignmentRule);

        ReplaceCollection(SelectedPowerbases,
            (record.Powerbase ?? new List<string>())
            .Select(MapRawPowerbaseToDisplay)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.OrdinalIgnoreCase));

        RebuildPowerCalculations(record.PowerCalculations);
        RebuildCasterLevels(record.CasterLevel);

        if (_selectedLifeScaleOption == null)
        {
            _selectedLifeScaleOption = _lifeScaleOptions.FirstOrDefault(option =>
                option.ClassName.Equals(BaseClassName, StringComparison.OrdinalIgnoreCase));
        }

        EnsureCustomLifeScaleRows(GetDefaultLifeScalePointsForCustom());

        RebuildExpandedLifeScaleRows(GetEffectiveLifeScalePoints());
        ApplyLifeScaleSearchFilters();
        Raise(nameof(LifescaleSummary));
        Raise(nameof(HasLifeScaleSelection));
        Raise(nameof(CanAddLifeScaleAssignment));
    }

    private void ApplyPayloadOverrides(JsonObject payload)
    {
        var tags = ReadStringArray(payload, "Tags");
        if (tags.Count > 0)
        {
            IsRebirth = tags.Any(tag => tag.Equals("Rebirth", StringComparison.OrdinalIgnoreCase));

            var baseName = tags
                .FirstOrDefault(tag => tag.StartsWith("Base:", StringComparison.OrdinalIgnoreCase))
                ?.Split(':', 2)
                .LastOrDefault()
                ?.Trim();
            if (!string.IsNullOrWhiteSpace(baseName))
                BaseClassName = baseName;
        }

        var path = ReadString(payload, "Path");
        if (path.Length > 0)
            PathClassName = path;

        var brackets = ReadStringArray(payload, "Brackets");
        if (brackets.Count > 0)
            ReplaceCollection(SelectedBrackets, brackets);

        if (TryReadInt(payload, "Max AC", out var maxAc))
            MaxAcText = Math.Clamp(maxAc, 0, 24).ToString();

        var buyAs = ReadStringArray(payload, "Buy as");
        if (buyAs.Count > 0)
        {
            BuyAsText = buyAs[0];
            ReplaceCollection(SelectedBuyAsOptions, buyAs);
        }

        if (TryGetObject(payload, "Armour", out var armourObject))
        {
            var selectedArmour = ReadStringArray(armourObject, "Wearable")
                .Select(MapRawArmourToDisplay)
                .Where(display => display.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            ReplaceCollection(SelectedArmourOptions, selectedArmour);
            ReplaceCollection(ArmourRestrictions, ReadStringArray(armourObject, "Restrictions"));
        }

        var levels = ParseLevelsPayload(payload);
        if (levels.Count > 0)
        {
            BuildWeaponSkillRows(new CharacterClassRecord { Levels = levels });
            BuildAbilityRows(new CharacterClassRecord { Levels = levels });
        }

        if (TryGetObject(payload, "alignmentRule", out var alignmentRuleObject))
        {
            try
            {
                var parsedRule = JsonSerializer.Deserialize<AlignmentRule>(alignmentRuleObject.ToJsonString());
                ApplyAlignmentRule(parsedRule);
            }
            catch
            {
                // Ignore malformed alignment payloads and keep current defaults.
            }
        }

        var powerbases = ReadStringArray(payload, "Powerbase")
            .Select(MapRawPowerbaseToDisplay)
            .Where(display => display.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (powerbases.Count > 0)
            ReplaceCollection(SelectedPowerbases, powerbases);

        if (TryGetArray(payload, "PowerCalculations", out var powerCalculationsArray))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<PowerCalculation>>(powerCalculationsArray.ToJsonString())
                    ?? new List<PowerCalculation>();
                RebuildPowerCalculations(parsed);
            }
            catch
            {
                // Keep existing values if payload is malformed.
            }
        }

        if (TryReadInt(payload, "CasterLevel", out var casterLevel))
            RebuildCasterLevels(casterLevel);

        ReplaceCollection(RaceWhitelist, ReadStringArray(payload, "RaceWhitelist"));
        ReplaceCollection(RaceBlacklist, ReadStringArray(payload, "RaceBlacklist"));

        if (TryGetObject(payload, "Post8Progression", out var postEighthObject))
            ApplyPostEighthProgression(postEighthObject);
    }

    private void BuildWeaponSkillRows(CharacterClassRecord record)
    {
        WeaponSkillLevels.Clear();
        WeaponSkillRestrictions.Clear();

        for (var level = 1; level <= 8; level++)
        {
            var row = new WeaponSkillLevelVm { Level = level };

            if (record.Levels.TryGetValue(level.ToString(), out var abilities) && abilities != null)
            {
                foreach (var ability in abilities)
                {
                    if (!string.Equals((ability?.Type ?? string.Empty).Trim(), "WeaponSkill", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var display = MapWeaponSkillCodeToDisplay((ability?.Name ?? string.Empty).Trim());
                    if (display.Length > 0 && !row.SelectedSkills.Any(x => x.Equals(display, StringComparison.OrdinalIgnoreCase)))
                        row.SelectedSkills.Add(display);

                    var restriction = (ability?.Effect ?? string.Empty).Trim();
                    if (restriction.Length > 0 && !WeaponSkillRestrictions.Any(x => x.Equals(restriction, StringComparison.OrdinalIgnoreCase)))
                        WeaponSkillRestrictions.Add(restriction);
                }
            }

            WeaponSkillLevels.Add(row);
            AttachWeaponSkillRow(row);
        }

        EnforceWeaponSkillUniqueness();
    }

    private void BuildAbilityRows(CharacterClassRecord record)
    {
        EnsureAbilityLevels();
        ClearAbilityLevels();

        foreach (var levelEntry in record.Levels)
        {
            if (!int.TryParse(levelEntry.Key, out var level)
                || level < 1
                || level > 8)
            {
                continue;
            }

            foreach (var ability in levelEntry.Value ?? new List<AbilityDefinition>())
            {
                if (string.Equals((ability?.Type ?? string.Empty).Trim(), "WeaponSkill", StringComparison.OrdinalIgnoreCase))
                    continue;

                var row = CreateAbilityRow(
                    level,
                    ability?.Name ?? string.Empty,
                    ability?.Type ?? string.Empty,
                    ability?.Count);

                if (AbilityLevels.FirstOrDefault(entry => entry.Level == level) is { } levelRow)
                {
                    levelRow.Abilities.Add(row);
                }
            }

            if (AbilityLevels.FirstOrDefault(entry => entry.Level == level) is { } indexedLevelRow)
                ReindexAbilityRows(indexedLevelRow);
        }
    }

    private ClassAbilityRowVm CreateAbilityRow(
        int level,
        string abilityName,
        string? abilityType = null,
        int? innateCount = null)
    {
        var resolvedType = abilityType ?? string.Empty;
        if (resolvedType.Length == 0
            && _abilityLookup.TryGetValue(abilityName, out var lookupDef))
        {
            resolvedType = lookupDef.Type ?? string.Empty;
        }

        var isInnate = string.Equals(resolvedType.Trim(), "Innate", StringComparison.OrdinalIgnoreCase);
        var countText = innateCount?.ToString() ?? string.Empty;

        if (isInnate && countText.Length == 0)
            countText = "1";

        return new ClassAbilityRowVm
        {
            Level = level,
            AbilityName = abilityName,
            AbilityType = resolvedType,
            IsInnate = isInnate,
            CountText = countText
        };
    }

    private void EnsureAbilityLevels()
    {
        if (AbilityLevels.Count == 8)
            return;

        AbilityLevels.Clear();
        for (var level = 1; level <= 8; level++)
            AbilityLevels.Add(new AbilityLevelVm(level));
    }

    private void ClearAbilityLevels()
    {
        EnsureAbilityLevels();
        foreach (var levelRow in AbilityLevels)
            levelRow.Abilities.Clear();
    }

    private async Task ApplyLifeScaleForClassAsync(string className)
    {
        UseCustomLifeScale = false;
        LifeScaleAssignments.Clear();

        var classMappings = _lifeScaleOptions
            .Where(option => option.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase))
            .OrderBy(option => option.RaceName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.DisplayTitle, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var mapping in classMappings)
        {
            LifeScaleAssignments.Add(new LifeScaleAssignmentVm
            {
                RaceName = mapping.RaceName,
                ClassName = ResolveClassNameForAssignment(className),
                SourceSummary = $"as per: {mapping.DisplayTitle}",
                Points = mapping.Points.ToList()
            });
        }

        ReindexLifeScaleAssignments();
        LifeScaleAssignmentRaceName = string.Empty;
        _selectedLifeScaleOption = classMappings.FirstOrDefault();

        if (_selectedLifeScaleOption != null)
            EnsureCustomLifeScaleRows(_selectedLifeScaleOption.Points);
        else
            EnsureCustomLifeScaleRows(GetDefaultLifeScalePointsForCustom());

        RebuildExpandedLifeScaleRows(GetEffectiveLifeScalePoints());
        Raise(nameof(LifescaleSummary));
        Raise(nameof(HasLifeScaleSelection));
        Raise(nameof(CanAddLifeScaleAssignment));
        Raise(nameof(CanSave));
        await Task.CompletedTask;
    }

    private void BuildBracketOptions()
    {
        var options = _allClasses.Values
            .SelectMany(record => record.Brackets ?? new List<string>())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(text => NormalizeFilterSort(text), StringComparer.OrdinalIgnoreCase)
            .ToList();

        ReplaceCollection(BracketOptions, options);
    }

    private void InitializeAlignmentSelection()
    {
        SelectedAlignments.Clear();
        foreach (var alignment in EnumerateAllAlignments())
            SelectedAlignments.Add(alignment);
    }

    private static IEnumerable<Alignment> EnumerateAllAlignments()
    {
        foreach (var moral in new[] { MoralAxis.Good, MoralAxis.Neutral, MoralAxis.Evil })
        {
            foreach (var order in new[] { OrderAxis.Lawful, OrderAxis.Neutral, OrderAxis.Chaotic })
                yield return new Alignment(order, moral);
        }
    }

    private void BuildCasterColourOptions()
    {
        CasterColourOptions.Clear();
        foreach (var colour in Enum.GetValues<ExtendedMagicColours>())
        {
            if (colour == ExtendedMagicColours.All)
                continue;

            CasterColourOptions.Add(colour.ToString());
        }
    }

    private async Task BuildAbilityLookupAsync()
    {
        _abilityLookup.Clear();

        var evolutionAbilities = await EvolutionService.GetAllAbilitiesAsync();
        foreach (var ability in evolutionAbilities)
        {
            var key = (ability.Index ?? string.Empty).Trim();
            if (key.Length == 0)
                continue;

            _abilityLookup[key] = new AbilityDefinition
            {
                Name = key,
                Type = "Static",
                Effect = ability.Description ?? string.Empty
            };
        }

        var specialisations = await SpecialisationDefinitionRepository.GetIndexAsync();
        foreach (var reference in specialisations.AbilityReferences)
        {
            if (string.IsNullOrWhiteSpace(reference.Key))
                continue;

            _abilityLookup[reference.Key] = reference.Value;
        }
    }

    private void BuildLifeScaleOptions(Dictionary<string, Dictionary<string, List<int[]>>> lifeScales)
    {
        _lifeScaleOptions.Clear();

        foreach (var raceEntry in lifeScales)
        {
            var raceName = raceEntry.Key;
            foreach (var classEntry in raceEntry.Value)
            {
                var className = classEntry.Key;
                var rawPoints = classEntry.Value ?? new List<int[]>();
                var points = rawPoints
                    .Where(pair => pair != null && pair.Length >= 2)
                    .Select(pair => new LifeScalePoint(pair[0], pair[1]))
                    .ToList();

                if (points.Count == 0)
                    continue;

                var final = points[^1];
                _lifeScaleOptions.Add(new NonStandardLifeScaleOption
                {
                    Key = $"{raceName}|{className}",
                    RaceName = raceName,
                    ClassName = className,
                    Points = points,
                    DisplayTitle = $"{raceName} {className} - {final.Body},{final.Loc}",
                    DisplaySubtitle = $"{raceName} / {className}"
                });
            }
        }

        _lifeScaleOptions.Sort((left, right) =>
            string.Compare(left.DisplayTitle, right.DisplayTitle, StringComparison.OrdinalIgnoreCase));

        ApplyLifeScaleSearchFilters();
    }

    private void ApplyLifeScaleSearchFilters()
    {
        var classFilter = (LifeScaleClassFilterText ?? string.Empty).Trim();
        var raceFilter = (LifeScaleRaceFilterText ?? string.Empty).Trim();

        var filtered = _lifeScaleOptions
            .Where(option => classFilter.Length == 0
                             || option.ClassName.Contains(classFilter, StringComparison.OrdinalIgnoreCase))
            .Where(option => raceFilter.Length == 0
                             || option.RaceName.Contains(raceFilter, StringComparison.OrdinalIgnoreCase))
            .Take(200)
            .Select((option, index) => new LifeScaleSearchOptionVm
            {
                Source = option,
                DisplayTitle = option.DisplayTitle,
                DisplaySubtitle = option.DisplaySubtitle,
                IsSelected = _selectedLifeScaleOption != null
                    && option.Key.Equals(_selectedLifeScaleOption.Key, StringComparison.OrdinalIgnoreCase),
                RowBackgroundHex = _selectedLifeScaleOption != null
                    && option.Key.Equals(_selectedLifeScaleOption.Key, StringComparison.OrdinalIgnoreCase)
                    ? "#FFF7ED"
                    : (index % 2 == 0 ? "#F8FAFC" : "#FFFFFF")
            })
            .ToList();

        ReplaceCollection(FilteredLifeScaleOptions, filtered);
    }

    private void ApplyAlignmentRule(AlignmentRule? rule)
    {
        var allowed = rule == null
            ? EnumerateAllAlignments().ToList()
            : CharacterDraft.ComputeAvailableAlignments(new[] { rule }).ToList();

        if (allowed.Count == 0)
            allowed = EnumerateAllAlignments().ToList();

        ReplaceCollection(SelectedAlignments, allowed);
    }

    private void RebuildPowerCalculations(IReadOnlyList<PowerCalculation>? baseCalculations)
    {
        var existing = baseCalculations
            ?.Where(calc => calc != null)
            .ToDictionary(
                calc => (calc.PowerBase ?? string.Empty).Trim(),
                calc => calc.Calculation ?? string.Empty,
                StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        PowerCalculations.Clear();

        foreach (var display in SelectedPowerbases)
        {
            var raw = MapPowerbaseDisplayToRaw(display);
            PowerCalculations.Add(new PowerCalculationRowVm
            {
                PowerBaseDisplay = display,
                Calculation = existing.TryGetValue(raw, out var calc) ? calc : string.Empty
            });
        }
    }

    private void RebuildCasterLevels(int? baseCasterLevel)
    {
        CasterLevelRows.Clear();

        if (!HasMagicPowerbase)
            return;

        CasterLevelRows.Add(new CasterLevelRowVm
        {
            Colour = "All",
            LevelText = (baseCasterLevel ?? 8).ToString()
        });
    }

    private void RebuildExpandedLifeScaleRows(IReadOnlyList<LifeScalePoint> points)
    {
        ExpandedLifeScaleRows.Clear();

        for (var index = 0; index < points.Count; index++)
        {
            ExpandedLifeScaleRows.Add(new LifeScalePointVm
            {
                Level = index + 1,
                Body = points[index].Body,
                Loc = points[index].Loc
            });
        }
    }

    private void EnsureCustomLifeScaleRows(IReadOnlyList<LifeScalePoint> seedPoints)
    {
        var points = seedPoints?.ToList() ?? new List<LifeScalePoint>();
        if (points.Count == 0)
            points = GetDefaultLifeScalePointsForCustom().ToList();

        while (points.Count < 8)
            points.Add(new LifeScalePoint(0, 0));

        CustomLifeScaleRows.Clear();
        for (var index = 0; index < 8; index++)
        {
            var row = new CustomLifeScalePointVm
            {
                Level = index + 1,
                BodyText = points[index].Body.ToString(),
                LocText = points[index].Loc.ToString()
            };

            row.PropertyChanged += OnCustomLifeScaleRowChanged;
            CustomLifeScaleRows.Add(row);
        }

        SyncCustomLifeScaleOverrideState();
    }

    private IReadOnlyList<LifeScalePoint> BuildCustomLifeScalePoints()
    {
        return CustomLifeScaleRows
            .OrderBy(row => row.Level)
            .Select(row => new LifeScalePoint(
                Math.Max(0, ParseInt(row.BodyText)),
                Math.Max(0, ParseInt(row.LocText))))
            .ToList();
    }

    private IReadOnlyList<LifeScalePoint> GetDefaultLifeScalePointsForCustom()
    {
        return _selectedLifeScaleOption?.Points?.Count > 0
            ? _selectedLifeScaleOption.Points
            : Enumerable.Range(1, 8).Select(_ => new LifeScalePoint(0, 0)).ToList();
    }

    private void OnCustomLifeScaleRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        SyncCustomLifeScaleOverrideState();

        RebuildExpandedLifeScaleRows(GetEffectiveLifeScalePoints());
        Raise(nameof(LifescaleSummary));
        Raise(nameof(CanAddLifeScaleAssignment));
        Raise(nameof(CanSave));
    }

    private IReadOnlyList<LifeScalePoint> GetEffectiveLifeScalePoints()
    {
        if (UseCustomLifeScale)
            return BuildCustomLifeScalePoints();

        return _selectedLifeScaleOption?.Points ?? Array.Empty<LifeScalePoint>();
    }

    private void SyncCustomLifeScaleOverrideState()
    {
        var current = BuildCustomLifeScalePoints();
        var baseline = (_selectedLifeScaleOption?.Points?.Count ?? 0) > 0
            ? _selectedLifeScaleOption!.Points
            : GetDefaultLifeScalePointsForCustom();

        UseCustomLifeScale = !AreLifeScalePointsEqual(current, baseline);
    }

    private static bool AreLifeScalePointsEqual(IReadOnlyList<LifeScalePoint> left, IReadOnlyList<LifeScalePoint> right)
    {
        if (left.Count != right.Count)
            return false;

        for (var index = 0; index < left.Count; index++)
        {
            if (left[index].Body != right[index].Body || left[index].Loc != right[index].Loc)
                return false;
        }

        return true;
    }

    private static void ReindexAbilityRows(AbilityLevelVm levelRow)
    {
        for (var index = 0; index < levelRow.Abilities.Count; index++)
            levelRow.Abilities[index].RowBackgroundHex = index % 2 == 0 ? "#F8FAFC" : "#FFFFFF";
    }

    private IReadOnlyList<NonStandardLifeScaleAssignment> BuildLifeScaleAssignmentsForSave()
    {
        return LifeScaleAssignments
            .Where(item => !string.IsNullOrWhiteSpace((item.RaceName ?? string.Empty).Trim()) && item.Points.Count >= 8)
            .Select(item => new NonStandardLifeScaleAssignment
            {
                RaceName = item.RaceName,
                ClassName = (Name ?? string.Empty).Trim(),
                Points = item.Points.ToList()
            })
            .ToList();
    }

    private bool TryCreateCurrentLifeScaleAssignment(out LifeScaleAssignmentVm assignment)
    {
        assignment = new LifeScaleAssignmentVm();

        var raceName = (LifeScaleAssignmentRaceName ?? string.Empty).Trim();
        if (raceName.Length == 0)
            return false;

        var effective = GetEffectiveLifeScalePoints();
        if (effective.Count < 8)
            return false;

        if (!HasLifeScaleSelection)
            return false;

        assignment = new LifeScaleAssignmentVm
        {
            RaceName = raceName,
            ClassName = ResolveClassNameForAssignment(),
            SourceSummary = BuildCurrentLifeScaleSummary(),
            Points = effective.ToList()
        };
        return true;
    }

    private void ReindexLifeScaleAssignments()
    {
        for (var index = 0; index < LifeScaleAssignments.Count; index++)
            LifeScaleAssignments[index].RowBackgroundHex = index % 2 == 0 ? "#F8FAFC" : "#FFFFFF";

        Raise(nameof(CanAddLifeScaleAssignment));
    }

    private void RefreshLifeScaleAssignmentClassName()
    {
        if (LifeScaleAssignments.Count == 0)
            return;

        var resolvedClassName = ResolveClassNameForAssignment();
        for (var index = 0; index < LifeScaleAssignments.Count; index++)
        {
            var current = LifeScaleAssignments[index];
            if (string.Equals(current.ClassName, resolvedClassName, StringComparison.Ordinal))
                continue;

            LifeScaleAssignments[index] = current with { ClassName = resolvedClassName };
        }
    }

    private string ResolveClassNameForAssignment(string? fallbackClassName = null)
    {
        var explicitName = (Name ?? string.Empty).Trim();
        if (explicitName.Length > 0)
            return explicitName;

        var fallback = (fallbackClassName ?? string.Empty).Trim();
        if (fallback.Length > 0)
            return fallback;

        return (BaseClassName ?? string.Empty).Trim();
    }

    private string BuildCurrentLifeScaleSummary()
    {
        if (_selectedLifeScaleOption == null)
            return UseCustomLifeScale ? "From: custom values - Customised" : string.Empty;

        if (UseCustomLifeScale)
            return $"From: {_selectedLifeScaleOption.DisplayTitle} - Customised";

        return $"as per: {_selectedLifeScaleOption.DisplayTitle}";
    }

    private JsonObject BuildClassPayload()
    {
        var payload = new JsonObject
        {
            ["Brackets"] = JsonSerializer.SerializeToNode(SelectedBrackets.ToList()) ?? new JsonArray(),
            ["Tags"] = JsonSerializer.SerializeToNode(BuildTags()) ?? new JsonArray(),
            ["Buy as"] = JsonSerializer.SerializeToNode(SelectedBuyAsOptions.ToList()) ?? new JsonArray(),
            ["Levels"] = JsonSerializer.SerializeToNode(BuildLevelsPayload()) ?? new JsonObject(),
            ["Max AC"] = Math.Clamp(ParseInt(MaxAcText), 0, 24),
            ["Powerbase"] = JsonSerializer.SerializeToNode(SelectedPowerbases.Select(MapPowerbaseDisplayToRaw).Where(x => x.Length > 0).ToList()) ?? new JsonArray(),
            ["CasterLevel"] = ParseInt(CasterLevelRows.FirstOrDefault(row => row.Colour.Equals("All", StringComparison.OrdinalIgnoreCase))?.LevelText),
            ["PowerCalculations"] = JsonSerializer.SerializeToNode(BuildPowerCalculationPayload()) ?? new JsonArray(),
            ["alignmentRule"] = JsonSerializer.SerializeToNode(BuildAlignmentRulePayload()),
            ["Armour"] = JsonSerializer.SerializeToNode(BuildArmourPayload()),
            ["Path"] = (PathClassName ?? string.Empty).Trim(),
            ["RaceWhitelist"] = JsonSerializer.SerializeToNode(RaceWhitelist.ToList()) ?? new JsonArray(),
            ["RaceBlacklist"] = JsonSerializer.SerializeToNode(RaceBlacklist.ToList()) ?? new JsonArray(),
            ["NonStandard"] = true,
            ["nonStandard"] = true,
            ["is_default"] = 0
        };

        if (PostEighthEntries.Count > 0)
        {
            payload["Post8Progression"] = new JsonObject
            {
                ["Rate"] = Math.Max(1, ParseInt(PostEighthEntries.FirstOrDefault()?.TablesPerGainText)),
                ["Entries"] = JsonSerializer.SerializeToNode(BuildPostEighthPayloadEntries()) ?? new JsonArray(),
                ["MultiRaces"] = JsonSerializer.SerializeToNode(PostEighthMultiRaces.ToList()) ?? new JsonArray(),
                ["MultiClasses"] = JsonSerializer.SerializeToNode(PostEighthMultiClasses.ToList()) ?? new JsonArray(),
                ["Restrictions"] = JsonSerializer.SerializeToNode(PostEighthRestrictions.ToList()) ?? new JsonArray()
            };
        }

        if (_baseClassRecord != null && _baseClassRecord.GuildOverrides != null)
            payload["GuildOverrides"] = JsonSerializer.SerializeToNode(_baseClassRecord.GuildOverrides);

        return payload;
    }

    private IReadOnlyList<string> BuildTags()
    {
        var tags = new List<string> { "Non-standard" };
        if (IsRebirth)
            tags.Add("Rebirth");

        if (!string.IsNullOrWhiteSpace(BaseClassName))
            tags.Add($"Base:{BaseClassName}");

        return tags;
    }

    private void ApplyPostEighthProgression(JsonObject progression)
    {
        PostEighthEntries.Clear();
        ReplaceCollection(PostEighthRestrictions, ReadStringArray(progression, "Restrictions"));

        if (TryGetArray(progression, "Entries", out var entriesArray))
        {
            foreach (var node in entriesArray)
            {
                if (node is not JsonObject entryObject)
                    continue;

                var kindText = ReadString(entryObject, "Kind");
                var name = ReadString(entryObject, "Name");
                var rate = TryReadInt(entryObject, "TablesPerGain", out var parsedRate)
                    ? parsedRate
                    : TryReadInt(entryObject, "Rate", out var legacyRate) ? legacyRate : 2;
                var start = TryReadInt(entryObject, "StartingTable", out var parsedStart)
                    ? parsedStart
                    : TryReadInt(entryObject, "StartTable", out var legacyStart) ? legacyStart : 5;

                var kind = string.Equals(kindText, "MultiClass", StringComparison.OrdinalIgnoreCase)
                    ? PostEighthEntryKind.MultiClass
                    : PostEighthEntryKind.MultiRace;

                AddPostEighthEntry(kind, name, rate, start);
            }
        }

        if (PostEighthEntries.Count == 0)
        {
            var fallbackRate = TryReadInt(progression, "Rate", out var parsedRate) ? parsedRate : 2;
            foreach (var race in ReadStringArray(progression, "MultiRaces"))
                AddPostEighthEntry(PostEighthEntryKind.MultiRace, race, fallbackRate, 5);

            foreach (var className in ReadStringArray(progression, "MultiClasses"))
                AddPostEighthEntry(PostEighthEntryKind.MultiClass, className, fallbackRate, 5);
        }

        SyncLegacyPostEighthCollections();
        Raise(nameof(PostEighthSummary));
    }

    private IReadOnlyList<PostEighthProgressionPayloadEntry> BuildPostEighthPayloadEntries()
    {
        return PostEighthEntries
            .Select(entry => new PostEighthProgressionPayloadEntry
            {
                Kind = entry.Kind.ToString(),
                Name = entry.Name,
                TablesPerGain = Math.Max(1, ParseInt(entry.TablesPerGainText)),
                StartingTable = Math.Max(1, ParseInt(entry.StartingTableText))
            })
            .ToList();
    }

    private Dictionary<string, List<AbilityDefinition>> BuildLevelsPayload()
    {
        var levels = Enumerable.Range(1, 8)
            .ToDictionary(level => level.ToString(), _ => new List<AbilityDefinition>(), StringComparer.OrdinalIgnoreCase);

        foreach (var row in WeaponSkillLevels)
        {
            if (!levels.TryGetValue(row.Level.ToString(), out var list))
                continue;

            foreach (var selectedSkill in row.SelectedSkills)
            {
                var code = MapWeaponSkillDisplayToCode(selectedSkill);
                if (code.Length == 0)
                    continue;

                list.Add(new AbilityDefinition
                {
                    Name = code,
                    Type = "WeaponSkill"
                });
            }
        }

        foreach (var levelRow in AbilityLevels)
        {
            if (!levels.TryGetValue(levelRow.Level.ToString(), out var list))
                continue;

            foreach (var row in levelRow.Abilities)
            {
                var abilityName = (row.AbilityName ?? string.Empty).Trim();
                if (abilityName.Length == 0)
                    continue;

                var ability = _abilityLookup.TryGetValue(abilityName, out var definition)
                    ? CloneAbility(definition)
                    : new AbilityDefinition
                    {
                        Name = abilityName,
                        Type = string.IsNullOrWhiteSpace(row.AbilityType) ? "Static" : row.AbilityType
                    };

                ability.Name = abilityName;
                if (!string.IsNullOrWhiteSpace(row.AbilityType))
                    ability.Type = row.AbilityType;

                if (row.IsInnate)
                    ability.Count = ParseNullableInt(row.CountText) ?? 1;

                list.Add(ability);
            }
        }

        return levels;
    }

    private IReadOnlyList<PowerCalculation> BuildPowerCalculationPayload()
    {
        var result = new List<PowerCalculation>();

        foreach (var row in PowerCalculations)
        {
            var raw = MapPowerbaseDisplayToRaw(row.PowerBaseDisplay);
            if (raw.Length == 0)
                continue;

            var calculation = (row.Calculation ?? string.Empty).Trim();
            if (calculation.Length == 0)
                continue;

            result.Add(new PowerCalculation
            {
                PowerBase = raw,
                Calculation = calculation
            });
        }

        return result;
    }

    private object BuildArmourPayload()
    {
        var wearable = SelectedArmourOptions
            .Select(option => ArmourDisplayToRaw.TryGetValue(option, out var raw) ? raw : string.Empty)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ArmourAllowance
        {
            Wearable = wearable,
            Restrictions = ArmourRestrictions
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Select(text => text.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private AlignmentRule BuildAlignmentRulePayload()
    {
        var enabled = SelectedAlignments
            .Distinct()
            .ToList();
        var allowedMoral = enabled.Select(alignment => alignment.Moral).Distinct().ToList();
        var allowedOrder = enabled.Select(alignment => alignment.Order).Distinct().ToList();

        var pairs = enabled
            .Select(alignment => alignment.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new AlignmentRule
        {
            Mode = "set",
            Allowed = new AllowedAxes
            {
                Moral = allowedMoral,
                Order = allowedOrder
            },
            AllowedPairs = pairs
        };
    }

    private static int ParseInt(string? value)
        => ParseNullableInt(value) ?? 0;

    private static int ParseInt(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var parsed) => parsed,
            JsonValueKind.String => ParseInt(value.GetString()),
            _ => 0
        };
    }

    private static int? ParseNullableInt(string? value)
    {
        if (int.TryParse((value ?? string.Empty).Trim(), out var parsed))
            return parsed;

        return null;
    }

    private static JsonObject? TryParsePayload(string? json)
    {
        var raw = (json ?? string.Empty).Trim();
        if (raw.Length == 0)
            return null;

        try
        {
            return JsonNode.Parse(raw) as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryGetObject(JsonObject payload, string key, out JsonObject value)
    {
        value = new JsonObject();
        if (payload.TryGetPropertyValue(key, out var node) && node is JsonObject obj)
        {
            value = obj;
            return true;
        }

        return false;
    }

    private static bool TryGetArray(JsonObject payload, string key, out JsonArray value)
    {
        value = new JsonArray();
        if (payload.TryGetPropertyValue(key, out var node) && node is JsonArray arr)
        {
            value = arr;
            return true;
        }

        return false;
    }

    private static string ReadString(JsonObject payload, string key)
    {
        if (!payload.TryGetPropertyValue(key, out var node) || node == null)
            return string.Empty;

        return node switch
        {
            JsonValue value when value.TryGetValue<string>(out var text) => (text ?? string.Empty).Trim(),
            JsonValue value when value.TryGetValue<int>(out var number) => number.ToString(),
            _ => string.Empty
        };
    }

    private static List<string> ReadStringArray(JsonObject payload, string key)
    {
        if (!payload.TryGetPropertyValue(key, out var node) || node is not JsonArray array)
            return new List<string>();

        return array
            .Select(item =>
            {
                if (item is JsonValue value && value.TryGetValue<string>(out var text))
                    return (text ?? string.Empty).Trim();
                return string.Empty;
            })
            .Where(text => text.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool TryReadInt(JsonObject payload, string key, out int value)
    {
        value = 0;
        if (!payload.TryGetPropertyValue(key, out var node) || node == null)
            return false;

        if (node is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<int>(out var direct))
            {
                value = direct;
                return true;
            }

            if (jsonValue.TryGetValue<string>(out var asText) && int.TryParse((asText ?? string.Empty).Trim(), out var parsed))
            {
                value = parsed;
                return true;
            }
        }

        return false;
    }

    private static string ExtractBaseClassTag(JsonObject payload)
    {
        var tags = ReadStringArray(payload, "Tags");
        foreach (var tag in tags)
        {
            if (!tag.StartsWith("Base:", StringComparison.OrdinalIgnoreCase))
                continue;

            var token = tag.Split(':', 2).LastOrDefault()?.Trim();
            if (!string.IsNullOrWhiteSpace(token))
                return token;
        }

        return string.Empty;
    }

    private static Dictionary<string, List<AbilityDefinition>> ParseLevelsPayload(JsonObject payload)
    {
        if (!payload.TryGetPropertyValue("Levels", out var levelsNode) || levelsNode == null)
        {
            payload.TryGetPropertyValue("levels", out levelsNode);
        }

        if (levelsNode == null)
            return new Dictionary<string, List<AbilityDefinition>>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, List<AbilityDefinition>>>(levelsNode.ToJsonString())
                ?? new Dictionary<string, List<AbilityDefinition>>(StringComparer.OrdinalIgnoreCase);

            return parsed;
        }
        catch
        {
            return new Dictionary<string, List<AbilityDefinition>>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string MapRawArmourToDisplay(string raw)
    {
        foreach (var map in ArmourDisplayToRaw)
        {
            if (map.Value.Equals((raw ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
                return map.Key;
        }

        return string.Empty;
    }

    private static string NormalizeFilterSort(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
            return string.Empty;

        var firstSpace = text.IndexOf(' ');
        if (firstSpace <= 0 || firstSpace >= text.Length - 1)
            return text;

        var prefix = text[..firstSpace];
        var hasEmojiPrefix = prefix.Any(ch => !char.IsLetterOrDigit(ch));
        return hasEmojiPrefix ? text[(firstSpace + 1)..].Trim() : text;
    }

    private void OnSelectedBracketsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Raise(nameof(HasBracketWarning));
        Raise(nameof(BracketSelectionHasIssue));
        Raise(nameof(CanSave));
    }

    private void OnSelectedPowerbasesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Raise(nameof(HasMagicPowerbase));
        Raise(nameof(PowerbaseExplanation));

        RebuildPowerCalculations(PowerCalculations
            .Select(row => new PowerCalculation
            {
                PowerBase = MapPowerbaseDisplayToRaw(row.PowerBaseDisplay),
                Calculation = row.Calculation
            })
            .ToList());

        var allLevel = CasterLevelRows.FirstOrDefault(row => row.Colour.Equals("All", StringComparison.OrdinalIgnoreCase))?.LevelText;
        RebuildCasterLevels(ParseNullableInt(allLevel));
    }

    private static string MapRawPowerbaseToDisplay(string? raw)
    {
        var normalized = (raw ?? string.Empty).Trim();
        if (normalized.Equals("Earthpower", StringComparison.OrdinalIgnoreCase))
            return "🌍 Earthpower";

        if (normalized.Equals("Magical", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Magic", StringComparison.OrdinalIgnoreCase))
        {
            return "🪄 Magic";
        }

        if (normalized.Equals("Spiritual", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Spirit", StringComparison.OrdinalIgnoreCase))
        {
            return "✨ Spirit";
        }

        if (normalized.Equals("Neuro", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Neuronic", StringComparison.OrdinalIgnoreCase))
        {
            return "🧠 Neuronic";
        }

        return string.Empty;
    }

    private static string MapPowerbaseDisplayToRaw(string? display)
    {
        var normalized = (display ?? string.Empty).Trim();
        return PowerbaseDisplayToRaw.TryGetValue(normalized, out var raw)
            ? raw
            : string.Empty;
    }

    private static bool IsMagicPowerbaseDisplay(string? display)
        => string.Equals((display ?? string.Empty).Trim(), "🪄 Magic", StringComparison.OrdinalIgnoreCase);

    private static string MapWeaponSkillCodeToDisplay(string? code)
    {
        var token = (code ?? string.Empty).Trim();
        if (token.Length == 0)
            return string.Empty;

        foreach (var entry in WeaponSkillDisplayToCode)
        {
            if (entry.Value.Equals(token, StringComparison.OrdinalIgnoreCase))
                return entry.Key;
        }

        if (token.Equals("UAC", StringComparison.OrdinalIgnoreCase))
            return "UAC (unarmed combat)";

        return string.Empty;
    }

    private static string MapWeaponSkillDisplayToCode(string? display)
    {
        var text = (display ?? string.Empty).Trim();
        if (text.Length == 0)
            return string.Empty;

        return WeaponSkillDisplayToCode.TryGetValue(text, out var code)
            ? code
            : string.Empty;
    }

    private void AttachWeaponSkillRow(WeaponSkillLevelVm row)
    {
        row.SelectedSkills.CollectionChanged += (_, _) => EnforceWeaponSkillUniqueness(row.Level);
    }

    private void EnforceWeaponSkillUniqueness(int preferredLevel = 0)
    {
        if (WeaponSkillLevels.Count == 0)
            return;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = preferredLevel > 0
            ? WeaponSkillLevels.OrderByDescending(level => level.Level == preferredLevel).ThenBy(level => level.Level).ToList()
            : WeaponSkillLevels.OrderBy(level => level.Level).ToList();

        foreach (var level in ordered)
        {
            for (var index = level.SelectedSkills.Count - 1; index >= 0; index--)
            {
                var skill = (level.SelectedSkills[index] ?? string.Empty).Trim();
                if (skill.Length == 0)
                {
                    level.SelectedSkills.RemoveAt(index);
                    continue;
                }

                if (!seen.Add(skill))
                    level.SelectedSkills.RemoveAt(index);
            }
        }
    }

    private static AbilityDefinition CloneAbility(AbilityDefinition source)
    {
        return new AbilityDefinition
        {
            Key = source.Key,
            Name = source.Name,
            Type = source.Type,
            Effect = source.Effect,
            BattleboardNameOverride = source.BattleboardNameOverride,
            UpdateKey = source.UpdateKey,
            Source = source.Source,
            Count = source.Count,
            Amount = source.Amount?.ToList(),
            Frequency = source.Frequency,
            OverwriteKey = source.OverwriteKey,
            PreReqs = source.PreReqs?.ToList(),
            GuildOverrides = source.GuildOverrides?.ToList(),
            Customisation = source.Customisation == null
                ? null
                : new AbilityCustomisation
                {
                    OptionEnum = source.Customisation.OptionEnum,
                    CustomValuesPermitted = source.Customisation.CustomValuesPermitted
                }
        };
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> collection, IEnumerable<T> values)
    {
        collection.Clear();
        foreach (var value in values)
            collection.Add(value);
    }

    private static void RemoveFromCollection(ObservableCollection<string> collection, string? value)
    {
        var token = (value ?? string.Empty).Trim();
        if (token.Length == 0)
            return;

        var existing = collection.FirstOrDefault(item => item.Equals(token, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            collection.Remove(existing);
    }

    private static void MoveBetweenCollections(ObservableCollection<string> source, ObservableCollection<string> target, string? value)
    {
        var token = (value ?? string.Empty).Trim();
        if (token.Length == 0)
            return;

        RemoveFromCollection(source, token);
        if (!target.Any(item => item.Equals(token, StringComparison.OrdinalIgnoreCase)))
            target.Add(token);
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

public sealed class WeaponSkillLevelVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private int _level;

    public WeaponSkillLevelVm()
    {
        SelectedSkills.CollectionChanged += (_, _) => Raise(nameof(SelectedSummary));
    }

    public int Level
    {
        get => _level;
        init
        {
            _level = value;
            Raise(nameof(RowBackgroundHex));
        }
    }

    public ObservableCollection<string> SelectedSkills { get; } = new();

    public string SelectedSummary
        => SelectedSkills.Count == 0
            ? "-"
            : string.Join("  ", SelectedSkills.Select(ToSkillCode));

    public string RowBackgroundHex => Level % 2 == 0 ? "#F8FAFC" : "#FFFFFF";

    private static string ToSkillCode(string? display)
    {
        var text = (display ?? string.Empty).Trim();
        if (text.Length == 0)
            return string.Empty;

        var firstSpace = text.IndexOf(' ');
        if (firstSpace > 0)
            return text[..firstSpace].Trim();

        return text;
    }

    private void Raise([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class AbilityLevelVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public AbilityLevelVm(int level)
    {
        Level = level;
        Abilities.CollectionChanged += OnAbilitiesChanged;
    }

    public int Level { get; }
    public ObservableCollection<ClassAbilityRowVm> Abilities { get; } = new();
    public string RowBackgroundHex => Level % 2 == 0 ? "#F8FAFC" : "#FFFFFF";

    public string SummaryText
        => Abilities.Count == 0
            ? "-"
            : string.Join("  |  ", Abilities.Select(FormatAbilitySummary));

    private void OnAbilitiesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems.OfType<ClassAbilityRowVm>())
                item.PropertyChanged -= OnAbilityRowChanged;
        }

        if (e.NewItems != null)
        {
            foreach (var item in e.NewItems.OfType<ClassAbilityRowVm>())
                item.PropertyChanged += OnAbilityRowChanged;
        }

        Raise(nameof(SummaryText));
    }

    private void OnAbilityRowChanged(object? sender, PropertyChangedEventArgs e)
        => Raise(nameof(SummaryText));

    private static string FormatAbilitySummary(ClassAbilityRowVm row)
    {
        var name = (row.AbilityName ?? string.Empty).Trim();
        if (name.Length == 0)
            name = "(unnamed ability)";

        if (!row.IsInnate)
            return name;

        var count = row.CountText;
        if (string.IsNullOrWhiteSpace((count ?? string.Empty).Trim()))
            count = "1";

        return $"{name} x{count}";
    }

    private void Raise([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class ClassAbilityRowVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private int _level;
    private string _abilityName = string.Empty;
    private string _abilityType = string.Empty;
    private bool _isInnate;
    private string _countText = string.Empty;
    private string _rowBackgroundHex = "#FFFFFF";

    public int Level
    {
        get => _level;
        set
        {
            if (_level == value)
                return;

            _level = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Level)));
        }
    }

    public string AbilityName
    {
        get => _abilityName;
        set
        {
            if (string.Equals(_abilityName, value, StringComparison.Ordinal))
                return;

            _abilityName = value ?? string.Empty;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AbilityName)));
        }
    }

    public string AbilityType
    {
        get => _abilityType;
        set
        {
            if (string.Equals(_abilityType, value, StringComparison.Ordinal))
                return;

            _abilityType = value ?? string.Empty;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AbilityType)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasAbilityType)));
        }
    }

    public bool IsInnate
    {
        get => _isInnate;
        set
        {
            if (_isInnate == value)
                return;

            _isInnate = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsInnate)));
        }
    }

    public string CountText
    {
        get => _countText;
        set
        {
            if (string.Equals(_countText, value, StringComparison.Ordinal))
                return;

            _countText = value ?? string.Empty;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CountText)));
        }
    }

    public string RowBackgroundHex
    {
        get => _rowBackgroundHex;
        set
        {
            if (string.Equals(_rowBackgroundHex, value, StringComparison.Ordinal))
                return;

            _rowBackgroundHex = value ?? "#FFFFFF";
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RowBackgroundHex)));
        }
    }

    public bool HasAbilityType => !string.IsNullOrWhiteSpace((_abilityType ?? string.Empty).Trim());
}

public sealed class PowerCalculationRowVm
{
    public string PowerBaseDisplay { get; set; } = string.Empty;
    public string Calculation { get; set; } = string.Empty;
}

public sealed class CasterLevelRowVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _colour = string.Empty;
    private string _levelText = string.Empty;

    public string Colour
    {
        get => _colour;
        set
        {
            if (string.Equals(_colour, value, StringComparison.Ordinal))
                return;

            _colour = value ?? string.Empty;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Colour)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsAll)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanRemove)));
        }
    }

    public string LevelText
    {
        get => _levelText;
        set
        {
            if (string.Equals(_levelText, value, StringComparison.Ordinal))
                return;

            _levelText = value ?? string.Empty;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LevelText)));
        }
    }

    public bool IsAll => Colour.Equals("All", StringComparison.OrdinalIgnoreCase);
    public bool CanRemove => !IsAll;
}

public sealed class CustomLifeScalePointVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _bodyText = string.Empty;
    private string _locText = string.Empty;

    public int Level { get; init; }
    public string RowBackgroundHex => Level % 2 == 0 ? "#F8FAFC" : "#FFFFFF";

    public string BodyText
    {
        get => _bodyText;
        set
        {
            if (string.Equals(_bodyText, value, StringComparison.Ordinal))
                return;

            _bodyText = value ?? string.Empty;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BodyText)));
        }
    }

    public string LocText
    {
        get => _locText;
        set
        {
            if (string.Equals(_locText, value, StringComparison.Ordinal))
                return;

            _locText = value ?? string.Empty;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LocText)));
        }
    }
}

public sealed record class LifeScaleAssignmentVm
{
    public string RaceName { get; init; } = string.Empty;
    public string ClassName { get; init; } = string.Empty;
    public string SourceSummary { get; init; } = string.Empty;
    public IReadOnlyList<LifeScalePoint> Points { get; init; } = Array.Empty<LifeScalePoint>();
    public string RowBackgroundHex { get; set; } = "#FFFFFF";
    public string RaceClassSummary => $"{RaceName} {ClassName}".Trim();
    public string LevelEightSummary
    {
        get
        {
            if (Points.Count == 0)
                return string.Empty;

            var last = Points[^1];
            return $"Level 8: {last.Body},{last.Loc}";
        }
    }
}

public sealed class TextRowVm
{
    public string Text { get; init; } = string.Empty;
    public string RowBackgroundHex { get; init; } = "#FFFFFF";
}

public enum PostEighthEntryKind
{
    MultiRace,
    MultiClass
}

public sealed class PostEighthEntryVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _name = string.Empty;
    private string _tablesPerGainText = "2";
    private string _startingTableText = "5";
    private string _rowBackgroundHex = "#FFFFFF";

    public PostEighthEntryKind Kind { get; init; }

    public string KindLabel => Kind == PostEighthEntryKind.MultiRace ? "Multi-race" : "Multi-class";

    public string Name
    {
        get => _name;
        set
        {
            if (string.Equals(_name, value, StringComparison.Ordinal))
                return;

            _name = value ?? string.Empty;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReviewSummary)));
        }
    }

    public string TablesPerGainText
    {
        get => _tablesPerGainText;
        set
        {
            if (string.Equals(_tablesPerGainText, value, StringComparison.Ordinal))
                return;

            _tablesPerGainText = value ?? "2";
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TablesPerGainText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReviewSummary)));
        }
    }

    public string StartingTableText
    {
        get => _startingTableText;
        set
        {
            if (string.Equals(_startingTableText, value, StringComparison.Ordinal))
                return;

            _startingTableText = value ?? "5";
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StartingTableText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReviewSummary)));
        }
    }

    public string RowBackgroundHex
    {
        get => _rowBackgroundHex;
        set
        {
            if (string.Equals(_rowBackgroundHex, value, StringComparison.Ordinal))
                return;

            _rowBackgroundHex = value ?? "#FFFFFF";
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RowBackgroundHex)));
        }
    }

    public string ReviewSummary
        => $"{KindLabel}: {Name} - 1 per {Math.Max(1, int.TryParse(TablesPerGainText, out var tables) ? tables : 2)} tables, starting at table {Math.Max(1, int.TryParse(StartingTableText, out var start) ? start : 5)}";
}

public sealed class PostEighthProgressionPayloadEntry
{
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int TablesPerGain { get; set; }
    public int StartingTable { get; set; }
}

public sealed class AlignmentCellVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _isEnabled;

    public Alignment Alignment { get; init; }

    public string Display => Alignment.ToString();

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
                return;

            _isEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CardState)));
        }
    }

    public string CardState => IsEnabled ? "Enabled" : "Disabled";
}

public sealed class LifeScalePointVm
{
    public int Level { get; init; }
    public int Body { get; init; }
    public int Loc { get; init; }
}

public sealed class NonStandardLifeScaleOption
{
    public string Key { get; init; } = string.Empty;
    public string RaceName { get; init; } = string.Empty;
    public string ClassName { get; init; } = string.Empty;
    public string DisplayTitle { get; init; } = string.Empty;
    public string DisplaySubtitle { get; init; } = string.Empty;
    public IReadOnlyList<LifeScalePoint> Points { get; init; } = Array.Empty<LifeScalePoint>();
}

public sealed class LifeScaleSearchOptionVm
{
    public required NonStandardLifeScaleOption Source { get; init; }
    public string DisplayTitle { get; init; } = string.Empty;
    public string DisplaySubtitle { get; init; } = string.Empty;
    public bool IsSelected { get; init; }
    public string RowBackgroundHex { get; init; } = "#FFFFFF";
}
