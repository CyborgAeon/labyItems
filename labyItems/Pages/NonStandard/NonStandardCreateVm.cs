using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using labyItems.Services;

namespace labyItems.Pages.NonStandard;

public sealed class NonStandardCreateVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

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

    private bool _initialized;
    private bool _suppressTypeReload;
    private bool _isBusy;
    private string _name = string.Empty;
    private string _saveStatus = string.Empty;
    private string _lifeScalePointsJson = string.Empty;
    private bool _isLifeScaleExpanded;

    private NonStandardTypeOptionVm? _selectedEntityType;
    private NonStandardTemplate? _selectedBaseTemplate;
    private string? _selectedLifeScaleTargetRace;
    private string? _selectedLifeScaleTargetClass;
    private string? _selectedLifeScaleSourceRace;
    private string? _selectedLifeScaleSourceClass;

    private EvolutionService.AbilityResult? _previewAbility;
    private SpellService.SpellRaw? _previewSpell;
    private MiracleService.MiracRaw? _previewMiracle;
    private DruidEvocationService.EvocRaw? _previewEvocation;
    private string _jsonPreviewText = string.Empty;

    public ObservableCollection<NonStandardTypeOptionVm> EntityTypes => _entityTypes;
    public ObservableCollection<NonStandardTemplate> BaseTemplates => _baseTemplates;
    public ObservableCollection<NonStandardFieldVm> Fields => _fields;
    public ObservableCollection<string> LifeScaleRaceOptions => _raceOptions;
    public ObservableCollection<string> LifeScaleClassOptions => _classOptions;

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
        }
    }

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
                return true;

            if (CurrentType == NonStandardEntityType.CharacterClass)
            {
                if (string.IsNullOrWhiteSpace((SelectedLifeScaleTargetRace ?? string.Empty).Trim()))
                    return false;
            }

            if (CurrentType == NonStandardEntityType.CharacterRace)
            {
                if (string.IsNullOrWhiteSpace((SelectedLifeScaleTargetClass ?? string.Empty).Trim()))
                    return false;
            }

            return TryParseLifeScalePoints(LifeScalePointsJson, out var points) && points.Count >= 8;
        }
    }

    private NonStandardEntityType CurrentType => SelectedEntityType?.EntityType ?? NonStandardEntityType.CharacterClass;

    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        _initialized = true;

        _entityTypes.Clear();
        foreach (var type in Enum.GetValues<NonStandardEntityType>())
            _entityTypes.Add(new NonStandardTypeOptionVm(type, FormatEntityLabel(type)));

        SelectedEntityType = _entityTypes.FirstOrDefault();

        await LoadLifeScaleLookupsAsync();
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

            if (RequiresLifeScale)
            {
                if (!TryParseLifeScalePoints(LifeScalePointsJson, out var points) || points.Count < 8)
                    throw new InvalidOperationException("Life-scale must contain 8 levels.");

                request.LifeScalePoints = points;

                if (CurrentType == NonStandardEntityType.CharacterClass)
                {
                    request.LifeScaleRaceName = (SelectedLifeScaleTargetRace ?? string.Empty).Trim();
                }
                else
                {
                    request.LifeScaleClassName = (SelectedLifeScaleTargetClass ?? string.Empty).Trim();
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

        await InitializeAsync();
        var typeOption = _entityTypes.FirstOrDefault(option => option.EntityType == entry.EntityType);
        if (typeOption == null)
            return;

        _suppressTypeReload = true;
        SelectedEntityType = typeOption;
        _suppressTypeReload = false;

        await ReloadForSelectedTypeAsync(entry.Name);
        BuildFields(entry.EntityType, entry.DataJson);
        Name = entry.Name;
        SaveStatus = string.Empty;
        UpdatePreview();

        await LoadExistingLifeScaleSelectionAsync(entry.EntityType, entry.Name);
        Raise(nameof(CanSave));
    }

    private async Task ReloadForSelectedTypeAsync(string? preferredTemplateName)
    {
        if (!_initialized)
            return;

        var type = CurrentType;
        var templates = await NonStandardContentService.GetTemplatesAsync(type);

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
        Raise(nameof(CanSave));
    }

    private async Task LoadLifeScaleLookupsAsync()
    {
        var races = await PeopleService.GetAllAsync();
        var classes = await ClassService.GetAllAsync();

        ReplaceItems(_raceOptions, races.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
        ReplaceItems(_classOptions, classes.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
    }

    private async Task ConfigureLifeScaleDefaultsAsync()
    {
        if (!RequiresLifeScale)
            return;

        if (CurrentType == NonStandardEntityType.CharacterClass)
        {
            SelectedLifeScaleTargetRace ??= _raceOptions.FirstOrDefault();
            SelectedLifeScaleSourceClass ??= (SelectedBaseTemplate?.Name ?? _classOptions.FirstOrDefault());
        }
        else if (CurrentType == NonStandardEntityType.CharacterRace)
        {
            SelectedLifeScaleTargetClass ??= _classOptions.FirstOrDefault();
            SelectedLifeScaleSourceRace ??= (SelectedBaseTemplate?.Name ?? _raceOptions.FirstOrDefault());
        }

        await RefreshLifeScaleFromSelectionAsync();
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
        LifeScalePointsJson = JsonSerializer.Serialize(tuples, PrettyJson);
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
                LifeScalePointsJson = JsonSerializer.Serialize(
                    points.Where(point => point is { Length: >= 2 }).Select(point => new[] { point[0], point[1] }).ToList(),
                    PrettyJson);
                return;
            }

            return;
        }

        if (!allLifeScales.TryGetValue(token, out var classOptions) || classOptions.Count == 0)
            return;

        var first = classOptions
            .OrderBy(option => option.Key, StringComparer.OrdinalIgnoreCase)
            .First();

        SelectedLifeScaleTargetClass = first.Key;
        SelectedLifeScaleSourceRace = token;
        LifeScalePointsJson = JsonSerializer.Serialize(
            first.Value.Where(point => point is { Length: >= 2 }).Select(point => new[] { point[0], point[1] }).ToList(),
            PrettyJson);
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

        UpdatePreview();
    }

    private void BuildFields(NonStandardEntityType entityType, string? baseJson)
    {
        foreach (var oldField in _fields)
            oldField.PropertyChanged -= OnFieldPropertyChanged;

        _fields.Clear();

        var definitions = FieldDefinitions.TryGetValue(entityType, out var configured)
            ? configured
            : Array.Empty<NonStandardFieldDefinition>();

        var properties = ParseObjectProperties(baseJson);

        foreach (var definition in definitions)
        {
            var normalized = NormalizeFieldKey(definition.Key);
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

            var field = new NonStandardFieldVm(
                key: extra.Name,
                label: BuildLabel(extra.Name),
                kind: InferKind(extra.Value),
                value: ToEditableValue(extra.Value, InferKind(extra.Value)));
            field.PropertyChanged += OnFieldPropertyChanged;
            _fields.Add(field);
        }

        Raise(nameof(Fields));
        Raise(nameof(CanSave));
    }

    private void OnFieldPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(NonStandardFieldVm.Value))
            return;

        SaveStatus = string.Empty;
        UpdatePreview();
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
                    JsonPreviewText = payload.ToJsonString(PrettyJson);
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
