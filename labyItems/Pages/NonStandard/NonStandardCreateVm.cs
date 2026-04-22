using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using labyItems.Helpers;
using labyItems.Models;
using labyItems.Models.Characters;
using labyItems.Services;

namespace labyItems.Pages.NonStandard;

public sealed class NonStandardCreateVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private static readonly IReadOnlyList<string> GuildOverrideTypes =
    [
        "Political",
        "Professional",
        "Social",
        "City"
    ];

    private static readonly HashSet<string> RaceStructuredFieldKeys = new(
        [
            NormalizeFieldKey("PeopleType"),
            NormalizeFieldKey("Tags"),
            NormalizeFieldKey("levelledAbilities"),
            NormalizeFieldKey("Subtype"),
            NormalizeFieldKey("Buy-as"),
            NormalizeFieldKey("GuildOverrides"),
            NormalizeFieldKey("alignmentRule")
        ],
        StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions GuildJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new GuildOverrideRulesConverter() }
    };

    private static readonly JsonSerializerOptions PrettyJson = new()
    {
        WriteIndented = true
    };

    private static readonly IReadOnlyDictionary<NonStandardEntityType, IReadOnlyList<NonStandardFieldDefinition>> FieldDefinitions =
        new Dictionary<NonStandardEntityType, IReadOnlyList<NonStandardFieldDefinition>>
        {
            [NonStandardEntityType.CharacterClass] =
            [
                new("Brackets", NonStandardFieldKind.Json),
                new("Tags", NonStandardFieldKind.Json),
                new("Buy as", NonStandardFieldKind.Json),
                new("Levels", NonStandardFieldKind.Json),
                new("Max AC", NonStandardFieldKind.Number),
                new("Powerbase", NonStandardFieldKind.Json),
                new("CasterLevel", NonStandardFieldKind.Number),
                new("PowerCalculations", NonStandardFieldKind.Json),
                new("alignmentRule", NonStandardFieldKind.Json),
                new("GuildOverrides", NonStandardFieldKind.Json)
            ],
            [NonStandardEntityType.CharacterRace] =
            [
                new("PeopleType", NonStandardFieldKind.Json),
                new("Tags", NonStandardFieldKind.Json),
                new("Description", NonStandardFieldKind.Text),
                new("levelledAbilities", NonStandardFieldKind.Json),
                new("AdditionalInfo", NonStandardFieldKind.Text),
                new("Subtype", NonStandardFieldKind.Json),
                new("Buy-as", NonStandardFieldKind.Text),
                new("alignmentRule", NonStandardFieldKind.Json),
                new("GuildOverrides", NonStandardFieldKind.Json)
            ],
            [NonStandardEntityType.Ability] =
            [
                new("index", NonStandardFieldKind.Text),
                new("desc", NonStandardFieldKind.Text),
                new("cost", NonStandardFieldKind.Number),
                new("table", NonStandardFieldKind.Number),
                new("available", NonStandardFieldKind.Auto),
                new("canBuyMultiple", NonStandardFieldKind.Boolean),
                new("preReqs", NonStandardFieldKind.Json),
                new("maxAvailable", NonStandardFieldKind.Number)
            ],
            [NonStandardEntityType.Spell] =
            [
                new("level", NonStandardFieldKind.Number),
                new("colour", NonStandardFieldKind.Text),
                new("range", NonStandardFieldKind.Text),
                new("duration", NonStandardFieldKind.Text),
                new("gesture", NonStandardFieldKind.Text),
                new("verbal", NonStandardFieldKind.Text),
                new("description", NonStandardFieldKind.Text),
                new("notes", NonStandardFieldKind.Text),
                new("isAdvanced", NonStandardFieldKind.Boolean),
                new("Damage", NonStandardFieldKind.Json)
            ],
            [NonStandardEntityType.Miracle] =
            [
                new("power", NonStandardFieldKind.Number),
                new("description", NonStandardFieldKind.Text),
                new("verbal", NonStandardFieldKind.Text),
                new("range", NonStandardFieldKind.Text),
                new("duration", NonStandardFieldKind.Text),
                new("gesture", NonStandardFieldKind.Text),
                new("level", NonStandardFieldKind.Text),
                new("sphere", NonStandardFieldKind.Text),
                new("alignment", NonStandardFieldKind.Text),
                new("isAdvanced", NonStandardFieldKind.Boolean),
                new("Damage", NonStandardFieldKind.Json),
                new("Heal", NonStandardFieldKind.Json),
                new("preReqs", NonStandardFieldKind.Json)
            ],
            [NonStandardEntityType.Evocation] =
            [
                new("power", NonStandardFieldKind.Number),
                new("fields", NonStandardFieldKind.Json),
                new("description", NonStandardFieldKind.Text),
                new("range", NonStandardFieldKind.Text),
                new("duration", NonStandardFieldKind.Text),
                new("verbal", NonStandardFieldKind.Text),
                new("preReqs", NonStandardFieldKind.Json),
                new("isAdvanced", NonStandardFieldKind.Boolean),
                new("Damage", NonStandardFieldKind.Json),
                new("Heal", NonStandardFieldKind.Json)
            ]
        };

    private readonly ObservableCollection<NonStandardTypeOptionVm> _entityTypes = new();
    private readonly ObservableCollection<NonStandardTemplate> _baseTemplates = new();
    private readonly ObservableCollection<NonStandardFieldVm> _fields = new();
    private readonly ObservableCollection<string> _raceOptions = new();
    private readonly ObservableCollection<string> _classOptions = new();
    private readonly ObservableCollection<string> _racePeopleTypeOptions = new();
    private readonly ObservableCollection<string> _selectedRacePeopleTypes = new();
    private readonly ObservableCollection<string> _selectedRaceTags = new();
    private readonly ObservableCollection<RaceTagRowVm> _raceTagRows = new();
    private readonly ObservableCollection<RaceAbilityRowVm> _raceAbilityRows = new();
    private readonly ObservableCollection<string> _guildOverrideTypeOptions = new(GuildOverrideTypes);
    private readonly ObservableCollection<string> _selectedRaceGuildOverrideTypes = new();
    private readonly ObservableCollection<Alignment> _selectedRaceAlignments = new();
    private readonly ObservableCollection<RaceSubtypeOptionVm> _raceSubtypeOptions = new();
    private readonly ObservableCollection<RaceSubtypeCopyVm> _raceSubtypeCopies = new();
    private readonly ObservableCollection<CharacterAssignmentOptionVm> _assignableCharacters = new();
    private readonly ObservableCollection<CustomLifeScalePointVm> _customLifeScaleRows = new();
    private readonly ObservableCollection<RaceLifeScaleClassEntryVm> _raceLifeScaleEntries = new();
    private readonly List<string> _raceTagOptions = new();

    private readonly Dictionary<string, EvolutionService.AbilityResult> _raceAbilityLookup =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SpecialisationRecord> _specialisationLookup =
        new(StringComparer.OrdinalIgnoreCase);

    private bool _initialized;
    private bool _suppressTypeReload;
    private bool _isBusy;
    private bool _raceLookupsLoaded;
    private bool _specialisationLookupLoaded;
    private bool _syncingLifeScaleRows;
    private string _name = string.Empty;
    private string _saveStatus = string.Empty;
    private string _lifeScalePointsJson = string.Empty;
    private bool _isLifeScaleExpanded;
    private bool _isRaceAlignmentCardExpanded;
    private bool _includeRaceAlignmentRule;
    private string _raceSubtypeKey = string.Empty;
    private string _raceSubtypeDisplayName = string.Empty;
    private string _raceSubtypeDescription = string.Empty;
    private string _raceSubtypeSelectionMode = "SingleOptional";
    private string _raceSubtypeOptionsSource = string.Empty;
    private string _raceSubtypeAbilityMapKey = string.Empty;
    private string _raceBuyAsText = string.Empty;
    private string _raceTagInput = string.Empty;
    private string? _raceTagSelectedValue;

    private NonStandardTypeOptionVm? _selectedEntityType;
    private NonStandardTemplate? _selectedBaseTemplate;
    private string? _selectedLifeScaleTargetRace;
    private string? _selectedLifeScaleTargetClass;
    private string? _selectedLifeScaleSourceRace;
    private string? _selectedLifeScaleSourceClass;
    private RaceSubtypeOptionVm? _selectedRaceSubtypeOption;

    private EvolutionService.AbilityResult? _previewAbility;
    private SpellService.SpellRaw? _previewSpell;
    private MiracleService.MiracRaw? _previewMiracle;
    private DruidEvocationService.EvocRaw? _previewEvocation;
    private string _jsonPreviewText = string.Empty;
    private CharacterAssignmentOptionVm? _selectedAssignedCharacter;

    public ObservableCollection<NonStandardTypeOptionVm> EntityTypes => _entityTypes;
    public ObservableCollection<NonStandardTemplate> BaseTemplates => _baseTemplates;
    public ObservableCollection<NonStandardFieldVm> Fields => _fields;
    public ObservableCollection<string> LifeScaleRaceOptions => _raceOptions;
    public ObservableCollection<string> LifeScaleClassOptions => _classOptions;
    public ObservableCollection<string> RacePeopleTypeOptions => _racePeopleTypeOptions;
    public ObservableCollection<string> SelectedRacePeopleTypes => _selectedRacePeopleTypes;
    public ObservableCollection<string> SelectedRaceTags => _selectedRaceTags;
    public ObservableCollection<RaceTagRowVm> RaceTagRows => _raceTagRows;
    public ObservableCollection<RaceAbilityRowVm> RaceAbilityRows => _raceAbilityRows;
    public ObservableCollection<string> GuildOverrideTypeOptions => _guildOverrideTypeOptions;
    public ObservableCollection<string> SelectedRaceGuildOverrideTypes => _selectedRaceGuildOverrideTypes;
    public ObservableCollection<Alignment> SelectedRaceAlignments => _selectedRaceAlignments;
    public ObservableCollection<RaceSubtypeOptionVm> RaceSubtypeOptions => _raceSubtypeOptions;
    public RaceSubtypeOptionVm? SelectedRaceSubtypeOption
    {
        get => _selectedRaceSubtypeOption;
        set
        {
            if (!Set(ref _selectedRaceSubtypeOption, value))
                return;

            ReindexSubtypeOptions();
        }
    }

    public Dictionary<string, string> RaceTagDictionary
        => _raceTagOptions
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(tag => tag, tag => tag, StringComparer.OrdinalIgnoreCase);
    public ObservableCollection<RaceSubtypeCopyVm> RaceSubtypeCopies => _raceSubtypeCopies;
    public ObservableCollection<CharacterAssignmentOptionVm> AssignableCharacters => _assignableCharacters;
    public ObservableCollection<CustomLifeScalePointVm> CustomLifeScaleRows => _customLifeScaleRows;
    public ObservableCollection<RaceLifeScaleClassEntryVm> RaceLifeScaleEntries => _raceLifeScaleEntries;

    public NonStandardCreateVm()
    {
        _selectedRacePeopleTypes.CollectionChanged += (_, _) => OnRaceStructuredDataChanged();
        _selectedRaceTags.CollectionChanged += (_, _) =>
        {
            Raise(nameof(HasRaceTags));
            Raise(nameof(RaceTagCountLabel));
            OnRaceStructuredDataChanged();
            RebuildRaceTagRows();
        };
        _selectedRaceGuildOverrideTypes.CollectionChanged += (_, _) => OnRaceStructuredDataChanged();
        _selectedRaceAlignments.CollectionChanged += (_, _) => OnRaceStructuredDataChanged();
        _raceAbilityRows.CollectionChanged += OnRaceAbilityRowsChanged;
        _raceSubtypeCopies.CollectionChanged += OnRaceSubtypeCopiesChanged;
        InitializeRaceAlignmentDefaults();
    }

    public NonStandardTypeOptionVm? SelectedEntityType
    {
        get => _selectedEntityType;
        set
        {
            if (!Set(ref _selectedEntityType, value))
                return;

            Raise(nameof(RequiresLifeScale));
            Raise(nameof(ShowClassLifeScaleSelectors));
            Raise(nameof(ShowRaceLifeScaleSelectors));
            Raise(nameof(IsRaceType));
            Raise(nameof(RequiresCharacterAssignment));
            Raise(nameof(ShowStructuredRaceEditor));
            Raise(nameof(HasSubtypeEditorData));
            Raise(nameof(CanSave));

            if (!_suppressTypeReload)
                _ = ReloadForSelectedTypeAsync(preferredTemplateName: null);
        }
    }

    public NonStandardTemplate? SelectedBaseTemplate
    {
        get => _selectedBaseTemplate;
        set
        {
            if (!Set(ref _selectedBaseTemplate, value))
                return;

            ApplySelectedBaseTemplate();
        }
    }

    public string Name
    {
        get => _name;
        set
        {
            if (!Set(ref _name, value ?? string.Empty))
                return;

            if (IsRaceType)
                UpdateSubtypeMapKey();

            Raise(nameof(CanSave));
            UpdatePreview();
        }
    }

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

    public bool IsRaceType => CurrentType == NonStandardEntityType.CharacterRace;
    public bool RequiresCharacterAssignment => CurrentType is NonStandardEntityType.Ability
        or NonStandardEntityType.Spell
        or NonStandardEntityType.Miracle
        or NonStandardEntityType.Evocation;

    public CharacterAssignmentOptionVm? SelectedAssignedCharacter
    {
        get => _selectedAssignedCharacter;
        set
        {
            if (!Set(ref _selectedAssignedCharacter, value))
                return;

            Raise(nameof(CanSave));
        }
    }

    public bool ShowStructuredRaceEditor => IsRaceType;

    public bool HasRaceTags => SelectedRaceTags.Count > 0;
    public string RaceTagCountLabel => $"{SelectedRaceTags.Count}/3";

    public string RaceBuyAsText
    {
        get => _raceBuyAsText;
        set
        {
            if (!Set(ref _raceBuyAsText, value ?? string.Empty))
                return;

            OnRaceStructuredDataChanged();
        }
    }

    public string RaceTagInput
    {
        get => _raceTagInput;
        set => Set(ref _raceTagInput, value ?? string.Empty);
    }

    public string? RaceTagSelectedValue
    {
        get => _raceTagSelectedValue;
        set
        {
            if (!Set(ref _raceTagSelectedValue, value))
                return;

            var selected = (value ?? string.Empty).Trim();
            if (selected.Length == 0)
                return;

            AddRaceTagValue(selected);
            _raceTagSelectedValue = null;
            Raise(nameof(RaceTagSelectedValue));
        }
    }

    public bool HasSubtypeEditorData
        => !string.IsNullOrWhiteSpace((_raceSubtypeKey ?? string.Empty).Trim())
            || !string.IsNullOrWhiteSpace((_raceSubtypeDisplayName ?? string.Empty).Trim())
            || !string.IsNullOrWhiteSpace((_raceSubtypeDescription ?? string.Empty).Trim())
            || !string.IsNullOrWhiteSpace((_raceSubtypeOptionsSource ?? string.Empty).Trim())
            || !string.IsNullOrWhiteSpace((_raceSubtypeAbilityMapKey ?? string.Empty).Trim())
            || RaceSubtypeOptions.Count > 0
            || RaceSubtypeCopies.Count > 0;

    public string RaceAlignmentChevronText => IsRaceAlignmentCardExpanded ? "▴" : "▾";

    public bool IsRaceAlignmentCardExpanded
    {
        get => _isRaceAlignmentCardExpanded;
        set
        {
            if (!Set(ref _isRaceAlignmentCardExpanded, value))
                return;

            Raise(nameof(RaceAlignmentChevronText));
        }
    }

    public bool IncludeRaceAlignmentRule
    {
        get => _includeRaceAlignmentRule;
        set
        {
            if (!Set(ref _includeRaceAlignmentRule, value))
                return;

            OnRaceStructuredDataChanged();
        }
    }

    public string RaceSubtypeKey
    {
        get => _raceSubtypeKey;
        set
        {
            if (!Set(ref _raceSubtypeKey, value ?? string.Empty))
                return;

            OnRaceStructuredDataChanged();
            Raise(nameof(HasSubtypeEditorData));
        }
    }

    public string RaceSubtypeDisplayName
    {
        get => _raceSubtypeDisplayName;
        set
        {
            if (!Set(ref _raceSubtypeDisplayName, value ?? string.Empty))
                return;

            OnRaceStructuredDataChanged();
            Raise(nameof(HasSubtypeEditorData));
        }
    }

    public string RaceSubtypeDescription
    {
        get => _raceSubtypeDescription;
        set
        {
            if (!Set(ref _raceSubtypeDescription, value ?? string.Empty))
                return;

            OnRaceStructuredDataChanged();
            Raise(nameof(HasSubtypeEditorData));
        }
    }

    public string RaceSubtypeSelectionMode
    {
        get => _raceSubtypeSelectionMode;
        set
        {
            if (!Set(ref _raceSubtypeSelectionMode, value ?? "SingleOptional"))
                return;

            OnRaceStructuredDataChanged();
            Raise(nameof(HasSubtypeEditorData));
        }
    }

    public string RaceSubtypeOptionsSource
    {
        get => _raceSubtypeOptionsSource;
        set
        {
            if (!Set(ref _raceSubtypeOptionsSource, value ?? string.Empty))
                return;

            _ = RefreshSubtypeOptionsAsync();
            OnRaceStructuredDataChanged();
            Raise(nameof(HasSubtypeEditorData));
        }
    }

    public string RaceSubtypeAbilityMapKey
    {
        get => _raceSubtypeAbilityMapKey;
        set
        {
            if (!Set(ref _raceSubtypeAbilityMapKey, value ?? string.Empty))
                return;

            _ = RefreshSubtypeOptionsAsync();
            OnRaceStructuredDataChanged();
            Raise(nameof(HasSubtypeEditorData));
        }
    }

    public bool RequiresLifeScale => CurrentType is NonStandardEntityType.CharacterClass or NonStandardEntityType.CharacterRace;
    public bool ShowClassLifeScaleSelectors => CurrentType == NonStandardEntityType.CharacterClass;
    public bool ShowRaceLifeScaleSelectors => CurrentType == NonStandardEntityType.CharacterRace;

    public string? SelectedLifeScaleTargetRace
    {
        get => _selectedLifeScaleTargetRace;
        set
        {
            if (!Set(ref _selectedLifeScaleTargetRace, value))
                return;

            Raise(nameof(CanSave));
            _ = RefreshLifeScaleFromSelectionAsync();
        }
    }

    public string? SelectedLifeScaleTargetClass
    {
        get => _selectedLifeScaleTargetClass;
        set
        {
            if (!Set(ref _selectedLifeScaleTargetClass, value))
                return;

            Raise(nameof(CanSave));
            Raise(nameof(SelectedLifeScaleTargetClassDisplay));
            _ = RefreshLifeScaleFromSelectionAsync();
        }
    }

    public string? SelectedLifeScaleSourceRace
    {
        get => _selectedLifeScaleSourceRace;
        set
        {
            if (!Set(ref _selectedLifeScaleSourceRace, value))
                return;

            Raise(nameof(SelectedLifeScaleSourceRaceDisplay));
            _ = RefreshLifeScaleFromSelectionAsync();
        }
    }

    public string? SelectedLifeScaleSourceClass
    {
        get => _selectedLifeScaleSourceClass;
        set
        {
            if (!Set(ref _selectedLifeScaleSourceClass, value))
                return;

            _ = RefreshLifeScaleFromSelectionAsync();
        }
    }

    public string LifeScalePointsJson
    {
        get => _lifeScalePointsJson;
        set
        {
            if (!Set(ref _lifeScalePointsJson, value ?? string.Empty))
                return;

            Raise(nameof(LifeScaleCollapsedSummary));
            Raise(nameof(LifeScaleExpandedSummary));
            Raise(nameof(CanSave));

            if (!_syncingLifeScaleRows)
                SyncLifeScaleRowsFromJson();
        }
    }

    public string SelectedLifeScaleTargetClassDisplay
        => string.IsNullOrWhiteSpace((SelectedLifeScaleTargetClass ?? string.Empty).Trim())
            ? "Select playable class"
            : SelectedLifeScaleTargetClass!;

    public string SelectedLifeScaleSourceRaceDisplay
        => string.IsNullOrWhiteSpace((SelectedLifeScaleSourceRace ?? string.Empty).Trim())
            ? "Select source race"
            : SelectedLifeScaleSourceRace!;

    public bool IsLifeScaleExpanded
    {
        get => _isLifeScaleExpanded;
        set
        {
            if (!Set(ref _isLifeScaleExpanded, value))
                return;

            Raise(nameof(LifeScaleChevronText));
        }
    }

    public string LifeScaleChevronText => IsLifeScaleExpanded ? "▴" : "▾";

    public string LifeScaleCollapsedSummary
    {
        get
        {
            if (!TryParseLifeScalePoints(LifeScalePointsJson, out var points) || points.Count == 0)
                return "No life-scale points loaded.";

            var last = points[Math.Min(points.Count, 8) - 1];
            return $"Level 8: {Math.Max(0, last.Body)}/{Math.Max(0, last.Loc)}";
        }
    }

    public string LifeScaleExpandedSummary
    {
        get
        {
            if (!TryParseLifeScalePoints(LifeScalePointsJson, out var points) || points.Count == 0)
                return string.Empty;

            var lines = new List<string>();
            for (var index = 0; index < points.Count; index++)
            {
                var point = points[index];
                lines.Add($"L{index + 1}: {Math.Max(0, point.Body)}/{Math.Max(0, point.Loc)}");
            }

            return string.Join("  |  ", lines);
        }
    }

    public EvolutionService.AbilityResult? PreviewAbility
    {
        get => _previewAbility;
        private set => Set(ref _previewAbility, value);
    }

    public SpellService.SpellRaw? PreviewSpell
    {
        get => _previewSpell;
        private set => Set(ref _previewSpell, value);
    }

    public MiracleService.MiracRaw? PreviewMiracle
    {
        get => _previewMiracle;
        private set => Set(ref _previewMiracle, value);
    }

    public DruidEvocationService.EvocRaw? PreviewEvocation
    {
        get => _previewEvocation;
        private set => Set(ref _previewEvocation, value);
    }

    public string JsonPreviewText
    {
        get => _jsonPreviewText;
        private set => Set(ref _jsonPreviewText, value ?? string.Empty);
    }

    public bool ShowAbilityPreview => PreviewAbility != null;
    public bool ShowSpellPreview => PreviewSpell != null;
    public bool ShowMiraclePreview => PreviewMiracle != null;
    public bool ShowEvocationPreview => PreviewEvocation != null;
    public bool ShowJsonPreview => !string.IsNullOrWhiteSpace((JsonPreviewText ?? string.Empty).Trim());

    public bool CanSave
    {
        get
        {
            if (IsBusy)
                return false;

            if (string.IsNullOrWhiteSpace((Name ?? string.Empty).Trim()))
                return false;

            if (!RequiresLifeScale)
                return !RequiresCharacterAssignment || SelectedAssignedCharacter != null;

            if (CurrentType == NonStandardEntityType.CharacterClass)
            {
                if (string.IsNullOrWhiteSpace((SelectedLifeScaleTargetRace ?? string.Empty).Trim()))
                    return false;

                if (!TryParseLifeScalePoints(LifeScalePointsJson, out var classPoints) || classPoints.Count < 8)
                    return false;
            }

            if (CurrentType == NonStandardEntityType.CharacterRace)
            {
                if (_raceLifeScaleEntries.Count == 0)
                    return false;

                if (_raceLifeScaleEntries.Any(entry =>
                    string.IsNullOrWhiteSpace((entry.ClassName ?? string.Empty).Trim())))
                    return false;
            }

            if (CurrentType != NonStandardEntityType.CharacterRace
                && (!TryParseLifeScalePoints(LifeScalePointsJson, out var points) || points.Count < 8))
                return false;

            return !RequiresCharacterAssignment || SelectedAssignedCharacter != null;
        }
    }

    private NonStandardEntityType CurrentType => SelectedEntityType?.EntityType ?? NonStandardEntityType.CharacterClass;

    public async Task InitializeAsync(NonStandardEntityType? preferredType = null)
    {
        if (_initialized)
        {
            if (preferredType.HasValue)
                await SelectEntityTypeAsync(preferredType.Value);

            return;
        }

        _initialized = true;

        _entityTypes.Clear();
        foreach (var type in Enum.GetValues<NonStandardEntityType>())
            _entityTypes.Add(new NonStandardTypeOptionVm(type, FormatEntityLabel(type)));

        SelectedEntityType = _entityTypes.FirstOrDefault();

        await LoadLifeScaleLookupsAsync();
        await LoadAssignableCharactersAsync();
        await EnsureRaceEditorLookupsAsync();
        await ReloadForSelectedTypeAsync(preferredTemplateName: null);

        if (preferredType.HasValue)
            await SelectEntityTypeAsync(preferredType.Value);
    }

    public async Task SelectEntityTypeAsync(NonStandardEntityType entityType)
    {
        if (!_initialized)
        {
            await InitializeAsync(entityType);
            return;
        }

        var typeOption = _entityTypes.FirstOrDefault(option => option.EntityType == entityType);
        if (typeOption == null)
            return;

        if (ReferenceEquals(SelectedEntityType, typeOption))
            return;

        _suppressTypeReload = true;
        try
        {
            SelectedEntityType = typeOption;
        }
        finally
        {
            _suppressTypeReload = false;
        }

        await ReloadForSelectedTypeAsync(preferredTemplateName: null);
    }

    public async Task SearchBaseAsync(INavigation navigation)
    {
        if (navigation == null || BaseTemplates.Count == 0)
            return;

        var options = BaseTemplates
            .Select(template => new NonStandardSearchOption(
                Title: template.Name,
                Subtitle: template.Subtitle,
                Value: template.Name))
            .ToList();

        var selected = await NonStandardSearchPage.PickAsync(navigation, "Select Base", options);
        if (selected == null)
            return;

        var template = BaseTemplates.FirstOrDefault(item =>
            string.Equals(item.Name, selected.Value, StringComparison.OrdinalIgnoreCase));
        if (template != null)
            SelectedBaseTemplate = template;
    }

    public async Task SearchFieldAsync(INavigation navigation, NonStandardFieldVm? field)
    {
        if (navigation == null || field == null)
            return;

        IReadOnlyList<NonStandardFieldAlternative> alternatives;
        if (CurrentType == NonStandardEntityType.CharacterClass
            && string.Equals(NormalizeFieldKey(field.Key), NormalizeFieldKey("Buy as"), StringComparison.Ordinal))
        {
            alternatives = await NonStandardContentService.GetArrayEntryAlternativesAsync(CurrentType, field.Key);
        }
        else
        {
            alternatives = await NonStandardContentService.GetFieldAlternativesAsync(CurrentType, field.Key);
        }

        if (alternatives.Count == 0)
            return;

        var options = alternatives
            .Select(option => new NonStandardSearchOption(
                Title: option.DisplayText,
                Subtitle: option.SourceName,
                Value: option.ValueJson))
            .ToList();

        var selected = await NonStandardSearchPage.PickAsync(navigation, $"Replace {field.Label}", options);
        if (selected == null)
            return;

        field.Value = ConvertAlternativeValue(selected.Value, field.Kind);
    }

    public async Task RefreshLookupsOnAppearAsync()
    {
        if (!_initialized)
            return;

        await LoadLifeScaleLookupsAsync();
        await LoadAssignableCharactersAsync();
        await EnsureRaceEditorLookupsAsync();
        EnsureLifeScaleSelectionsAreValid();
        await RefreshSubtypeOptionsAsync();
        Raise(nameof(CanSave));
    }

    public void ToggleRaceAlignmentExpanded()
    {
        IsRaceAlignmentCardExpanded = !IsRaceAlignmentCardExpanded;
    }

    public async Task SearchRaceAbilityAsync(INavigation navigation)
    {
        if (!IsRaceType || navigation == null)
            return;

        await EnsureRaceEditorLookupsAsync();
        if (_raceAbilityLookup.Count == 0)
            return;

        var options = _raceAbilityLookup.Values
            .OrderBy(entry => entry.Table)
            .ThenBy(entry => entry.Index, StringComparer.OrdinalIgnoreCase)
            .Select(entry => new NonStandardSearchOption(
                Title: entry.Index,
                Subtitle: $"Table {entry.Table}",
                Value: entry.Index))
            .ToList();

        var selected = await NonStandardSearchPage.PickAsync(navigation, "Select Ability", options);
        if (selected == null)
            return;

        AddRaceAbilityRow(level: 1, selected.Value, type: "Static", countText: string.Empty, subtypeCopyId: null);
    }

    public async Task SearchRaceTagsAsync(INavigation navigation)
    {
        if (!IsRaceType || navigation == null)
            return;

        await EnsureRaceEditorLookupsAsync();
        var options = _raceTagOptions
            .Select(tag => new NonStandardTagSearchOption(tag, tag))
            .ToList();

        var selected = await NonStandardTagSearchPage.PickAsync(
            navigation,
            "Select race tags",
            options,
            SelectedRaceTags.ToList(),
            3);

        ReplaceItems(_selectedRaceTags, selected);
        Raise(nameof(RaceTagDictionary));
        RebuildRaceTagRows();
        Raise(nameof(HasRaceTags));
        OnRaceStructuredDataChanged();
    }

    public void AddRaceTag()
    {
        var value = (RaceTagInput ?? string.Empty).Trim();
        if (value.Length == 0)
            return;

        AddRaceTagValue(value);
        RaceTagInput = string.Empty;
    }

    public void RemoveRaceTag(string? tag)
    {
        var value = (tag ?? string.Empty).Trim();
        if (value.Length == 0)
            return;

        var existing = _selectedRaceTags.FirstOrDefault(item => item.Equals(value, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            _selectedRaceTags.Remove(existing);

        RebuildRaceTagRows();
    }

    public async Task SearchLifeScaleTargetClassAsync(INavigation navigation)
    {
        if (navigation == null || !ShowRaceLifeScaleSelectors)
            return;

        var selected = await NonStandardClassSearchPage.PickAsync(navigation, SelectedLifeScaleTargetClass);
        if (!string.IsNullOrWhiteSpace(selected))
            SelectedLifeScaleTargetClass = selected;
    }

    public async Task AddRaceLifeScaleEntryAsync(INavigation navigation)
    {
        if (navigation == null || !IsRaceType)
            return;

        var selected = await NonStandardClassSearchPage.PickAsync(navigation, null);
        if (string.IsNullOrWhiteSpace(selected))
            return;

        var className = selected.Trim();
        if (_raceLifeScaleEntries.Any(entry =>
            string.Equals(entry.ClassName, className, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var entry = new RaceLifeScaleClassEntryVm { ClassName = className };
        _raceLifeScaleEntries.Add(entry);
        ReindexRaceLifeScaleEntries();
        Raise(nameof(CanSave));

        await RefreshRaceLifeScaleEntryAsync(entry);
    }

    public async Task SearchRaceLifeScaleClassAsync(INavigation navigation, RaceLifeScaleClassEntryVm? entry)
    {
        if (navigation == null || entry == null || !IsRaceType)
            return;

        var selected = await NonStandardClassSearchPage.PickAsync(navigation, entry.ClassName);
        if (string.IsNullOrWhiteSpace(selected))
            return;

        entry.ClassName = selected.Trim();
        Raise(nameof(CanSave));
    }

    public void RemoveRaceLifeScaleEntry(RaceLifeScaleClassEntryVm? entry)
    {
        if (entry == null)
            return;

        _raceLifeScaleEntries.Remove(entry);
        ReindexRaceLifeScaleEntries();
        Raise(nameof(CanSave));
    }

    public void ToggleRaceLifeScaleEntryEditor(RaceLifeScaleClassEntryVm? entry)
    {
        if (entry == null)
            return;

        entry.IsEditorExpanded = !entry.IsEditorExpanded;
    }

    private void ReindexRaceLifeScaleEntries()
    {
        for (var i = 0; i < _raceLifeScaleEntries.Count; i++)
            _raceLifeScaleEntries[i].RowBackgroundHex = i % 2 == 0 ? "#FFFFFF" : "#F8F8F8";
    }

    private async Task RefreshRaceLifeScaleEntryAsync(RaceLifeScaleClassEntryVm entry)
    {
        var className = (entry.ClassName ?? string.Empty).Trim();
        if (className.Length == 0)
            return;

        var sourceRace = (SelectedLifeScaleSourceRace ?? string.Empty).Trim();
        if (sourceRace.Length == 0)
            return;

        var points = await LifeScalesService.GetLifeScaleAsync(sourceRace, className);
        if (points.Count == 0)
            return;

        entry.ApplyPoints(points);
    }

    public async Task SearchLifeScaleSourceRaceAsync(INavigation navigation)
    {
        if (navigation == null || _raceOptions.Count == 0)
            return;

        var options = _raceOptions
            .Select(race => new NonStandardSearchOption(race, "Race", race))
            .ToList();

        var selected = await NonStandardSearchPage.PickAsync(navigation, "Select source race", options);
        if (selected == null)
            return;

        SelectedLifeScaleSourceRace = (selected.Value ?? string.Empty).Trim();
    }

    public void RemoveRaceAbilityRow(RaceAbilityRowVm? row)
    {
        if (row == null)
            return;

        if (row.SubtypeCopyId != null)
        {
            var copy = RaceSubtypeCopies.FirstOrDefault(item => item.Id == row.SubtypeCopyId.Value);
            copy?.Abilities.Remove(row);
            return;
        }

        RaceAbilityRows.Remove(row);
    }

    public void ApplyRaceAbilityRowEdit(RaceAbilityRowVm? row, int level, string? abilityName, string? abilityType, string? countText)
    {
        if (row == null)
            return;

        row.Level = Math.Clamp(level, 1, 8);
        row.AbilityName = (abilityName ?? string.Empty).Trim();
        row.AbilityType = (abilityType ?? string.Empty).Trim();
        row.CountText = (countText ?? string.Empty).Trim();
        row.RefreshSummary();
        OnRaceStructuredDataChanged();
    }

    public string BuildRaceAbilityInfoText(RaceAbilityRowVm? row)
    {
        if (row == null)
            return "No ability selected.";

        var name = (row.AbilityName ?? string.Empty).Trim();
        if (name.Length == 0)
            return "No ability selected.";

        if (_raceAbilityLookup.TryGetValue(name, out var known))
        {
            var details = new List<string>
            {
                known.Index,
                $"Type: {(string.IsNullOrWhiteSpace(row.AbilityType) ? "Static" : row.AbilityType)}",
                $"Table: {known.Table}",
                $"Cost: {known.Cost}"
            };

            if (known.Description?.Trim().Length > 0)
                details.Add(known.Description.Trim());

            return string.Join(Environment.NewLine, details);
        }

        var fallback = new List<string>
        {
            name,
            $"Type: {(string.IsNullOrWhiteSpace(row.AbilityType) ? "Static" : row.AbilityType)}"
        };

        if (!string.IsNullOrWhiteSpace((row.CountText ?? string.Empty).Trim()))
            fallback.Add($"Count: {row.CountText.Trim()}");

        return string.Join(Environment.NewLine, fallback);
    }

    public void AddSubtypeCopyFromOption(RaceSubtypeOptionVm? option)
    {
        if (option == null)
            return;

        var baseName = (option.Name ?? string.Empty).Trim();
        var nextName = BuildNextSubtypeCopyName(baseName.Length == 0 ? "Subtype option" : baseName);
        var copy = new RaceSubtypeCopyVm
        {
            Name = nextName,
            Description = option.Description
        };

        foreach (var source in option.Abilities)
            copy.Abilities.Add(CloneRaceAbilityRow(source, copy.Id));

        RaceSubtypeCopies.Add(copy);
        Raise(nameof(HasSubtypeEditorData));
        OnRaceStructuredDataChanged();
    }

    public void AddBlankSubtypeCopy()
    {
        var copy = new RaceSubtypeCopyVm
        {
            Name = BuildNextSubtypeCopyName("Custom subtype"),
            Description = string.Empty
        };

        RaceSubtypeCopies.Add(copy);
        Raise(nameof(HasSubtypeEditorData));
        OnRaceStructuredDataChanged();
    }

    public void RemoveSubtypeCopy(RaceSubtypeCopyVm? copy)
    {
        if (copy == null)
            return;

        RaceSubtypeCopies.Remove(copy);
        Raise(nameof(HasSubtypeEditorData));
        OnRaceStructuredDataChanged();
    }

    public async Task SearchSubtypeCopyAbilityAsync(INavigation navigation, RaceSubtypeCopyVm? copy)
    {
        if (!IsRaceType || navigation == null || copy == null)
            return;

        await EnsureRaceEditorLookupsAsync();
        if (_raceAbilityLookup.Count == 0)
            return;

        var options = _raceAbilityLookup.Values
            .OrderBy(entry => entry.Table)
            .ThenBy(entry => entry.Index, StringComparer.OrdinalIgnoreCase)
            .Select(entry => new NonStandardSearchOption(
                Title: entry.Index,
                Subtitle: $"Table {entry.Table}",
                Value: entry.Index))
            .ToList();

        var selected = await NonStandardSearchPage.PickAsync(navigation, "Select Ability", options);
        if (selected == null)
            return;

        var row = CreateRaceAbilityRow(level: 1, abilityName: selected.Value, type: "Static", countText: string.Empty, copy.Id);
        copy.Abilities.Add(row);
        OnRaceStructuredDataChanged();
    }

    public async Task SaveAsync()
    {
        if (!CanSave)
            throw new InvalidOperationException("Please complete required fields before saving.");

        IsBusy = true;
        SaveStatus = string.Empty;
        try
        {
            var payloadJson = BuildPayloadJson();
            var request = new NonStandardSaveRequest
            {
                EntityType = CurrentType,
                Name = (Name ?? string.Empty).Trim(),
                DataJson = payloadJson
            };

            if (RequiresCharacterAssignment)
            {
                var selected = SelectedAssignedCharacter
                    ?? throw new InvalidOperationException("Select a character before saving.");

                request.AssignedCharacterId = selected.Id;
                request.AssignedCharacterName = selected.Name;
                request.AssignedCharacterPlayerName = selected.PlayerName;
            }

            if (RequiresLifeScale)
            {
                if (CurrentType == NonStandardEntityType.CharacterClass)
                {
                    if (!TryParseLifeScalePoints(LifeScalePointsJson, out var points) || points.Count < 8)
                        throw new InvalidOperationException("Life-scale must contain 8 levels.");

                    request.LifeScalePoints = points;
                    request.LifeScaleRaceName = (SelectedLifeScaleTargetRace ?? string.Empty).Trim();
                }
                else if (CurrentType == NonStandardEntityType.CharacterRace)
                {
                    request.RaceLifeScaleEntries = _raceLifeScaleEntries
                        .Where(entry => !string.IsNullOrWhiteSpace((entry.ClassName ?? string.Empty).Trim()))
                        .Select(entry => new RaceLifeScaleClassEntry
                        {
                            ClassName = entry.ClassName.Trim(),
                            Points = entry.ToPoints()
                        })
                        .ToList();
                }
            }

            await NonStandardContentService.SaveAsync(request);
            SaveStatus = $"Saved {FormatEntityLabel(CurrentType)} '{request.Name}' as non-standard.";

            await ReloadForSelectedTypeAsync(request.Name);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void ToggleLifeScaleExpanded()
    {
        IsLifeScaleExpanded = !IsLifeScaleExpanded;
    }

    public async Task LoadFromWalletEntryAsync(NonStandardWalletEntry? entry)
    {
        if (entry == null)
            return;

        await InitializeAsync(entry.EntityType);
        await SelectEntityTypeAsync(entry.EntityType);
        await ReloadForSelectedTypeAsync(entry.Name);
        BuildFields(entry.EntityType, entry.DataJson);
        ApplyAssignedCharacterFromPayload(entry.DataJson);
        Name = entry.Name;
        SaveStatus = string.Empty;
        UpdatePreview();

        await LoadExistingLifeScaleSelectionAsync(entry.EntityType, entry.Name);
        Raise(nameof(CanSave));
    }

    private void ApplyAssignedCharacterFromPayload(string? payloadJson)
    {
        var payload = ParseObjectProperties(payloadJson);
        if (!payload.TryGetValue(NormalizeFieldKey("assignedCharacterId"), out var property)
            || property.Value.ValueKind != JsonValueKind.String)
        {
            SelectDefaultAssignedCharacter();
            return;
        }

        var id = (property.Value.GetString() ?? string.Empty).Trim();
        if (id.Length == 0)
        {
            SelectDefaultAssignedCharacter();
            return;
        }

        SelectedAssignedCharacter = AssignableCharacters.FirstOrDefault(option =>
            option.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    private async Task ReloadForSelectedTypeAsync(string? preferredTemplateName)
    {
        if (!_initialized)
            return;

        var type = CurrentType;
        var templates = await NonStandardContentService.GetTemplatesAsync(type);
        await EnsureRaceEditorLookupsAsync();

        ReplaceItems(_baseTemplates, templates);

        var preferredName = (preferredTemplateName ?? SelectedBaseTemplate?.Name ?? string.Empty).Trim();
        var selected = templates.FirstOrDefault(template =>
            string.Equals(template.Name, preferredName, StringComparison.OrdinalIgnoreCase))
            ?? templates.FirstOrDefault();

        if (selected != null)
        {
            SelectedBaseTemplate = selected;
        }
        else
        {
            BuildFields(type, null);
            Name = string.Empty;
        }

        await ConfigureLifeScaleDefaultsAsync();
        SelectDefaultAssignedCharacter();
        Raise(nameof(CanSave));
    }

    private async Task LoadLifeScaleLookupsAsync()
    {
        var races = await PeopleService.GetAllAsync();
        var classes = await ClassService.GetAllAsync();

        ReplaceItems(_raceOptions, races.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
        ReplaceItems(_classOptions, classes.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
    }

    private Task LoadAssignableCharactersAsync()
    {
        var characters = LiteDbService.GetCharacters()
            .OrderBy(character => character.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(character => character.PlayerName, StringComparer.OrdinalIgnoreCase)
            .Select(character => new CharacterAssignmentOptionVm(
                character.Id,
                character.Name,
                character.PlayerName,
                character.Class))
            .ToList();

        ReplaceItems(_assignableCharacters, characters);
        SelectDefaultAssignedCharacter();
        Raise(nameof(CanSave));
        return Task.CompletedTask;
    }

    private void SelectDefaultAssignedCharacter()
    {
        if (SelectedAssignedCharacter != null
            && AssignableCharacters.Any(option => option.Id.Equals(SelectedAssignedCharacter.Id, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        SelectedAssignedCharacter = AssignableCharacters.FirstOrDefault();
    }

    private async Task ConfigureLifeScaleDefaultsAsync()
    {
        if (!RequiresLifeScale)
            return;

        if (CurrentType == NonStandardEntityType.CharacterClass)
        {
            SelectedLifeScaleTargetRace ??= _raceOptions.FirstOrDefault();
            SelectedLifeScaleSourceClass ??= (SelectedBaseTemplate?.Name ?? _classOptions.FirstOrDefault());
            await RefreshLifeScaleFromSelectionAsync();
            SyncLifeScaleRowsFromJson();
        }
        else if (CurrentType == NonStandardEntityType.CharacterRace)
        {
            SelectedLifeScaleSourceRace ??= (SelectedBaseTemplate?.Name ?? _raceOptions.FirstOrDefault());
        }
    }

    private async Task RefreshLifeScaleFromSelectionAsync()
    {
        if (!RequiresLifeScale)
            return;

        string sourceRace;
        string sourceClass;

        if (CurrentType == NonStandardEntityType.CharacterClass)
        {
            sourceRace = (SelectedLifeScaleTargetRace ?? string.Empty).Trim();
            sourceClass = (SelectedLifeScaleSourceClass ?? string.Empty).Trim();
        }
        else
        {
            sourceRace = (SelectedLifeScaleSourceRace ?? string.Empty).Trim();
            sourceClass = (SelectedLifeScaleTargetClass ?? string.Empty).Trim();
        }

        if (sourceRace.Length == 0 || sourceClass.Length == 0)
            return;

        var points = await LifeScalesService.GetLifeScaleAsync(sourceRace, sourceClass);
        if (points.Count == 0)
            return;

        var tuples = points.Select(point => new[] { point.Body, point.Loc }).ToList();
        _syncingLifeScaleRows = true;
        try
        {
            LifeScalePointsJson = JsonSerializer.Serialize(tuples, PrettyJson);
        }
        finally
        {
            _syncingLifeScaleRows = false;
        }

        SyncLifeScaleRowsFromJson();
    }

    private async Task LoadExistingLifeScaleSelectionAsync(NonStandardEntityType entityType, string name)
    {
        if (entityType is not (NonStandardEntityType.CharacterClass or NonStandardEntityType.CharacterRace))
            return;

        var token = (name ?? string.Empty).Trim();
        if (token.Length == 0)
            return;

        var allLifeScales = await LifeScalesService.GetAllAsync();
        if (entityType == NonStandardEntityType.CharacterClass)
        {
            foreach (var raceEntry in allLifeScales)
            {
                if (!raceEntry.Value.TryGetValue(token, out var points) || points.Count == 0)
                    continue;

                SelectedLifeScaleTargetRace = raceEntry.Key;
                SelectedLifeScaleSourceClass = token;
                _syncingLifeScaleRows = true;
                try
                {
                    LifeScalePointsJson = JsonSerializer.Serialize(
                        points.Where(point => point is { Length: >= 2 }).Select(point => new[] { point[0], point[1] }).ToList(),
                        PrettyJson);
                }
                finally
                {
                    _syncingLifeScaleRows = false;
                }

                SyncLifeScaleRowsFromJson();
                return;
            }

            return;
        }

        if (!allLifeScales.TryGetValue(token, out var classOptions) || classOptions.Count == 0)
            return;

        // Load all class entries for this race
        _raceLifeScaleEntries.Clear();
        foreach (var option in classOptions.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            var entry = new RaceLifeScaleClassEntryVm { ClassName = option.Key };
            var rawPoints = option.Value
                .Where(point => point is { Length: >= 2 })
                .Select(point => new LifeScalePoint(Math.Max(0, point[0]), Math.Max(0, point[1])))
                .ToList();
            if (rawPoints.Count > 0)
                entry.ApplyPoints(rawPoints);
            _raceLifeScaleEntries.Add(entry);
        }

        ReindexRaceLifeScaleEntries();
        Raise(nameof(CanSave));

        // Keep legacy fields in sync (first entry)
        var first = classOptions.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).First();
        SelectedLifeScaleTargetClass = first.Key;
        SelectedLifeScaleSourceRace = token;
    }

    private void ApplySelectedBaseTemplate()
    {
        var template = SelectedBaseTemplate;
        BuildFields(CurrentType, template?.Json);

        if (template != null)
        {
            Name = template.Name;
            SaveStatus = string.Empty;
        }

        if (CurrentType == NonStandardEntityType.CharacterClass)
            SelectedLifeScaleSourceClass = template?.Name ?? SelectedLifeScaleSourceClass;

        if (CurrentType == NonStandardEntityType.CharacterRace)
            SelectedLifeScaleSourceRace = template?.Name ?? SelectedLifeScaleSourceRace;

        if (CurrentType == NonStandardEntityType.CharacterRace)
            UpdateSubtypeMapKey();

        SyncLifeScaleRowsFromJson();
        UpdatePreview();
    }

    private void BuildFields(NonStandardEntityType entityType, string? baseJson)
    {
        foreach (var oldField in _fields)
            oldField.PropertyChanged -= OnFieldPropertyChanged;

        _fields.Clear();
        ResetRaceStructuredState();

        var definitions = FieldDefinitions.TryGetValue(entityType, out var configured)
            ? configured
            : Array.Empty<NonStandardFieldDefinition>();

        var properties = ParseObjectProperties(baseJson);
        if (entityType == NonStandardEntityType.CharacterRace)
            ParseRaceStructuredFields(properties);

        foreach (var definition in definitions)
        {
            var normalized = NormalizeFieldKey(definition.Key);
            if (entityType == NonStandardEntityType.CharacterRace && RaceStructuredFieldKeys.Contains(normalized))
                continue;

            string value = string.Empty;

            if (properties.TryGetValue(normalized, out var property))
            {
                value = ToEditableValue(property.Value, definition.Kind);
                properties.Remove(normalized);
            }

            var field = new NonStandardFieldVm(definition.Key, definition.Label, definition.Kind, value);
            field.PropertyChanged += OnFieldPropertyChanged;
            _fields.Add(field);
        }

        foreach (var extra in properties.Values.OrderBy(prop => prop.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (string.Equals(NormalizeFieldKey(extra.Name), NormalizeFieldKey("nonStandard"), StringComparison.Ordinal)
                || string.Equals(NormalizeFieldKey(extra.Name), NormalizeFieldKey("is_default"), StringComparison.Ordinal))
            {
                continue;
            }

            if (entityType == NonStandardEntityType.CharacterRace
                && RaceStructuredFieldKeys.Contains(NormalizeFieldKey(extra.Name)))
            {
                continue;
            }

            var field = new NonStandardFieldVm(
                key: extra.Name,
                label: BuildLabel(extra.Name),
                kind: InferKind(extra.Value),
                value: ToEditableValue(extra.Value, InferKind(extra.Value)));
            field.PropertyChanged += OnFieldPropertyChanged;
            _fields.Add(field);
        }

        Raise(nameof(Fields));
        Raise(nameof(IsRaceType));
        Raise(nameof(ShowStructuredRaceEditor));
        Raise(nameof(HasSubtypeEditorData));
        Raise(nameof(CanSave));
        UpdatePreview();
    }

    private void OnFieldPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(NonStandardFieldVm.Value))
            return;

        SaveStatus = string.Empty;
        UpdatePreview();
    }

    private void OnRaceAbilityRowsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var row in e.OldItems.OfType<RaceAbilityRowVm>())
                row.PropertyChanged -= OnRaceAbilityRowChanged;
        }

        if (e.NewItems != null)
        {
            foreach (var row in e.NewItems.OfType<RaceAbilityRowVm>())
                row.PropertyChanged += OnRaceAbilityRowChanged;
        }

        ReindexRaceAbilityRows();
        OnRaceStructuredDataChanged();
    }

    private void OnRaceSubtypeCopiesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var copy in e.OldItems.OfType<RaceSubtypeCopyVm>())
                DetachSubtypeCopy(copy);
        }

        if (e.NewItems != null)
        {
            foreach (var copy in e.NewItems.OfType<RaceSubtypeCopyVm>())
                AttachSubtypeCopy(copy);
        }

        ReindexSubtypeCopies();
        Raise(nameof(HasSubtypeEditorData));
        OnRaceStructuredDataChanged();
    }

    private void AttachSubtypeCopy(RaceSubtypeCopyVm copy)
    {
        copy.PropertyChanged += OnRaceSubtypeCopyChanged;
        copy.Abilities.CollectionChanged += OnSubtypeCopyAbilityRowsChanged;
        foreach (var row in copy.Abilities)
            row.PropertyChanged += OnRaceAbilityRowChanged;
    }

    private void DetachSubtypeCopy(RaceSubtypeCopyVm copy)
    {
        copy.PropertyChanged -= OnRaceSubtypeCopyChanged;
        copy.Abilities.CollectionChanged -= OnSubtypeCopyAbilityRowsChanged;
        foreach (var row in copy.Abilities)
            row.PropertyChanged -= OnRaceAbilityRowChanged;
    }

    private void OnSubtypeCopyAbilityRowsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var row in e.OldItems.OfType<RaceAbilityRowVm>())
                row.PropertyChanged -= OnRaceAbilityRowChanged;
        }

        if (e.NewItems != null)
        {
            foreach (var row in e.NewItems.OfType<RaceAbilityRowVm>())
                row.PropertyChanged += OnRaceAbilityRowChanged;
        }

        ReindexSubtypeCopies();
        OnRaceStructuredDataChanged();
    }

    private void OnRaceSubtypeCopyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RaceSubtypeCopyVm.Name) or nameof(RaceSubtypeCopyVm.Description))
            OnRaceStructuredDataChanged();
    }

    private void OnRaceAbilityRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RaceAbilityRowVm.AbilityName)
            or nameof(RaceAbilityRowVm.AbilityType)
            or nameof(RaceAbilityRowVm.Level)
            or nameof(RaceAbilityRowVm.CountText))
        {
            OnRaceStructuredDataChanged();
        }
    }

    private void AddRaceTagValue(string value)
    {
        if (_selectedRaceTags.Count >= 3)
            return;

        if (!_selectedRaceTags.Any(tag => tag.Equals(value, StringComparison.OrdinalIgnoreCase)))
            _selectedRaceTags.Add(value);

        if (!_raceTagOptions.Any(tag => tag.Equals(value, StringComparison.OrdinalIgnoreCase)))
        {
            _raceTagOptions.Add(value);
            Raise(nameof(RaceTagDictionary));
        }

        RebuildRaceTagRows();
    }

    private void RebuildRaceTagRows()
    {
        _raceTagRows.Clear();
        for (var index = 0; index < _selectedRaceTags.Count; index++)
        {
            _raceTagRows.Add(new RaceTagRowVm
            {
                Value = _selectedRaceTags[index],
                RowBackgroundHex = index % 2 == 0 ? "#F8FAFC" : "#FFFFFF"
            });
        }
    }

    private void ReindexRaceAbilityRows()
    {
        for (var index = 0; index < _raceAbilityRows.Count; index++)
            _raceAbilityRows[index].RowBackgroundHex = index % 2 == 0 ? "#F8FAFC" : "#FFFFFF";
    }

    private void ReindexSubtypeOptions()
    {
        for (var index = 0; index < _raceSubtypeOptions.Count; index++)
        {
            var option = _raceSubtypeOptions[index];
            var isSelected = ReferenceEquals(option, _selectedRaceSubtypeOption);
            option.IsSelected = isSelected;
            option.RowBackgroundHex = isSelected
                ? "#FFF7ED"
                : (index % 2 == 0 ? "#F8FAFC" : "#FFFFFF");
        }
    }

    private void ReindexSubtypeCopies()
    {
        for (var copyIndex = 0; copyIndex < _raceSubtypeCopies.Count; copyIndex++)
        {
            var copy = _raceSubtypeCopies[copyIndex];
            copy.RowBackgroundHex = copyIndex % 2 == 0 ? "#F8FAFC" : "#FFFFFF";

            for (var abilityIndex = 0; abilityIndex < copy.Abilities.Count; abilityIndex++)
                copy.Abilities[abilityIndex].RowBackgroundHex = abilityIndex % 2 == 0 ? "#F8FAFC" : "#FFFFFF";
        }
    }

    private void UpdateSubtypeMapKey()
    {
        if (!IsRaceType)
            return;

        var raceName = (SelectedBaseTemplate?.Name ?? Name ?? string.Empty).Trim();
        if (raceName.Length == 0)
            return;

        var mapKey = $"{raceName}SubtypeAbilities";
        if (!string.Equals(_raceSubtypeAbilityMapKey, mapKey, StringComparison.Ordinal))
        {
            _raceSubtypeAbilityMapKey = mapKey;
            Raise(nameof(RaceSubtypeAbilityMapKey));
            _ = RefreshSubtypeOptionsAsync();
        }

        if (!string.IsNullOrWhiteSpace(_raceSubtypeOptionsSource))
        {
            _raceSubtypeOptionsSource = string.Empty;
            Raise(nameof(RaceSubtypeOptionsSource));
        }
    }

    private void OnRaceStructuredDataChanged()
    {
        SaveStatus = string.Empty;
        if (IsRaceType)
            UpdateSubtypeMapKey();
        Raise(nameof(CanSave));
        UpdatePreview();
    }

    private void ResetRaceStructuredState()
    {
        _selectedRacePeopleTypes.Clear();
        _selectedRaceTags.Clear();
        _raceAbilityRows.Clear();
        _selectedRaceGuildOverrideTypes.Clear();
        _raceSubtypeOptions.Clear();
        _raceSubtypeCopies.Clear();
        _customLifeScaleRows.Clear();
        _raceLifeScaleEntries.Clear();
        _selectedRaceSubtypeOption = null;

        _raceSubtypeKey = string.Empty;
        _raceSubtypeDisplayName = string.Empty;
        _raceSubtypeDescription = string.Empty;
        _raceSubtypeSelectionMode = "SingleOptional";
        _raceSubtypeOptionsSource = string.Empty;
        _raceSubtypeAbilityMapKey = string.Empty;
        _raceBuyAsText = string.Empty;
        _raceTagInput = string.Empty;
        _raceTagSelectedValue = null;
        _includeRaceAlignmentRule = true;
        InitializeRaceAlignmentDefaults();
        Raise(nameof(RaceBuyAsText));
        Raise(nameof(RaceTagInput));
        Raise(nameof(RaceTagSelectedValue));
        Raise(nameof(HasRaceTags));
        Raise(nameof(RaceTagCountLabel));
        Raise(nameof(RaceTagDictionary));
        Raise(nameof(RaceSubtypeKey));
        Raise(nameof(RaceSubtypeDisplayName));
        Raise(nameof(RaceSubtypeDescription));
        Raise(nameof(RaceSubtypeSelectionMode));
        Raise(nameof(RaceSubtypeOptionsSource));
        Raise(nameof(RaceSubtypeAbilityMapKey));
        Raise(nameof(SelectedRaceSubtypeOption));
        Raise(nameof(IncludeRaceAlignmentRule));
        Raise(nameof(HasSubtypeEditorData));
        RebuildRaceTagRows();
    }

    private async Task EnsureRaceEditorLookupsAsync()
    {
        if (!_raceLookupsLoaded)
        {
            var races = await PeopleService.GetAllAsync();
            var peopleTypes = races.Values
                .SelectMany(record => record.PeopleType ?? new List<string>())
                .Select(value => (value ?? string.Empty).Trim())
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();

            ReplaceItems(_racePeopleTypeOptions, peopleTypes);

            _raceTagOptions.Clear();
            _raceTagOptions.AddRange(races.Values
                .SelectMany(record => record.Tags ?? new List<string>())
                .Select(value => (value ?? string.Empty).Trim())
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
            Raise(nameof(RaceTagDictionary));

            _raceAbilityLookup.Clear();
            var abilities = await EvolutionService.GetAllAbilitiesAsync();
            foreach (var ability in abilities)
            {
                var key = (ability.Index ?? string.Empty).Trim();
                if (key.Length == 0)
                    continue;

                _raceAbilityLookup[key] = ability;
            }

            _raceLookupsLoaded = true;
        }

        if (!_specialisationLookupLoaded)
        {
            _specialisationLookup.Clear();
            var all = await SpecialisationService.GetAllAsync();
            foreach (var entry in all)
                _specialisationLookup[entry.Key] = entry.Value;

            _specialisationLookupLoaded = true;
        }
    }

    private void ParseRaceStructuredFields(Dictionary<string, (string Name, JsonElement Value)> properties)
    {
        if (properties.TryGetValue(NormalizeFieldKey("PeopleType"), out var peopleTypeProperty))
        {
            ParseRacePeopleTypeProperty(peopleTypeProperty.Value);
            properties.Remove(NormalizeFieldKey("PeopleType"));
        }

        if (properties.TryGetValue(NormalizeFieldKey("Tags"), out var tagsProperty))
        {
            ParseRaceTags(tagsProperty.Value);
            properties.Remove(NormalizeFieldKey("Tags"));
        }

        if (properties.TryGetValue(NormalizeFieldKey("levelledAbilities"), out var levelledProperty))
        {
            ParseRaceLevelledAbilities(levelledProperty.Value);
            properties.Remove(NormalizeFieldKey("levelledAbilities"));
        }

        if (properties.TryGetValue(NormalizeFieldKey("Subtype"), out var subtypeProperty))
        {
            ParseRaceSubtype(subtypeProperty.Value);
            properties.Remove(NormalizeFieldKey("Subtype"));
        }

        if (properties.TryGetValue(NormalizeFieldKey("Buy-as"), out var buyAsProperty))
        {
            ParseRaceBuyAs(buyAsProperty.Value);
            properties.Remove(NormalizeFieldKey("Buy-as"));
        }

        if (properties.TryGetValue(NormalizeFieldKey("GuildOverrides"), out var guildProperty))
        {
            ParseRaceGuildOverrides(guildProperty.Value);
            properties.Remove(NormalizeFieldKey("GuildOverrides"));
        }

        if (properties.TryGetValue(NormalizeFieldKey("alignmentRule"), out var alignmentProperty))
        {
            ParseRaceAlignmentRule(alignmentProperty.Value);
            properties.Remove(NormalizeFieldKey("alignmentRule"));
        }

        _ = RefreshSubtypeOptionsAsync();
    }

    private void ParseRacePeopleTypeProperty(JsonElement element)
    {
        var parsed = new List<string>();
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                    continue;

                var value = (item.GetString() ?? string.Empty).Trim();
                if (value.Length > 0)
                    parsed.Add(value);
            }
        }
        else if (element.ValueKind == JsonValueKind.String)
        {
            var value = (element.GetString() ?? string.Empty).Trim();
            if (value.Length > 0)
                parsed.Add(value);
        }

        ReplaceItems(_selectedRacePeopleTypes, parsed.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private void ParseRaceTags(JsonElement element)
    {
        var parsed = new List<string>();
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                    continue;

                var value = (item.GetString() ?? string.Empty).Trim();
                if (value.Length > 0)
                    parsed.Add(value);
            }
        }
        else if (element.ValueKind == JsonValueKind.String)
        {
            var value = (element.GetString() ?? string.Empty).Trim();
            if (value.Length > 0)
                parsed.Add(value);
        }

        ReplaceItems(_selectedRaceTags, parsed.Distinct(StringComparer.OrdinalIgnoreCase));
        foreach (var tag in _selectedRaceTags)
        {
            if (!_raceTagOptions.Any(existing => existing.Equals(tag, StringComparison.OrdinalIgnoreCase)))
                _raceTagOptions.Add(tag);
        }

        Raise(nameof(RaceTagDictionary));
        Raise(nameof(RaceTagCountLabel));
        RebuildRaceTagRows();
    }

    private void ParseRaceLevelledAbilities(JsonElement element)
    {
        _raceAbilityRows.Clear();
        if (element.ValueKind != JsonValueKind.Object)
            return;

        foreach (var levelProperty in element.EnumerateObject())
        {
            if (!int.TryParse((levelProperty.Name ?? string.Empty).Trim(), out var level))
                continue;

            level = Math.Clamp(level, 1, 8);
            if (levelProperty.Value.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var abilityElement in levelProperty.Value.EnumerateArray())
            {
                AbilityDefinition ability;
                try
                {
                    ability = JsonSerializer.Deserialize<AbilityDefinition>(abilityElement.GetRawText()) ?? new AbilityDefinition();
                }
                catch
                {
                    continue;
                }

                var row = CreateRaceAbilityRow(
                    level: level,
                    abilityName: ability.Name,
                    type: ability.Type,
                    countText: ability.Count?.ToString() ?? string.Empty,
                    subtypeCopyId: null,
                    source: ability);
                _raceAbilityRows.Add(row);
            }
        }
    }

    private void ParseRaceSubtype(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return;

        RaceSubtypeKey = ReadStringProperty(element, "Key");
        RaceSubtypeDisplayName = ReadStringProperty(element, "DisplayName");
        RaceSubtypeDescription = ReadStringProperty(element, "Description");
        RaceSubtypeSelectionMode = string.IsNullOrWhiteSpace(ReadStringProperty(element, "SelectionMode"))
            ? "SingleOptional"
            : ReadStringProperty(element, "SelectionMode");
        RaceSubtypeOptionsSource = string.Empty;
        UpdateSubtypeMapKey();

        foreach (var copy in _raceSubtypeCopies.ToList())
            DetachSubtypeCopy(copy);
        _raceSubtypeCopies.Clear();

        if (!element.TryGetProperty("CustomOptions", out var customOptions)
            || customOptions.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in customOptions.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var copy = new RaceSubtypeCopyVm
            {
                Name = ReadStringProperty(item, "Name"),
                Description = ReadStringProperty(item, "Description")
            };

            if (item.TryGetProperty("levelledAbilities", out var levels)
                && levels.ValueKind == JsonValueKind.Object)
            {
                foreach (var levelProperty in levels.EnumerateObject())
                {
                    if (!int.TryParse((levelProperty.Name ?? string.Empty).Trim(), out var level))
                        continue;

                    if (levelProperty.Value.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var abilityElement in levelProperty.Value.EnumerateArray())
                    {
                        AbilityDefinition ability;
                        try
                        {
                            ability = JsonSerializer.Deserialize<AbilityDefinition>(abilityElement.GetRawText()) ?? new AbilityDefinition();
                        }
                        catch
                        {
                            continue;
                        }

                        var row = CreateRaceAbilityRow(
                            level: Math.Clamp(level, 1, 8),
                            abilityName: ability.Name,
                            type: ability.Type,
                            countText: ability.Count?.ToString() ?? string.Empty,
                            subtypeCopyId: copy.Id,
                            source: ability);
                        copy.Abilities.Add(row);
                    }
                }
            }

            _raceSubtypeCopies.Add(copy);
        }
    }

    private void ParseRaceBuyAs(JsonElement element)
    {
        RaceBuyAsText = element.ValueKind == JsonValueKind.String
            ? (element.GetString() ?? string.Empty).Trim()
            : string.Empty;
    }

    private void ParseRaceGuildOverrides(JsonElement element)
    {
        ReplaceItems(_selectedRaceGuildOverrideTypes, ParseGuildOverrideTypes(element));
    }

    private void ParseRaceAlignmentRule(JsonElement element)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<AlignmentRule>(element.GetRawText());
            if (parsed == null)
            {
                InitializeRaceAlignmentDefaults();
                return;
            }

            ApplyRaceAlignmentRule(parsed);
        }
        catch
        {
            InitializeRaceAlignmentDefaults();
        }
    }

    private async Task RefreshSubtypeOptionsAsync()
    {
        if (!IsRaceType)
            return;

        await EnsureRaceEditorLookupsAsync();

        var options = ResolveSubtypeOptions(RaceSubtypeOptionsSource);
        var abilityMapKey = (RaceSubtypeAbilityMapKey ?? string.Empty).Trim();

        if (_specialisationLookup.TryGetValue(abilityMapKey, out var specialisation)
            && specialisation.ColourAbilities is { Count: > 0 })
        {
            foreach (var mappedKey in specialisation.ColourAbilities.Keys)
            {
                if (!options.Contains(mappedKey, StringComparer.OrdinalIgnoreCase))
                    options.Add(mappedKey);
            }
        }

        var built = new List<RaceSubtypeOptionVm>();
        foreach (var option in options.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var vm = new RaceSubtypeOptionVm
            {
                Name = option
            };

            if (_specialisationLookup.TryGetValue(abilityMapKey, out var record)
                && record.ColourAbilities != null
                && record.ColourAbilities.TryGetValue(option, out var mapped))
            {
                vm.Description = mapped.Description ?? string.Empty;
                foreach (var row in BuildSubtypeOptionRows(mapped.Levels, option))
                    vm.Abilities.Add(row);
            }

            built.Add(vm);
        }

        ReplaceItems(_raceSubtypeOptions, built);
        if (_selectedRaceSubtypeOption != null)
            _selectedRaceSubtypeOption = _raceSubtypeOptions.FirstOrDefault(item =>
                item.Name.Equals(_selectedRaceSubtypeOption.Name, StringComparison.OrdinalIgnoreCase));
        ReindexSubtypeOptions();
        Raise(nameof(HasSubtypeEditorData));
        UpdatePreview();
    }

    private static IReadOnlyList<RaceAbilityRowVm> BuildSubtypeOptionRows(
        Dictionary<string, List<AbilityDefinition>>? levelMap,
        string optionName)
    {
        if (levelMap == null || levelMap.Count == 0)
            return Array.Empty<RaceAbilityRowVm>();

        var rows = new List<RaceAbilityRowVm>();
        foreach (var entry in levelMap)
        {
            if (!int.TryParse((entry.Key ?? string.Empty).Trim(), out var level))
                continue;

            foreach (var ability in entry.Value ?? new List<AbilityDefinition>())
            {
                if (ability == null || string.IsNullOrWhiteSpace(ability.Name))
                    continue;

                rows.Add(new RaceAbilityRowVm
                {
                    Level = Math.Clamp(level, 1, 8),
                    AbilityName = ability.Name,
                    AbilityType = ability.Type ?? "Static",
                    CountText = ability.Count?.ToString() ?? string.Empty,
                    SourceAbility = CloneAbilityDefinition(ability),
                    SourceContext = optionName
                });
            }
        }

        return rows
            .OrderBy(row => row.Level)
            .ThenBy(row => row.AbilityName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void InitializeRaceAlignmentDefaults()
    {
        ReplaceItems(_selectedRaceAlignments, EnumerateAllAlignments());
    }

    private void ApplyRaceAlignmentRule(AlignmentRule? rule)
    {
        var allowed = rule == null
            ? EnumerateAllAlignments().ToList()
            : CharacterDraft.ComputeAvailableAlignments(new[] { rule }).ToList();

        if (allowed.Count == 0)
            allowed = EnumerateAllAlignments().ToList();

        ReplaceItems(_selectedRaceAlignments, allowed);
    }

    private static IEnumerable<Alignment> EnumerateAllAlignments()
    {
        foreach (var order in Enum.GetValues<OrderAxis>())
        {
            foreach (var moral in Enum.GetValues<MoralAxis>())
                yield return new Alignment(order, moral);
        }
    }

    private void EnsureLifeScaleSelectionsAreValid()
    {
        if (!RequiresLifeScale)
            return;

        if (CurrentType == NonStandardEntityType.CharacterClass)
        {
            if (_raceOptions.Count > 0 && string.IsNullOrWhiteSpace((SelectedLifeScaleTargetRace ?? string.Empty).Trim()))
                SelectedLifeScaleTargetRace = _raceOptions.FirstOrDefault();

            if (_classOptions.Count > 0 && string.IsNullOrWhiteSpace((SelectedLifeScaleSourceClass ?? string.Empty).Trim()))
                SelectedLifeScaleSourceClass = SelectedBaseTemplate?.Name ?? _classOptions.FirstOrDefault();

            return;
        }

        if (_classOptions.Count > 0 && string.IsNullOrWhiteSpace((SelectedLifeScaleTargetClass ?? string.Empty).Trim()))
            SelectedLifeScaleTargetClass = _classOptions.FirstOrDefault();

        if (_raceOptions.Count > 0 && string.IsNullOrWhiteSpace((SelectedLifeScaleSourceRace ?? string.Empty).Trim()))
            SelectedLifeScaleSourceRace = SelectedBaseTemplate?.Name ?? _raceOptions.FirstOrDefault();
    }

    private void AddRaceAbilityRow(int level, string? abilityName, string? type, string? countText, Guid? subtypeCopyId)
    {
        var row = CreateRaceAbilityRow(level, abilityName, type, countText, subtypeCopyId);
        if (subtypeCopyId != null)
        {
            var copy = RaceSubtypeCopies.FirstOrDefault(item => item.Id == subtypeCopyId.Value);
            copy?.Abilities.Add(row);
            return;
        }

        _raceAbilityRows.Add(row);
    }

    private RaceAbilityRowVm CreateRaceAbilityRow(
        int level,
        string? abilityName,
        string? type,
        string? countText,
        Guid? subtypeCopyId,
        AbilityDefinition? source = null)
    {
        return new RaceAbilityRowVm
        {
            Level = Math.Clamp(level, 1, 8),
            AbilityName = (abilityName ?? string.Empty).Trim(),
            AbilityType = string.IsNullOrWhiteSpace((type ?? string.Empty).Trim()) ? "Static" : type.Trim(),
            CountText = (countText ?? string.Empty).Trim(),
            SubtypeCopyId = subtypeCopyId,
            SourceAbility = CloneAbilityDefinition(source),
            SourceContext = string.Empty
        };
    }

    private static RaceAbilityRowVm CloneRaceAbilityRow(RaceAbilityRowVm source, Guid copyId)
    {
        return new RaceAbilityRowVm
        {
            Level = source.Level,
            AbilityName = source.AbilityName,
            AbilityType = source.AbilityType,
            CountText = source.CountText,
            SubtypeCopyId = copyId,
            SourceAbility = CloneAbilityDefinition(source.SourceAbility),
            SourceContext = source.SourceContext
        };
    }

    private AbilityDefinition? BuildAbilityDefinitionFromRow(RaceAbilityRowVm row)
    {
        var name = (row.AbilityName ?? string.Empty).Trim();
        if (name.Length == 0)
            return null;

        var ability = CloneAbilityDefinition(row.SourceAbility) ?? new AbilityDefinition();
        ability.Name = name;
        ability.Type = string.IsNullOrWhiteSpace((row.AbilityType ?? string.Empty).Trim())
            ? "Static"
            : row.AbilityType.Trim();

        if (int.TryParse((row.CountText ?? string.Empty).Trim(), out var count) && count > 0)
            ability.Count = count;
        else
            ability.Count = null;

        return ability;
    }

    private string BuildNextSubtypeCopyName(string baseName)
    {
        var token = (baseName ?? string.Empty).Trim();
        if (token.Length == 0)
            token = "Custom subtype";

        var existing = new HashSet<string>(
            RaceSubtypeCopies
                .Select(copy => (copy.Name ?? string.Empty).Trim())
                .Where(name => name.Length > 0),
            StringComparer.OrdinalIgnoreCase);

        if (!existing.Contains(token))
            return token;

        var counter = 2;
        while (true)
        {
            var candidate = $"{token} ({counter})";
            if (!existing.Contains(candidate))
                return candidate;

            counter++;
        }
    }

    private static AbilityDefinition? CloneAbilityDefinition(AbilityDefinition? source)
    {
        if (source == null)
            return null;

        return new AbilityDefinition
        {
            Key = source.Key,
            AbilityRef = source.AbilityRef,
            GrantId = source.GrantId,
            GrantType = source.GrantType,
            Duration = source.Duration,
            Overrides = source.Overrides == null
                ? null
                : new GuildGrantOverrides
                {
                    DisplayName = source.Overrides.DisplayName,
                    Verbal = source.Overrides.Verbal,
                    Effect = source.Overrides.Effect,
                    Source = source.Overrides.Source,
                    GrantType = source.Overrides.GrantType,
                    Count = source.Overrides.Count,
                    Frequency = source.Overrides.Frequency,
                    Duration = source.Overrides.Duration
                },
            UpgradeGrantRef = source.UpgradeGrantRef,
            ReplaceWith = CloneAbilityDefinition(source.ReplaceWith),
            Modify = source.Modify == null
                ? null
                : new GuildGrantModify
                {
                    CountDelta = source.Modify.CountDelta
                },
            Name = source.Name,
            BattleboardNameOverride = source.BattleboardNameOverride,
            UpdateKey = source.UpdateKey,
            Type = source.Type,
            Effect = source.Effect,
            Lore = source.Lore,
            Source = source.Source,
            Count = source.Count,
            Progression = source.Progression == null
                ? null
                : new AbilityCountProgression
                {
                    Amount = source.Progression.Amount,
                    PerLevels = source.Progression.PerLevels,
                    Minimum = source.Progression.Minimum,
                    Maximum = source.Progression.Maximum
                },
            Amount = source.Amount?.ToList(),
            Frequency = source.Frequency,
            OverwriteKey = source.OverwriteKey,
            PreReqs = source.PreReqs?.ToList(),
            GuildOverrides = source.GuildOverrides?.ToList(),
            ChoiceSetRef = source.ChoiceSetRef,
            ChoiceSetRefs = source.ChoiceSetRefs?.ToList(),
            Customisation = source.Customisation == null
                ? null
                : new AbilityCustomisation
                {
                    OptionEnum = source.Customisation.OptionEnum,
                    CustomValuesPermitted = source.Customisation.CustomValuesPermitted
                },
            SystemEffects = source.SystemEffects?
                .Select(effect => new AbilitySystemEffect
                {
                    EffectType = effect.EffectType,
                    DisplayName = effect.DisplayName,
                    ResistanceType = effect.ResistanceType,
                    Level = effect.Level,
                    ImmunityName = effect.ImmunityName
                })
                .ToList()
        };
    }

    private static IReadOnlyList<string> ParseGuildOverrideTypes(JsonElement element)
    {
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        GuildOverrideRules? rules = null;

        try
        {
            rules = JsonSerializer.Deserialize<GuildOverrideRules>(element.GetRawText(), GuildJsonOptions);
        }
        catch
        {
            rules = null;
        }

        if (rules != null)
        {
            if (rules.IsCityBound)
                selected.Add("City");

            if (HasChannelOverride(rules.Social))
                selected.Add("Social");

            if (HasChannelOverride(rules.Professional))
                selected.Add("Professional");

            if (HasChannelOverride(rules.Political))
                selected.Add("Political");
        }

        return GuildOverrideTypes
            .Where(type => selected.Contains(type))
            .ToList();
    }

    private static bool HasChannelOverride(GuildOverrideChannel? channel)
    {
        if (channel == null)
            return false;

        return channel.CanJoin.HasValue
               || !string.IsNullOrWhiteSpace((channel.GuildPeople ?? string.Empty).Trim())
               || (channel.ReplacedBy?.Count ?? 0) > 0;
    }

    private static string ReadStringProperty(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
            return string.Empty;

        if (value.ValueKind == JsonValueKind.String)
            return (value.GetString() ?? string.Empty).Trim();

        return string.Empty;
    }

    private static List<string> ResolveSubtypeOptions(string? optionsSource)
    {
        var source = (optionsSource ?? string.Empty).Trim();
        if (source.Length == 0)
            return new List<string>();

        if (source.StartsWith("Enum:", StringComparison.OrdinalIgnoreCase))
        {
            var enumName = source["Enum:".Length..].Trim();
            if (enumName.Length == 0)
                return new List<string>();

            if (enumName.Equals("ElfColours", StringComparison.OrdinalIgnoreCase))
            {
                return
                [
                    "Fire", "Air", "Earth", "Aquatic", "Light", "Dark", "Twilight", "Bronze", "Ebony", "Gold",
                    "Ivory", "Silver", "Jade", "Onyx", "Winter", "Spring", "Summer", "Autumn"
                ];
            }

            if (enumName.Equals("AthfanalColours", StringComparison.OrdinalIgnoreCase))
                return ["Fire", "Aquatic", "Earth", "Air", "Twilight", "Light", "Dark"];

            var enumType = ReflectionHelper.FindEnumTypeByName(enumName);
            if (enumType != null)
                return Enum.GetNames(enumType).ToList();

            return new List<string>();
        }

        if (source.Contains(',', StringComparison.Ordinal))
        {
            return source
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(item => item.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return new List<string>();
    }

    private void UpdatePreview()
    {
        PreviewAbility = null;
        PreviewSpell = null;
        PreviewMiracle = null;
        PreviewEvocation = null;
        JsonPreviewText = string.Empty;

        try
        {
            var payload = BuildPayloadObject();
            if (payload == null)
                return;

            switch (CurrentType)
            {
                case NonStandardEntityType.Ability:
                    PreviewAbility = BuildAbilityPreview(payload);
                    break;
                case NonStandardEntityType.Spell:
                    PreviewSpell = BuildSpellPreview(payload);
                    break;
                case NonStandardEntityType.Miracle:
                    PreviewMiracle = BuildMiraclePreview(payload);
                    break;
                case NonStandardEntityType.Evocation:
                    PreviewEvocation = BuildEvocationPreview(payload);
                    break;
                case NonStandardEntityType.CharacterClass:
                case NonStandardEntityType.CharacterRace:
                    JsonPreviewText = string.Empty;
                    break;
            }
        }
        catch
        {
            // Keep preview hidden when editor content is temporarily malformed.
        }

        Raise(nameof(ShowAbilityPreview));
        Raise(nameof(ShowSpellPreview));
        Raise(nameof(ShowMiraclePreview));
        Raise(nameof(ShowEvocationPreview));
        Raise(nameof(ShowJsonPreview));
    }

    private EvolutionService.AbilityResult BuildAbilityPreview(JsonObject payload)
    {
        var index = ReadText(payload, "index", "idx");
        if (index.Length == 0)
            index = (Name ?? string.Empty).Trim();

        return new EvolutionService.AbilityResult
        {
            Index = index,
            Description = ReadText(payload, "desc", "description"),
            Cost = ReadInt(payload, 0, "cost"),
            Table = ReadInt(payload, 1, "table", "table_id"),
            Available = ReadNodeText(payload, "available"),
            CanBuyMultiple = ReadBool(payload, "canBuyMultiple", "can_buy_multiple"),
            PreReqs = ReadStringList(payload, "preReqs", "prereqs"),
            MaxAvailable = ReadNullableInt(payload, "maxAvailable", "max_available"),
            IsNonStandard = true
        };
    }

    private SpellService.SpellRaw BuildSpellPreview(JsonObject payload)
    {
        var json = payload.ToJsonString();
        var parsed = JsonSerializer.Deserialize<SpellService.SpellRaw>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new SpellService.SpellRaw();

        parsed.name = (Name ?? string.Empty).Trim();
        parsed.nonStandard = true;
        return parsed;
    }

    private MiracleService.MiracRaw BuildMiraclePreview(JsonObject payload)
    {
        var json = payload.ToJsonString();
        var parsed = JsonSerializer.Deserialize<MiracleService.MiracRaw>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new MiracleService.MiracRaw();

        parsed.name = (Name ?? string.Empty).Trim();
        parsed.nonStandard = true;
        return parsed;
    }

    private DruidEvocationService.EvocRaw BuildEvocationPreview(JsonObject payload)
    {
        var json = payload.ToJsonString();
        var parsed = JsonSerializer.Deserialize<DruidEvocationService.EvocRaw>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new DruidEvocationService.EvocRaw();

        parsed.name = (Name ?? string.Empty).Trim();
        parsed.nonStandard = true;
        return parsed;
    }

    private JsonObject BuildPayloadObject()
    {
        var payload = new JsonObject();

        foreach (var field in _fields)
        {
            var raw = (field.Value ?? string.Empty).Trim();
            if (raw.Length == 0)
                continue;

            payload[field.Key] = ParseFieldValue(raw, field.Kind, field.Label);
        }

        if (CurrentType == NonStandardEntityType.CharacterRace)
            AppendRaceStructuredPayload(payload);

        if (CurrentType == NonStandardEntityType.Ability)
            payload["index"] = (Name ?? string.Empty).Trim();

        if (CurrentType is NonStandardEntityType.Spell or NonStandardEntityType.Miracle or NonStandardEntityType.Evocation)
            payload["name"] = (Name ?? string.Empty).Trim();

        return payload;
    }

    private string BuildPayloadJson()
    {
        var payload = BuildPayloadObject();
        return payload.ToJsonString(PrettyJson);
    }

    private void AppendRaceStructuredPayload(JsonObject payload)
    {
        var peopleTypes = SelectedRacePeopleTypes
            .Select(value => (value ?? string.Empty).Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (peopleTypes.Count > 0)
            payload["PeopleType"] = JsonSerializer.SerializeToNode(peopleTypes);

        var tags = SelectedRaceTags
            .Select(value => (value ?? string.Empty).Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (tags.Count > 0)
            payload["Tags"] = JsonSerializer.SerializeToNode(tags);

        var buyAs = (RaceBuyAsText ?? string.Empty).Trim();
        if (buyAs.Length > 0)
            payload["Buy-as"] = buyAs;

        payload["levelledAbilities"] = BuildRaceLevelledAbilitiesPayload();

        var subtypePayload = BuildRaceSubtypePayload();
        if (subtypePayload != null)
            payload["Subtype"] = subtypePayload;

        var guildPayload = BuildRaceGuildOverridePayload();
        if (guildPayload != null)
            payload["GuildOverrides"] = guildPayload;

        payload["alignmentRule"] = JsonSerializer.SerializeToNode(BuildRaceAlignmentRule());
    }

    private JsonObject BuildRaceLevelledAbilitiesPayload()
    {
        var levels = Enumerable.Range(1, 8)
            .ToDictionary(level => level.ToString(), _ => new List<AbilityDefinition>(), StringComparer.OrdinalIgnoreCase);

        foreach (var row in RaceAbilityRows
                     .OrderBy(item => item.Level)
                     .ThenBy(item => item.AbilityName, StringComparer.OrdinalIgnoreCase))
        {
            var key = row.Level.ToString();
            if (!levels.TryGetValue(key, out var list))
                continue;

            var ability = BuildAbilityDefinitionFromRow(row);
            if (ability == null)
                continue;

            list.Add(ability);
        }

        var node = JsonSerializer.SerializeToNode(levels);
        return node as JsonObject ?? new JsonObject();
    }

    private JsonNode? BuildRaceSubtypePayload()
    {
        var hasSubtype = !string.IsNullOrWhiteSpace((RaceSubtypeKey ?? string.Empty).Trim())
            || !string.IsNullOrWhiteSpace((RaceSubtypeDisplayName ?? string.Empty).Trim())
            || !string.IsNullOrWhiteSpace((RaceSubtypeDescription ?? string.Empty).Trim())
            || !string.IsNullOrWhiteSpace((RaceSubtypeOptionsSource ?? string.Empty).Trim())
            || !string.IsNullOrWhiteSpace((RaceSubtypeAbilityMapKey ?? string.Empty).Trim())
            || RaceSubtypeCopies.Count > 0;

        if (!hasSubtype)
            return null;

        var subtype = new JsonObject
        {
            ["Key"] = (RaceSubtypeKey ?? string.Empty).Trim(),
            ["DisplayName"] = (RaceSubtypeDisplayName ?? string.Empty).Trim(),
            ["Description"] = (RaceSubtypeDescription ?? string.Empty).Trim(),
            ["SelectionMode"] = string.IsNullOrWhiteSpace((RaceSubtypeSelectionMode ?? string.Empty).Trim())
                ? "SingleOptional"
                : RaceSubtypeSelectionMode.Trim(),
            ["OptionsSource"] = (RaceSubtypeOptionsSource ?? string.Empty).Trim(),
            ["AbilityMapKey"] = (RaceSubtypeAbilityMapKey ?? string.Empty).Trim()
        };

        if (RaceSubtypeCopies.Count > 0)
        {
            var copies = new JsonArray();
            foreach (var copy in RaceSubtypeCopies)
            {
                var copyObject = new JsonObject
                {
                    ["Name"] = (copy.Name ?? string.Empty).Trim(),
                    ["Description"] = (copy.Description ?? string.Empty).Trim(),
                    ["levelledAbilities"] = BuildSubtypeCopyLevelledAbilitiesPayload(copy)
                };

                copies.Add(copyObject);
            }

            subtype["CustomOptions"] = copies;
        }

        return subtype;
    }

    private JsonObject BuildSubtypeCopyLevelledAbilitiesPayload(RaceSubtypeCopyVm copy)
    {
        var levels = Enumerable.Range(1, 8)
            .ToDictionary(level => level.ToString(), _ => new List<AbilityDefinition>(), StringComparer.OrdinalIgnoreCase);

        foreach (var row in copy.Abilities
                     .OrderBy(item => item.Level)
                     .ThenBy(item => item.AbilityName, StringComparer.OrdinalIgnoreCase))
        {
            if (!levels.TryGetValue(row.Level.ToString(), out var list))
                continue;

            var ability = BuildAbilityDefinitionFromRow(row);
            if (ability == null)
                continue;

            list.Add(ability);
        }

        var node = JsonSerializer.SerializeToNode(levels);
        return node as JsonObject ?? new JsonObject();
    }

    private JsonNode? BuildRaceGuildOverridePayload()
    {
        var selected = SelectedRaceGuildOverrideTypes
            .Select(value => (value ?? string.Empty).Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (selected.Count == 0)
            return null;

        var rules = new GuildOverrideRules();
        if (selected.Contains("City", StringComparer.OrdinalIgnoreCase))
            rules.IsCityBound = true;

        if (selected.Contains("Social", StringComparer.OrdinalIgnoreCase))
            rules.Social.CanJoin = false;

        if (selected.Contains("Professional", StringComparer.OrdinalIgnoreCase))
            rules.Professional.CanJoin = false;

        if (selected.Contains("Political", StringComparer.OrdinalIgnoreCase))
            rules.Political.CanJoin = false;

        return JsonSerializer.SerializeToNode(rules, GuildJsonOptions);
    }

    private AlignmentRule BuildRaceAlignmentRule()
    {
        var enabled = SelectedRaceAlignments
            .Distinct()
            .ToList();

        if (enabled.Count == 0)
            enabled = EnumerateAllAlignments().ToList();

        var allowedMoral = enabled.Select(alignment => alignment.Moral).Distinct().ToList();
        var allowedOrder = enabled.Select(alignment => alignment.Order).Distinct().ToList();
        var allowedPairs = enabled
            .Select(alignment => alignment.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new AlignmentRule
        {
            Mode = "restrict",
            Allowed = new AllowedAxes
            {
                Moral = allowedMoral,
                Order = allowedOrder
            },
            AllowedPairs = allowedPairs
        };
    }

    private static JsonNode ParseFieldValue(string raw, NonStandardFieldKind kind, string label)
    {
        try
        {
            return kind switch
            {
                NonStandardFieldKind.Text => raw,
                NonStandardFieldKind.Number => ParseNumberNode(raw),
                NonStandardFieldKind.Boolean => ParseBooleanNode(raw),
                NonStandardFieldKind.Json => ParseJsonNode(raw),
                _ => ParseAutoNode(raw)
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Invalid value for '{label}': {ex.Message}");
        }
    }

    private static JsonNode ParseNumberNode(string raw)
    {
        if (int.TryParse(raw, out var asInt))
            return asInt;

        if (double.TryParse(raw, out var asDouble))
            return asDouble;

        throw new FormatException("Expected a number.");
    }

    private static JsonNode ParseBooleanNode(string raw)
    {
        if (bool.TryParse(raw, out var asBool))
            return asBool;

        if (int.TryParse(raw, out var asInt))
            return asInt != 0;

        throw new FormatException("Expected true/false.");
    }

    private static JsonNode ParseJsonNode(string raw)
    {
        var node = JsonNode.Parse(raw);
        if (node == null)
            throw new FormatException("Expected valid JSON.");

        return node;
    }

    private static JsonNode ParseAutoNode(string raw)
    {
        if ((raw.StartsWith("{") && raw.EndsWith("}")) || (raw.StartsWith("[") && raw.EndsWith("]")))
        {
            var parsed = JsonNode.Parse(raw);
            if (parsed != null)
                return parsed;
        }

        if (bool.TryParse(raw, out var asBool))
            return asBool;

        if (int.TryParse(raw, out var asInt))
            return asInt;

        if (double.TryParse(raw, out var asDouble))
            return asDouble;

        return raw;
    }

    private static string ConvertAlternativeValue(string rawJson, NonStandardFieldKind kind)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            return kind switch
            {
                NonStandardFieldKind.Text => doc.RootElement.ValueKind == JsonValueKind.String
                    ? (doc.RootElement.GetString() ?? string.Empty)
                    : doc.RootElement.GetRawText(),
                NonStandardFieldKind.Number => doc.RootElement.GetRawText(),
                NonStandardFieldKind.Boolean => doc.RootElement.GetRawText(),
                NonStandardFieldKind.Json => JsonNode.Parse(doc.RootElement.GetRawText())?.ToJsonString(PrettyJson) ?? doc.RootElement.GetRawText(),
                _ => doc.RootElement.ValueKind == JsonValueKind.String
                    ? (doc.RootElement.GetString() ?? string.Empty)
                    : JsonNode.Parse(doc.RootElement.GetRawText())?.ToJsonString(PrettyJson) ?? doc.RootElement.GetRawText()
            };
        }
        catch
        {
            return rawJson;
        }
    }

    private static Dictionary<string, (string Name, JsonElement Value)> ParseObjectProperties(string? json)
    {
        var map = new Dictionary<string, (string Name, JsonElement Value)>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
            return map;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return map;

            foreach (var property in doc.RootElement.EnumerateObject())
                map[NormalizeFieldKey(property.Name)] = (property.Name, property.Value.Clone());
        }
        catch
        {
            // ignore malformed template JSON
        }

        return map;
    }

    private static string ToEditableValue(JsonElement value, NonStandardFieldKind kind)
    {
        if (value.ValueKind == JsonValueKind.String)
            return value.GetString() ?? string.Empty;

        if (value.ValueKind == JsonValueKind.Number || value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            return value.GetRawText();

        if (kind == NonStandardFieldKind.Json || value.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
            return JsonNode.Parse(value.GetRawText())?.ToJsonString(PrettyJson) ?? value.GetRawText();

        return value.GetRawText();
    }

    private static NonStandardFieldKind InferKind(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.True => NonStandardFieldKind.Boolean,
            JsonValueKind.False => NonStandardFieldKind.Boolean,
            JsonValueKind.Number => NonStandardFieldKind.Number,
            JsonValueKind.Array => NonStandardFieldKind.Json,
            JsonValueKind.Object => NonStandardFieldKind.Json,
            _ => NonStandardFieldKind.Auto
        };
    }

    private static string BuildLabel(string key)
    {
        var text = (key ?? string.Empty).Trim();
        if (text.Length == 0)
            return string.Empty;

        text = text.Replace("_", " ").Replace("-", " ");
        return text;
    }

    private static string NormalizeFieldKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        var chars = key
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray();
        return new string(chars);
    }

    private static string FormatEntityLabel(NonStandardEntityType type)
    {
        return type switch
        {
            NonStandardEntityType.CharacterClass => "Class",
            NonStandardEntityType.CharacterRace => "Race",
            NonStandardEntityType.Ability => "Ability",
            NonStandardEntityType.Miracle => "Miracle",
            NonStandardEntityType.Spell => "Spell",
            NonStandardEntityType.Evocation => "Evocation",
            _ => type.ToString()
        };
    }

    private static void ReplaceItems<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items)
            collection.Add(item);
    }

    private static bool TryParseLifeScalePoints(string json, out List<LifeScalePoint> points)
    {
        points = new List<LifeScalePoint>();
        var raw = (json ?? string.Empty).Trim();
        if (raw.Length == 0)
            return false;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Array)
                    continue;

                var tuple = element.EnumerateArray().ToList();
                if (tuple.Count < 2)
                    continue;

                if (!TryReadInt(tuple[0], out var body) || !TryReadInt(tuple[1], out var loc))
                    continue;

                points.Add(new LifeScalePoint(Math.Max(0, body), Math.Max(0, loc)));
            }

            return points.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private void SyncLifeScaleRowsFromJson()
    {
        if (_syncingLifeScaleRows)
            return;

        var points = TryParseLifeScalePoints(LifeScalePointsJson, out var parsed)
            ? parsed
            : new List<LifeScalePoint>();

        while (points.Count < 8)
            points.Add(new LifeScalePoint(0, 0));

        foreach (var row in _customLifeScaleRows)
            row.PropertyChanged -= OnCustomLifeScaleRowChanged;

        _customLifeScaleRows.Clear();
        for (var index = 0; index < 8; index++)
        {
            var row = new CustomLifeScalePointVm
            {
                Level = index + 1,
                BodyText = points[index].Body.ToString(),
                LocText = points[index].Loc.ToString()
            };
            row.PropertyChanged += OnCustomLifeScaleRowChanged;
            _customLifeScaleRows.Add(row);
        }
    }

    private void OnCustomLifeScaleRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(CustomLifeScalePointVm.BodyText) or nameof(CustomLifeScalePointVm.LocText)))
            return;

        _syncingLifeScaleRows = true;
        try
        {
            var points = _customLifeScaleRows
                .OrderBy(row => row.Level)
                .Select(row => new[]
                {
                    Math.Max(0, int.TryParse((row.BodyText ?? string.Empty).Trim(), out var body) ? body : 0),
                    Math.Max(0, int.TryParse((row.LocText ?? string.Empty).Trim(), out var loc) ? loc : 0)
                })
                .ToList();

            LifeScalePointsJson = JsonSerializer.Serialize(points, PrettyJson);
        }
        finally
        {
            _syncingLifeScaleRows = false;
        }
    }

    private static bool TryReadInt(JsonElement element, out int value)
    {
        value = 0;
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value))
            return true;

        if (element.ValueKind == JsonValueKind.String
            && int.TryParse((element.GetString() ?? string.Empty).Trim(), out value))
        {
            return true;
        }

        return false;
    }

    private static string ReadText(JsonObject payload, params string[] keys)
    {
        var node = ReadNode(payload, keys);
        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var text))
                return (text ?? string.Empty).Trim();

            if (value.TryGetValue<int>(out var number))
                return number.ToString();
        }

        return node?.ToJsonString() ?? string.Empty;
    }

    private static int ReadInt(JsonObject payload, int fallback, params string[] keys)
    {
        var node = ReadNode(payload, keys);
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var asInt))
                return asInt;

            if (value.TryGetValue<string>(out var asText)
                && int.TryParse((asText ?? string.Empty).Trim(), out asInt))
            {
                return asInt;
            }
        }

        return fallback;
    }

    private static int? ReadNullableInt(JsonObject payload, params string[] keys)
    {
        var node = ReadNode(payload, keys);
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var asInt))
                return asInt;

            if (value.TryGetValue<string>(out var asText)
                && int.TryParse((asText ?? string.Empty).Trim(), out asInt))
            {
                return asInt;
            }
        }

        return null;
    }

    private static bool ReadBool(JsonObject payload, params string[] keys)
    {
        var node = ReadNode(payload, keys);
        if (node is JsonValue value)
        {
            if (value.TryGetValue<bool>(out var asBool))
                return asBool;

            if (value.TryGetValue<int>(out var asInt))
                return asInt != 0;

            if (value.TryGetValue<string>(out var asText))
            {
                var trimmed = (asText ?? string.Empty).Trim();
                if (bool.TryParse(trimmed, out asBool))
                    return asBool;
                if (int.TryParse(trimmed, out asInt))
                    return asInt != 0;
            }
        }

        return false;
    }

    private static string ReadNodeText(JsonObject payload, params string[] keys)
    {
        var node = ReadNode(payload, keys);
        if (node == null)
            return string.Empty;

        if (node is JsonValue value && value.TryGetValue<string>(out var asText))
            return asText ?? string.Empty;

        return node.ToJsonString();
    }

    private static IReadOnlyList<string> ReadStringList(JsonObject payload, params string[] keys)
    {
        var node = ReadNode(payload, keys);
        if (node is not JsonArray array)
            return Array.Empty<string>();

        return array
            .Select(entry =>
            {
                if (entry is JsonValue value && value.TryGetValue<string>(out var text))
                    return text ?? string.Empty;
                return string.Empty;
            })
            .Select(text => text.Trim())
            .Where(text => text.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static JsonNode? ReadNode(JsonObject payload, params string[] keys)
    {
        var wanted = new HashSet<string>(keys.Select(NormalizeFieldKey), StringComparer.OrdinalIgnoreCase);
        foreach (var property in payload)
        {
            if (wanted.Contains(NormalizeFieldKey(property.Key)))
                return property.Value;
        }

        return null;
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
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class NonStandardTypeOptionVm
{
    public NonStandardTypeOptionVm(NonStandardEntityType entityType, string label)
    {
        EntityType = entityType;
        Label = label;
    }

    public NonStandardEntityType EntityType { get; }
    public string Label { get; }
}

public sealed class CharacterAssignmentOptionVm
{
    public CharacterAssignmentOptionVm(string id, string name, string playerName, string className)
    {
        Id = (id ?? string.Empty).Trim();
        Name = (name ?? string.Empty).Trim();
        PlayerName = (playerName ?? string.Empty).Trim();
        ClassName = (className ?? string.Empty).Trim();
    }

    public string Id { get; }
    public string Name { get; }
    public string PlayerName { get; }
    public string ClassName { get; }
    public string Display => string.IsNullOrWhiteSpace(PlayerName)
        ? $"{Name} ({ClassName})"
        : $"{Name} ({ClassName}) - {PlayerName}";
}

public enum NonStandardFieldKind
{
    Auto,
    Text,
    Number,
    Boolean,
    Json
}

public sealed class NonStandardFieldVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _value;

    public NonStandardFieldVm(string key, string label, NonStandardFieldKind kind, string value)
    {
        Key = key;
        Label = label;
        Kind = kind;
        _value = value ?? string.Empty;
    }

    public string Key { get; }
    public string Label { get; }
    public NonStandardFieldKind Kind { get; }

    public bool IsMultiLine => Kind == NonStandardFieldKind.Json || (Value?.Length ?? 0) > 80;
    public bool IsSingleLine => !IsMultiLine;

    public string Value
    {
        get => _value;
        set
        {
            if (string.Equals(_value, value, StringComparison.Ordinal))
                return;

            _value = value ?? string.Empty;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsMultiLine)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSingleLine)));
        }
    }
}

