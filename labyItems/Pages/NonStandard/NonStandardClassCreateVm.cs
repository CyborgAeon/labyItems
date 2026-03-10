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
    private readonly List<NonStandardLifeScaleOption> _lifeScaleOptions = new();

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
    private string _lifescaleSummary = string.Empty;
    private bool _isLifescaleExpanded;
    private bool _useCustomLifeScale;

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

    public ObservableCollection<ClassAbilityRowVm> AbilityRows { get; } = new();
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
    public ObservableCollection<string> RaceWhitelist { get; } = new();
    public ObservableCollection<string> RaceBlacklist { get; } = new();

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
        private set => Set(ref _baseClassName, value ?? string.Empty);
    }

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

    public string LifescaleSummary
    {
        get => _lifescaleSummary;
        private set => Set(ref _lifescaleSummary, value ?? string.Empty);
    }

    public bool HasLifeScaleSelection => _selectedLifeScaleOption != null;

    public bool IsLifescaleExpanded
    {
        get => _isLifescaleExpanded;
        set => Set(ref _isLifescaleExpanded, value);
    }

    public string LifescaleChevronText => IsLifescaleExpanded ? "▴" : "▾";

    public bool UseCustomLifeScale
    {
        get => _useCustomLifeScale;
        set
        {
            if (!Set(ref _useCustomLifeScale, value))
                return;

            if (_useCustomLifeScale)
                EnsureCustomLifeScaleRows(GetDefaultLifeScalePointsForCustom());

            RebuildExpandedLifeScaleRows(GetEffectiveLifeScalePoints());
            Raise(nameof(CanSave));
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

            if (!TryGetLifeScaleForSave(out _, out _))
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

        _allClasses.Clear();
        var classes = await ClassService.GetAllAsync();
        foreach (var entry in classes)
            _allClasses[entry.Key] = entry.Value;

        _allRaceNames.Clear();
        var races = await PeopleService.GetAllAsync();
        _allRaceNames.AddRange(races.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

        BuildBracketOptions();
        InitializeAlignmentSelection();
        BuildCasterColourOptions();
        BuildLifeScaleOptions(await LifeScalesService.GetAllAsync());

        await BuildAbilityLookupAsync();

        var defaultBase = _allClasses.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(defaultBase))
            await SetBaseClassAsync(defaultBase);

        if (AbilityRows.Count == 0)
            AbilityRows.Add(new ClassAbilityRowVm { Level = 1 });

        RebuildExpandedLifeScaleRows(GetEffectiveLifeScalePoints());
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
            AbilityRows.Clear();
            for (var level = 1; level <= 8; level++)
                WeaponSkillLevels.Add(new WeaponSkillLevelVm { Level = level });
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

    public void AddAbilityRow()
        => AbilityRows.Add(new ClassAbilityRowVm { Level = 1 });

    public void RemoveAbilityRow(ClassAbilityRowVm? row)
    {
        if (row == null)
            return;

        AbilityRows.Remove(row);
        if (AbilityRows.Count == 0)
            AbilityRows.Add(new ClassAbilityRowVm { Level = 1 });
    }

    public async Task SearchAbilityAsync(INavigation navigation, ClassAbilityRowVm? row)
    {
        if (navigation == null || row == null)
            return;

        var options = _abilityLookup
            .Keys
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Select(name => new NonStandardSearchOption(
                Title: name,
                Subtitle: _abilityLookup.TryGetValue(name, out var def) ? (def.Type ?? string.Empty) : string.Empty,
                Value: name))
            .ToList();

        var selected = await NonStandardSearchPage.PickAsync(navigation, "Select Ability", options);
        if (selected == null)
            return;

        row.AbilityName = selected.Value;
        if (_abilityLookup.TryGetValue(selected.Value, out var definition))
        {
            row.AbilityType = definition.Type ?? string.Empty;
            row.IsInnate = (definition.Type ?? string.Empty).Equals("Innate", StringComparison.OrdinalIgnoreCase);
            if (row.IsInnate && string.IsNullOrWhiteSpace(row.CountText))
                row.CountText = "1";
        }
        else
        {
            row.AbilityType = string.Empty;
            row.IsInnate = false;
        }
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
        LifescaleSummary = match.DisplayTitle;
        Raise(nameof(HasLifeScaleSelection));
        Raise(nameof(CanSave));

        EnsureCustomLifeScaleRows(match.Points);

        RebuildExpandedLifeScaleRows(GetEffectiveLifeScalePoints());
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

        if (!TryGetLifeScaleForSave(out var lifeScaleRace, out var lifeScalePoints))
            throw new InvalidOperationException("A lifescale selection is required.");

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
                LifeScaleRaceName = lifeScaleRace,
                LifeScalePoints = lifeScalePoints
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

            if (_selectedLifeScaleOption != null)
                LifescaleSummary = _selectedLifeScaleOption.DisplayTitle;
        }

        EnsureCustomLifeScaleRows(GetDefaultLifeScalePointsForCustom());

        RebuildExpandedLifeScaleRows(GetEffectiveLifeScalePoints());
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
            BuyAsText = buyAs[0];

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
        }
    }

    private void BuildAbilityRows(CharacterClassRecord record)
    {
        AbilityRows.Clear();

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

                var row = new ClassAbilityRowVm
                {
                    Level = level,
                    AbilityName = ability?.Name ?? string.Empty,
                    AbilityType = ability?.Type ?? string.Empty,
                    IsInnate = string.Equals((ability?.Type ?? string.Empty).Trim(), "Innate", StringComparison.OrdinalIgnoreCase),
                    CountText = ability?.Count?.ToString() ?? string.Empty
                };

                AbilityRows.Add(row);
            }
        }

        if (AbilityRows.Count == 0)
            AbilityRows.Add(new ClassAbilityRowVm { Level = 1 });
    }

    private async Task ApplyLifeScaleForClassAsync(string className)
    {
        UseCustomLifeScale = false;
        _selectedLifeScaleOption = _lifeScaleOptions.FirstOrDefault(option =>
            option.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase));

        if (_selectedLifeScaleOption == null)
        {
            LifescaleSummary = string.Empty;
            Raise(nameof(HasLifeScaleSelection));
            Raise(nameof(CanSave));
            RebuildExpandedLifeScaleRows(GetEffectiveLifeScalePoints());
            return;
        }

        LifescaleSummary = _selectedLifeScaleOption.DisplayTitle;
        Raise(nameof(HasLifeScaleSelection));
        Raise(nameof(CanSave));
        EnsureCustomLifeScaleRows(_selectedLifeScaleOption.Points);

        RebuildExpandedLifeScaleRows(GetEffectiveLifeScalePoints());
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
        if (!UseCustomLifeScale)
            return;

        RebuildExpandedLifeScaleRows(GetEffectiveLifeScalePoints());
        Raise(nameof(CanSave));
    }

    private IReadOnlyList<LifeScalePoint> GetEffectiveLifeScalePoints()
    {
        if (UseCustomLifeScale)
            return BuildCustomLifeScalePoints();

        return _selectedLifeScaleOption?.Points ?? Array.Empty<LifeScalePoint>();
    }

    private bool TryGetLifeScaleForSave(out string raceName, out IReadOnlyList<LifeScalePoint> points)
    {
        raceName = string.Empty;
        points = Array.Empty<LifeScalePoint>();

        var effective = GetEffectiveLifeScalePoints();
        if (effective.Count < 8)
            return false;

        if (_selectedLifeScaleOption == null)
            return false;

        raceName = _selectedLifeScaleOption.RaceName;
        points = effective;
        return true;
    }

    private JsonObject BuildClassPayload()
    {
        var payload = new JsonObject
        {
            ["Brackets"] = JsonSerializer.SerializeToNode(SelectedBrackets.ToList()) ?? new JsonArray(),
            ["Tags"] = JsonSerializer.SerializeToNode(BuildTags()) ?? new JsonArray(),
            ["Buy as"] = JsonSerializer.SerializeToNode(new[] { (BuyAsText ?? string.Empty).Trim() }) ?? new JsonArray(),
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

        foreach (var row in AbilityRows)
        {
            if (row.Level < 1 || row.Level > 8)
                continue;

            if (!levels.TryGetValue(row.Level.ToString(), out var list))
                continue;

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

public sealed class WeaponSkillLevelVm
{
    public int Level { get; init; }
    public ObservableCollection<string> SelectedSkills { get; } = new();
}

public sealed class ClassAbilityRowVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private int _level;
    private string _abilityName = string.Empty;
    private string _abilityType = string.Empty;
    private bool _isInnate;
    private string _countText = string.Empty;

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