public sealed class NonStandardFieldDefinition
{
    public NonStandardFieldDefinition(string key, NonStandardFieldKind kind)
    {
        Key = key;
        Kind = kind;
    }

    public string Key { get; }
    public string Label => Key.Replace("_", " ").Replace("-", " ");
    public NonStandardFieldKind Kind { get; }
}

public sealed class RaceAbilityRowVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private int _level = 1;
    private string _abilityName = string.Empty;
    private string _abilityType = "Static";
    private string _countText = string.Empty;
    private string _rowBackgroundHex = "#FFFFFF";

    public Guid? SubtypeCopyId { get; set; }
    public AbilityDefinition? SourceAbility { get; set; }
    public string SourceContext { get; set; } = string.Empty;
    public IReadOnlyList<int> LevelOptions { get; } = Enumerable.Range(1, 8).ToList();

    public int Level
    {
        get => _level;
        set
        {
            var next = Math.Clamp(value, 1, 8);
            if (_level == next)
                return;

            _level = next;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Level)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LevelLabel)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RowSummary)));
        }
    }

    public string AbilityName
    {
        get => _abilityName;
        set
        {
            var next = value ?? string.Empty;
            if (string.Equals(_abilityName, next, StringComparison.Ordinal))
                return;

            _abilityName = next;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AbilityName)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RowSummary)));
        }
    }

    public string AbilityType
    {
        get => _abilityType;
        set
        {
            var next = string.IsNullOrWhiteSpace((value ?? string.Empty).Trim()) ? "Static" : value.Trim();
            if (string.Equals(_abilityType, next, StringComparison.Ordinal))
                return;

            _abilityType = next;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AbilityType)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RowSummary)));
        }
    }

    public string CountText
    {
        get => _countText;
        set
        {
            var next = value ?? string.Empty;
            if (string.Equals(_countText, next, StringComparison.Ordinal))
                return;

            _countText = next;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CountText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RowSummary)));
        }
    }

    public string LevelLabel => $"L{Level}";

    public string RowBackgroundHex
    {
        get => _rowBackgroundHex;
        set
        {
            var next = value ?? "#FFFFFF";
            if (string.Equals(_rowBackgroundHex, next, StringComparison.Ordinal))
                return;

            _rowBackgroundHex = next;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RowBackgroundHex)));
        }
    }

    public string RowSummary
    {
        get
        {
            var parts = new List<string>
            {
                (AbilityName ?? string.Empty).Trim(),
                string.IsNullOrWhiteSpace((AbilityType ?? string.Empty).Trim()) ? "Static" : AbilityType.Trim()
            };

            if (int.TryParse((CountText ?? string.Empty).Trim(), out var count) && count > 0)
                parts.Add($"x{count}");

            return string.Join(" • ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }
    }

    public void RefreshSummary()
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RowSummary)));
}

public sealed class RaceSubtypeOptionVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private string _rowBackgroundHex = "#FFFFFF";
    private bool _isSelected;

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ObservableCollection<RaceAbilityRowVm> Abilities { get; } = new();

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

    public string RowBackgroundHex
    {
        get => _rowBackgroundHex;
        set
        {
            var next = value ?? "#FFFFFF";
            if (string.Equals(_rowBackgroundHex, next, StringComparison.Ordinal))
                return;

            _rowBackgroundHex = next;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RowBackgroundHex)));
        }
    }

    public string AbilitySummary
    {
        get
        {
            if (Abilities.Count == 0)
                return "No mapped abilities.";

            var grouped = Abilities
                .GroupBy(row => row.Level)
                .OrderBy(group => group.Key)
                .Select(group => $"L{group.Key}: {group.Count()}");

            return string.Join("  |  ", grouped);
        }
    }
}

public sealed class RaceSubtypeCopyVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _rowBackgroundHex = "#FFFFFF";

    public RaceSubtypeCopyVm()
    {
        Abilities.CollectionChanged += (_, _) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AbilityCountSummary)));
    }

    public Guid Id { get; } = Guid.NewGuid();
    public ObservableCollection<RaceAbilityRowVm> Abilities { get; } = new();

    public string Name
    {
        get => _name;
        set
        {
            var next = value ?? string.Empty;
            if (string.Equals(_name, next, StringComparison.Ordinal))
                return;

            _name = next;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }

    public string Description
    {
        get => _description;
        set
        {
            var next = value ?? string.Empty;
            if (string.Equals(_description, next, StringComparison.Ordinal))
                return;

            _description = next;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description)));
        }
    }

    public string AbilityCountSummary => Abilities.Count == 1 ? "1 ability row" : $"{Abilities.Count} ability rows";

    public string RowBackgroundHex
    {
        get => _rowBackgroundHex;
        set
        {
            var next = value ?? "#FFFFFF";
            if (string.Equals(_rowBackgroundHex, next, StringComparison.Ordinal))
                return;

            _rowBackgroundHex = next;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RowBackgroundHex)));
        }
    }
}

public sealed class RaceTagRowVm
{
    public string Value { get; init; } = string.Empty;
    public string RowBackgroundHex { get; init; } = "#FFFFFF";
}

public sealed class RaceLifeScaleClassEntryVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _className = string.Empty;
    private bool _isEditorExpanded;
    private string _rowBackgroundHex = "#FFFFFF";

    public RaceLifeScaleClassEntryVm()
    {
        CustomLifeScaleRows = new ObservableCollection<CustomLifeScalePointVm>(
            Enumerable.Range(1, 8).Select(level => new CustomLifeScalePointVm { Level = level }));
    }

    public string ClassName
    {
        get => _className;
        set
        {
            var next = value ?? string.Empty;
            if (string.Equals(_className, next, StringComparison.Ordinal))
                return;
            _className = next;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ClassName)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ClassNameDisplay)));
        }
    }

    public string ClassNameDisplay => string.IsNullOrWhiteSpace(_className) ? "Select class…" : _className;

    public bool IsEditorExpanded
    {
        get => _isEditorExpanded;
        set
        {
            if (_isEditorExpanded == value)
                return;
            _isEditorExpanded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEditorExpanded)));
        }
    }

    public string RowBackgroundHex
    {
        get => _rowBackgroundHex;
        set
        {
            var next = value ?? "#FFFFFF";
            if (string.Equals(_rowBackgroundHex, next, StringComparison.Ordinal))
                return;
            _rowBackgroundHex = next;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RowBackgroundHex)));
        }
    }

    public ObservableCollection<CustomLifeScalePointVm> CustomLifeScaleRows { get; }

    public void ApplyPoints(IReadOnlyList<LifeScalePoint> points)
    {
        CustomLifeScaleRows.Clear();
        for (var i = 0; i < 8; i++)
        {
            var point = i < points.Count ? points[i] : new LifeScalePoint(0, 0);
            CustomLifeScaleRows.Add(new CustomLifeScalePointVm
            {
                Level = i + 1,
                BodyText = point.Body.ToString(),
                LocText = point.Loc.ToString()
            });
        }
    }

    public IReadOnlyList<LifeScalePoint> ToPoints()
        => CustomLifeScaleRows
            .Select(row => new LifeScalePoint(
                Math.Max(0, int.TryParse((row.BodyText ?? string.Empty).Trim(), out var body) ? body : 0),
                Math.Max(0, int.TryParse((row.LocText ?? string.Empty).Trim(), out var loc) ? loc : 0)))
            .ToList();
}
