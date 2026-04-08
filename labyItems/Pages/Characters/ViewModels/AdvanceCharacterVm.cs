using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Text;
using ClosedXML.Excel;
using labyItems.Controls;
using labyItems.Helpers;
using labyItems.Models;
using labyItems.Models.Abilities;
using labyItems.Models.Characters;
using labyItems.Models.Enums;
using labyItems.Models.Rules;
using labyItems.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using ServiceCharacterClassRecord = labyItems.Services.CharacterClassRecord;

namespace labyItems.Pages.Characters.ViewModels;

public sealed class AdvanceCharacterVm : INotifyPropertyChanged
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

    private readonly ICharacterDraftStore _draftStore;
    private readonly ICharacterAdvancementDomainService _domainService;
    private readonly IAdvancementTabVisibilityService _tabVisibilityService;
    private readonly IAdvancementValidationService _validationService;
    private readonly IExportService _exportService;
    private readonly IFileService _fileService;
    private readonly IAdvanceCharacterDataProvider _dataProvider;
    private readonly IAdvanceAbilityLookupService _abilityLookupService;
    private readonly IAbilityAvailabilityService _abilityAvailabilityService;
    private readonly IAdvanceCharacterAbilityService _abilityService;
    private readonly CharacterDraft _draft;
    private IReadOnlyList<MiracleService.MiracRaw> _allMiracles = Array.Empty<MiracleService.MiracRaw>();
    private IReadOnlyList<SpellService.SpellRaw> _allSpells = Array.Empty<SpellService.SpellRaw>();
    private IReadOnlyList<DruidEvocationService.EvocRaw> _allEvocations = Array.Empty<DruidEvocationService.EvocRaw>();
    private Dictionary<string, GuildRecord> _guilds = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, ServiceCharacterClassRecord> _classes = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, PeopleRecord> _races = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, ManuAbilityOption> _abilityOptions = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, ManuAbilityOption> _abilityOptionsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MultiClassDefinition> _multiClassDefinitionsByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MultiRaceDefinition> _multiRaceDefinitionsByKey = new(StringComparer.OrdinalIgnoreCase);
    private bool _hasLoadedReferenceData;
    private bool _suppressAbilityReactions;

    private const string NightsoilRaceName = "Nightsoil";

    private bool _showSpellsTab;
    public bool ShowSpellsTab { get => _showSpellsTab; private set => Set(ref _showSpellsTab, value); }

    private bool _showMiraclesTab;
    public bool ShowMiraclesTab { get => _showMiraclesTab; private set => Set(ref _showMiraclesTab, value); }

    private bool _showPriestMiracleLists;
    public bool ShowPriestMiracleLists { get => _showPriestMiracleLists; private set => Set(ref _showPriestMiracleLists, value); }

    private bool _showEvilStairway;
    public bool ShowEvilStairway { get => _showEvilStairway; private set => Set(ref _showEvilStairway, value); }

    private bool _showEvocationsTab;
    public bool ShowEvocationsTab { get => _showEvocationsTab; private set => Set(ref _showEvocationsTab, value); }

    private IReadOnlyList<SpecialistSlotSegmentVm> _specialistSlotSegments = Array.Empty<SpecialistSlotSegmentVm>();
    public IReadOnlyList<SpecialistSlotSegmentVm> SpecialistSlotSegments
    {
        get => _specialistSlotSegments;
        private set => Set(ref _specialistSlotSegments, value);
    }

    private IReadOnlyList<SpecialistSlotLegendVm> _specialistSlotLegendItems = Array.Empty<SpecialistSlotLegendVm>();
    public IReadOnlyList<SpecialistSlotLegendVm> SpecialistSlotLegendItems
    {
        get => _specialistSlotLegendItems;
        private set => Set(ref _specialistSlotLegendItems, value);
    }

    private int _specialistSlotsUsed;
    public int SpecialistSlotsUsed
    {
        get => _specialistSlotsUsed;
        private set => Set(ref _specialistSlotsUsed, value);
    }

    private int _specialistSlotsTotal;
    public int SpecialistSlotsTotal
    {
        get => _specialistSlotsTotal;
        private set => Set(ref _specialistSlotsTotal, value);
    }

    private Color _specialistSlotsSummaryColor = Colors.Black;
    public Color SpecialistSlotsSummaryColor
    {
        get => _specialistSlotsSummaryColor;
        private set => Set(ref _specialistSlotsSummaryColor, value);
    }

    public string SpecialistSlotsSummary =>
        SpecialistSlotsTotal > 0
            ? $"Specialist slots: {SpecialistSlotsUsed}/{SpecialistSlotsTotal}"
            : string.Empty;

    public bool ShowSpecialistSlotsBar => SpecialistSlotsTotal > 0;
    public bool ShowSpecialistSlotsLegend => SpecialistSlotLegendItems.Count > 0;

    public bool CanAddSpecialistList => SpecialistSlotsTotal > 0 && SpecialistSpellLists.Count == 0;

    public AdvanceCharacterVm(
        CharacterDraft draft,
        ICharacterDraftStore draftStore,
        ICharacterAdvancementDomainService domainService,
        IAdvancementTabVisibilityService tabVisibilityService,
        IAdvancementValidationService validationService,
        IExportService exportService,
        IFileService fileService,
        IAdvanceCharacterDataProvider dataProvider,
        IAdvanceAbilityLookupService abilityLookupService,
        IAbilityAvailabilityService abilityAvailabilityService,
        IAdvanceCharacterAbilityService? abilityService = null)
    {
        _draftStore = draftStore ?? throw new ArgumentNullException(nameof(draftStore));
        _draft = _draftStore.Draft;
        _domainService = domainService ?? throw new ArgumentNullException(nameof(domainService));
        _tabVisibilityService = tabVisibilityService ?? throw new ArgumentNullException(nameof(tabVisibilityService));
        _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
        _abilityLookupService = abilityLookupService ?? throw new ArgumentNullException(nameof(abilityLookupService));
        _abilityAvailabilityService = abilityAvailabilityService ?? throw new ArgumentNullException(nameof(abilityAvailabilityService));
        _abilityService = abilityService ?? ServiceHelper.ResolveService<IAdvanceCharacterAbilityService>() ?? new AdvanceCharacterAbilityService(_abilityLookupService, _abilityAvailabilityService, _dataProvider);
        _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));

        Items.CollectionChanged += OnItemsCollectionChanged;
        Abilities.CollectionChanged += (_, __) => OnAdvancementAbilityCollectionChanged();
        MultiClasses.CollectionChanged += (_, __) => OnMultiClassCollectionChanged();

        ApplyRaceSpecificVitaeRules();

        AddAbilityCommand = new Command(AddAbility);
        RemoveAbilityCommand = new Command<AbilityEntryVm>(RemoveAbility);
        RemoveMultiClassCommand = new Command<MultiClassEntryVm>(entry => _ = RemoveMultiClassAsync(entry));
        RemoveMultiRaceCommand = new Command<MultiRaceEntryVm>(entry => _ = RemoveMultiRaceAsync(entry));

        RemoveItemCommand = new Command<CharacterItemEntryVm>(RemoveItem);

        AddSpecialistListCommand = new Command(AddSpecialistList);
        RemoveSpellListCommand = new Command<SpellListVm>(RemoveSpellList);

        AddMiracleListCommand = new Command(AddMiracleList);
        RemoveMiracleListCommand = new Command<MiracleListVm>(RemoveMiracleList);

        SaveCommand = new Command(SaveDraft, () => CanSave);

        ExportSpellsToExcelCommand = new Command(async () => await ExportSpellsToExcelAsync());
        CopySpellsCommand = new Command(async () => await CopySpellsToClipboardAsync());
        SaveSpellsTextCommand = new Command(async () => await SaveSpellsToTextAsync());

        ExportMiraclesToExcelCommand = new Command(async () => await ExportMiraclesToExcelAsync());
        CopyMiraclesCommand = new Command(async () => await CopyMiraclesToClipboardAsync());
        SaveMiraclesTextCommand = new Command(async () => await SaveMiraclesToTextAsync());

        ApplyDraftClassTabVisibilityFallback();
    }

    public CharacterDraft Draft => _draft;

    public int Points
    {
        get => _draft.Points;
        set
        {
            if (_draft.Points == value) return;
            _draft.Points = value;
            Raise();
            Raise(nameof(AbilityPointsSummary));
            Raise(nameof(MultiClassSlotsAvailable));
            Raise(nameof(MultiClassEmptyStateText));
            foreach (var list in MiracleLists)
                list.RefreshExternalLimits();
        }
    }

    public int CurrentVitae
    {
        get => _draft.CurrentVitae;
        set
        {
            if (!CanEditCurrentVitae)
            {
                if (_draft.CurrentVitae != 0 || !_draft.HasSetCurrentVitae)
                {
                    _draft.CurrentVitae = 0;
                    _draft.HasSetCurrentVitae = true;
                    Raise();
                }

                Raise(nameof(CurrentVitaeDisplayText));
                return;
            }

            if (_draft.CurrentVitae == value) return;
            _draft.CurrentVitae = value;
            _draft.HasSetCurrentVitae = true;
            Raise();
            Raise(nameof(CurrentVitaeDisplayText));
        }
    }

    public bool CanEditCurrentVitae => !IsNightsoilRace(_draft.Race);
    public string CurrentVitaeDisplayText => CanEditCurrentVitae
        ? _draft.CurrentVitae.ToString()
        : "nightsoil";

    public string Notes
    {
        get => _draft.Notes;
        set
        {
            if (_draft.Notes == value) return;
            _draft.Notes = value ?? string.Empty;
            Raise();
        }
    }

    public Dictionary<string, ManuAbilityOption> AbilityOptions
    {
        get => _abilityOptions;
        private set => Set(ref _abilityOptions, value);
    }

    private ManuAbilityOption? _selectedAbilityOption;
    public ManuAbilityOption? SelectedAbilityOption
    {
        get => _selectedAbilityOption;
        set
        {
            if (!Set(ref _selectedAbilityOption, value)) return;
            Raise(nameof(CanAddAbility));
        }
    }

    public bool CanAddAbility => !string.IsNullOrWhiteSpace(SelectedAbilityOption?.Name);

    public ObservableCollection<AbilityEntryVm> Abilities { get; } = new();
    public ObservableCollection<MultiClassEntryVm> MultiClasses { get; } = new();
    private MultiRaceEntryVm? _multiRaceSelection;
    public MultiRaceEntryVm? MultiRaceSelection
    {
        get => _multiRaceSelection;
        private set
        {
            if (!Set(ref _multiRaceSelection, value))
                return;

            Raise(nameof(HasMultiRace));
            Raise(nameof(HasMultiRaceChoiceSets));
            Raise(nameof(MultiRaceEmptyStateText));
        }
    }

    public ICommand AddAbilityCommand { get; }
    public ICommand RemoveAbilityCommand { get; }
    public ICommand RemoveMultiClassCommand { get; }
    public ICommand RemoveMultiRaceCommand { get; }

    public int MultiClassPointsSpent => MultiClasses.Sum(entry => entry.Cost);
    public int MultiRacePointsSpent => MultiRaceSelection?.Cost ?? 0;
    public int AbilityPointsSpent => Abilities.Sum(a => a.Cost) + MultiClassPointsSpent + MultiRacePointsSpent;
    public string AbilityPointsSummary => $"Spent: {AbilityPointsSpent} / {Points} Pts";
    public bool HasMultiClasses => MultiClasses.Count > 0;
    public bool HasMultiClassChoiceSets => MultiClasses.Any(entry => entry.HasChoiceSets);
    public bool HasMultiRace => MultiRaceSelection != null;
    public bool HasMultiRaceChoiceSets => MultiRaceSelection?.HasChoiceSets == true;
    public bool HasItems => Items.Count > 0;
    public int MultiClassSlotsAvailable => CalculateMultiClassSlots(Points);
    public string MultiClassEmptyStateText => $"Multi-class slots available: {MultiClassSlotsAvailable}";
    public string MultiRaceEmptyStateText => "Multi-race slots available: 1";

    public ObservableCollection<CharacterItemEntryVm> Items { get; } = new();
    public ICommand RemoveItemCommand { get; }

    public ObservableCollection<SpellListVm> SpellLists { get; } = new();
    public ObservableCollection<SpellListVm> SpecialistSpellLists { get; } = new();
    private SpellListVm? _baseSpellList;
    public SpellListVm? BaseSpellList
    {
        get => _baseSpellList;
        private set
        {
            if (!Set(ref _baseSpellList, value)) return;
            Raise(nameof(HasBaseSpellList));
        }
    }
    public bool HasBaseSpellList => BaseSpellList != null;
    public ICommand AddSpecialistListCommand { get; }
    public ICommand RemoveSpellListCommand { get; }

    public ObservableCollection<MiracleListVm> MiracleLists { get; } = new();
    public ICommand AddMiracleListCommand { get; }
    public ICommand RemoveMiracleListCommand { get; }

    private EvilStairwayVm? _evilStairway;
    public EvilStairwayVm? EvilStairway { get => _evilStairway; private set => Set(ref _evilStairway, value); }

    public ObservableCollection<EvocationListVm> EvocationLists { get; } = new();

    public ICommand SaveCommand { get; }
    public ICommand ExportSpellsToExcelCommand { get; }
    public ICommand CopySpellsCommand { get; }
    public ICommand SaveSpellsTextCommand { get; }
    public ICommand ExportMiraclesToExcelCommand { get; }
    public ICommand CopyMiraclesCommand { get; }
    public ICommand SaveMiraclesTextCommand { get; }

    public bool CanAddMiracleList
        => GetBaseMiracleList() == null
           || (GetBaseMiracleList()?.IsSaved == true && GetScripturesMiracleList() == null);

    public string AddMiracleListLabel
        => GetBaseMiracleList() == null ? "+ Add miracle list" : "Add Scriptures of Faith";

    public bool CanSave =>
        _validationService.CanSave(
            hasMiracleAlignmentIssues: MiracleLists.Any(m => !m.IsAlignmentCompatible(_draft.Alignment)),
            showEvilStairway: ShowEvilStairway,
            hasEvilStairwayValidationError: EvilStairway?.HasValidationError == true);

    private static int CalculateMultiClassSlots(int points)
    {
        if (points <= 449)
            return 0;

        return ((points - 450) / 1000) + 1;
    }

    public async Task InitializeAsync()
    {
        ApplyRaceSpecificVitaeRules();
        var referenceData = await _dataProvider.LoadReferenceDataAsync();
        await _abilityService.WarmCachesAsync();
        ApplyReferenceData(referenceData);
        await LoadDraftStateAsync();
        FinalizeInitialization();
    }

    private void ApplyRaceSpecificVitaeRules()
    {
        var previous = _draft.CurrentVitae;
        var hadValue = _draft.HasSetCurrentVitae;

        if (IsNightsoilRace(_draft.Race))
        {
            _draft.CurrentVitae = 0;
            _draft.HasSetCurrentVitae = true;
        }
        else if (!_draft.HasSetCurrentVitae)
        {
            _draft.CurrentVitae = 100;
            _draft.HasSetCurrentVitae = true;
        }

        if (_draft.CurrentVitae != previous || _draft.HasSetCurrentVitae != hadValue)
            Raise(nameof(CurrentVitae));

        Raise(nameof(CanEditCurrentVitae));
        Raise(nameof(CurrentVitaeDisplayText));
    }

    private static bool IsNightsoilRace(string? raceName)
    {
        var normalized = (raceName ?? string.Empty).Trim();
        return string.Equals(normalized, NightsoilRaceName, StringComparison.OrdinalIgnoreCase);
    }

    private async Task LoadDraftStateAsync()
    {
        LoadAbilitiesFromDraft();
        LoadItemsFromDraft();
        await RefreshMultiClassesAsync();
        await RefreshMultiRaceAsync();
        UpdateTabVisibility();
        if (ShowSpellsTab)
            EnsureWizardSpellListImported();
        LoadSpellListsFromDraft();
        await EnsureWizardBaseListHasEntriesAsync();
        LoadMiracleListsFromDraft();
        await LoadEvilStairwayAsync();
        LoadEvocationListsFromDraft();
    }

    private void FinalizeInitialization()
    {
        Raise(nameof(CanSave));
        (SaveCommand as Command)?.ChangeCanExecute();
        UpdateAbilityPoints();
    }

    private void ApplyReferenceData(AdvanceCharacterReferenceData referenceData)
    {
        _guilds = new Dictionary<string, GuildRecord>(
            referenceData.Guilds ?? new Dictionary<string, GuildRecord>(StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
        _classes = new Dictionary<string, ServiceCharacterClassRecord>(
            referenceData.Classes ?? new Dictionary<string, ServiceCharacterClassRecord>(StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
        _races = new Dictionary<string, PeopleRecord>(
            referenceData.Races ?? new Dictionary<string, PeopleRecord>(StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);

        _allMiracles = referenceData.Miracles ?? Array.Empty<MiracleService.MiracRaw>();
        _allSpells = referenceData.Spells ?? Array.Empty<SpellService.SpellRaw>();
        _allEvocations = referenceData.Evocations ?? Array.Empty<DruidEvocationService.EvocRaw>();

        var abilityEntries = referenceData.Abilities ?? Array.Empty<ManuAbilityService.ManuAbilityEntry>();
        _abilityOptionsByName = _abilityService.BuildAbilityOptionsByName(abilityEntries, _draft, _classes, _races);
        AbilityOptions = _abilityService.BuildAbilityOptionsWithLabels(_abilityOptionsByName.Values);
        _hasLoadedReferenceData = true;
    }

    private async Task<bool> EnsureSpellCatalogueLoadedAsync(bool forceReload = false)
    {
        if (!forceReload && _allSpells.Count > 0)
            return true;

        _allSpells = await _dataProvider.LoadSpellsAsync();

        System.Diagnostics.Debug.WriteLine($"[ADVANCE][SPELLS] Loaded spell catalogue count: {_allSpells.Count}");
        return _allSpells.Count > 0;
    }

    private async Task EnsureWizardBaseListHasEntriesAsync()
    {
        if (!ShowSpellsTab)
            return;

        const int maxAttempts = 12;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var baseList = FindBaseSpellList();
            if ((baseList?.Entries?.Count ?? 0) > 0)
                return;

            var forceReload = attempt > 0 || _allSpells.Count == 0;
            if (!await EnsureSpellCatalogueLoadedAsync(forceReload))
            {
                if (attempt < maxAttempts - 1)
                    await Task.Delay(500);

                continue;
            }

            EnsureWizardSpellListImported();
            LoadSpellListsFromDraft();

            baseList = FindBaseSpellList();
            if ((baseList?.Entries?.Count ?? 0) > 0)
                return;

            if (attempt < maxAttempts - 1)
                await Task.Delay(500);
        }

        System.Diagnostics.Debug.WriteLine("[ADVANCE][SPELLS] Base spell list is still empty after retries.");
    }
    private static readonly MagicColours[] GreyWizardColours =
    {
        MagicColours.Blue, MagicColours.Black, MagicColours.White,
        MagicColours.Green, MagicColours.Red, MagicColours.Brown
    };
    private const string SecondColourAbilityName = "Second Colour";
    private const string SecondColorAbilityName = "Second Color";
    private const string CompetenceAbilityName = "Competence";
    private const string FaerieColourSelectionKey = "Faerie Colour";
    private const string ElfColourAbilitiesSelectionKey = "ElfColourAbilities";
    private const string WizardColourChoiceSetRef = "choice.wizard-colour.primary";
    private const string PowerMasterColourChoiceSetRef = "choice.power-master-colour.primary";
    private const string VivomancerColourChoiceSetRef = "choice.vivomancer-colour.primary";

    private ServiceCharacterClassRecord? ResolveClassRecord()
    {
        var className = (_draft.Class ?? string.Empty).Trim();
        if (className.Length == 0 || _classes == null || _classes.Count == 0)
            return null;

        if (_classes.TryGetValue(className, out var record))
            return record;

        var normalizedClassName = NormalizeClassKey(className);

        foreach (var kvp in _classes)
        {
            if (string.Equals(kvp.Key, className, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;

            if (NormalizeClassKey(kvp.Key) == normalizedClassName)
                return kvp.Value;
        }

        // Handle colour-prefixed names such as "Brown Wizard" from older or derived records.
        foreach (var kvp in _classes)
        {
            if (className.EndsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;

            var normalizedKey = NormalizeClassKey(kvp.Key);
            if (normalizedClassName.EndsWith(normalizedKey, StringComparison.Ordinal))
                return kvp.Value;
        }

        return null;
    }

    private static string NormalizeClassKey(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var chars = raw.Trim().ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray();
        return new string(chars);
    }

    public Dictionary<MagicColours, int> GetFreeSpecialistSlotsByColour()
    {
        var classRecord = ResolveClassRecord();

        var result = new Dictionary<MagicColours, int>();
        if (IsVivomancerClass())
            return result;

        var selectedColours = GetWizardColourSelections();
        var primaryColour = GetPrimaryWizardColour(selectedColours);

        if (IsWarlockClass())
        {
            if (primaryColour.HasValue && primaryColour.Value == MagicColours.Grey)
            {
                foreach (var c in GreyWizardColours)
                    result[c] = 1;
                return result;
            }

            if (primaryColour.HasValue)
                result[primaryColour.Value] = 3;
            else
                result[MagicColours.Grey] = 3;

            return result;
        }

        if (!IsWizardTrackClass(classRecord))
        {
            if (HasAdvancementAbilityByName(CompetenceAbilityName))
            {
                foreach (var c in GreyWizardColours)
                    result[c] = 3;
            }

            return result;
        }

        if (primaryColour.HasValue && primaryColour.Value == MagicColours.Grey)
        {
            foreach (var c in GreyWizardColours)
                result[c] = 3;
            return result;
        }

        if (primaryColour.HasValue)
            result[primaryColour.Value] = 5;
        else
            result[MagicColours.Grey] = 5;

        return result;
    }

    private bool IsVivomancerClass()
        => (_draft.Class ?? string.Empty).Contains("Vivomancer", StringComparison.OrdinalIgnoreCase);

    private bool IsWarlockClass()
    {
        var className = (_draft.Class ?? string.Empty).Trim();
        return className.Contains("Warlock", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsWizardTrackClass(ServiceCharacterClassRecord? classRecord)
    {
        var className = (_draft.Class ?? string.Empty).Trim();
        if (className.Length > 0)
        {
            var hasWizardToken = className.Contains("Wizard", StringComparison.OrdinalIgnoreCase)
                                 || className.Contains("Sorc", StringComparison.OrdinalIgnoreCase);
            var isExcluded = className.Contains("Warlock", StringComparison.OrdinalIgnoreCase)
                             || className.Contains("Vochstelen", StringComparison.OrdinalIgnoreCase)
                             || className.Contains("Vivomancer", StringComparison.OrdinalIgnoreCase);

            if (hasWizardToken && !isExcluded)
                return true;
        }

        return HasBracket(classRecord?.Brackets, "Wizard")
               && !HasBracket(classRecord?.Brackets, "Warrior")
               && !HasBracket(classRecord?.Brackets, "Priest");
    }

    private void UpdateTabVisibility()
    {
        var classRecord = ResolveClassRecord();
        var state = _tabVisibilityService.Resolve(_draft.Class, classRecord);
        var hasWizardTrackViaMultiClass = HasWizardTrackMultiClass();
        var hasWizardColourViaMultiClass = GetMultiClassWizardColourSelections().Count > 0;

        ShowSpellsTab = state.ShowSpellsTab || hasWizardTrackViaMultiClass || hasWizardColourViaMultiClass;
        ShowMiraclesTab = state.ShowMiraclesTab;
        ShowPriestMiracleLists = state.ShowPriestMiracleLists;
        ShowEvilStairway = state.ShowEvilStairway;
        ShowEvocationsTab = state.ShowEvocationsTab;
    }

    private void ApplyDraftClassTabVisibilityFallback()
    {
        var current = new AdvancementTabState(
            ShowSpellsTab,
            ShowMiraclesTab,
            ShowPriestMiracleLists,
            ShowEvilStairway,
            ShowEvocationsTab);

        var state = _tabVisibilityService.ApplyNameFallback(_draft.Class, current);
        ShowSpellsTab = state.ShowSpellsTab;
        ShowMiraclesTab = state.ShowMiraclesTab;
        ShowPriestMiracleLists = state.ShowPriestMiracleLists;
        ShowEvilStairway = state.ShowEvilStairway;
        ShowEvocationsTab = state.ShowEvocationsTab;
    }

    private static bool HasBracket(IEnumerable<string>? brackets, string token)
        => brackets != null && brackets.Any(b => b.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static bool HasClassAbility(ServiceCharacterClassRecord? classRecord, params string[] names)
    {
        if (classRecord?.Levels == null || names.Length == 0)
            return false;

        foreach (var level in classRecord.Levels.Values)
        {
            if (level == null) continue;
            foreach (var ability in level)
            {
                var name = ability?.Name?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                foreach (var token in names)
                {
                    if (string.Equals(name, token, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
        }

        return false;
    }

    private void LoadAbilitiesFromDraft()
    {
        _suppressAbilityReactions = true;
        try
        {
            Abilities.Clear();
            foreach (var ability in _draft.AdvancementAbilities ?? new List<string>())
            {
                var line = BuildAbilityEntry(ability);
                Abilities.Add(line);
            }
        }
        finally
        {
            _suppressAbilityReactions = false;
        }

        SyncAbilitiesToDraft();
        UpdateAbilityPoints();
    }

    private void AddAbility()
    {
        if (SelectedAbilityOption == null)
            return;

        var name = SelectedAbilityOption.Value.Name ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            return;

        var abilityKey = name.Trim();
        var resolved = _abilityService.TryResolveAbilityDetails(name);
        if (resolved != null)
            abilityKey = AbilityKey.Build(resolved);

        var line = new AbilityEntryVm(
            name,
            SelectedAbilityOption.Value.Cost,
            abilityKey,
            OnAdvancementAbilityEntryChanged);
        Abilities.Add(line);
        SelectedAbilityOption = null;
    }

    public void AddAdvancementAbilities(IEnumerable<EvolutionService.AbilityResult>? abilities)
    {
        if (abilities == null)
            return;

        foreach (var ability in abilities)
        {
            var displayName = EvolutionService.NormalizeAbilityDisplayText(ability.Index);
            if (string.IsNullOrWhiteSpace(displayName))
                continue;

            var normalizedCost = _abilityService.ApplyRaceAbilityCostModifiers(
                Math.Max(0, ability.Cost),
                displayName,
                ability,
                ability.AbilityRef,
                _draft,
                _races);
            var normalizedTable = Math.Max(0, ability.Table);

            var abilityKey = AbilityKey.Build(ability);
            Abilities.Add(new AbilityEntryVm(displayName, normalizedCost, abilityKey, OnAdvancementAbilityEntryChanged));

            _abilityOptionsByName[displayName] = new ManuAbilityOption(
                displayName,
                normalizedCost,
                normalizedTable,
                ability.Available ?? string.Empty,
                ability.AvailabilityRules ?? Array.Empty<RuleClause>(),
                ability.Description ?? string.Empty);

            _abilityService.CacheAbilityDetails(ability);
        }

        SelectedAbilityOption = null;
    }

    private void RemoveAbility(AbilityEntryVm? ability)
    {
        if (ability == null) return;
        Abilities.Remove(ability);
    }

    private void OnAdvancementAbilityCollectionChanged()
    {
        if (_suppressAbilityReactions)
            return;

        SyncAbilitiesToDraft();
        UpdateAbilityPoints();
        RefreshWizardSpellAccessFromAdvancementAbilities();
    }

    private void OnAdvancementAbilityEntryChanged()
    {
        if (_suppressAbilityReactions)
            return;

        SyncAbilitiesToDraft();
        UpdateAbilityPoints();
        RefreshWizardSpellAccessFromAdvancementAbilities();
    }

    private void OnMultiClassCollectionChanged()
    {
        Raise(nameof(HasMultiClasses));
        Raise(nameof(HasMultiClassChoiceSets));
        Raise(nameof(MultiClassEmptyStateText));
        UpdateAbilityPoints();
    }

    private void RefreshWizardSpellAccessFromAdvancementAbilities()
    {
        if (!ShowSpellsTab)
            return;

        EnsureWizardSpellListImported();
        LoadSpellListsFromDraft();
    }

    private void SyncAbilitiesToDraft()
    {
        _draft.AdvancementAbilities = Abilities
            .Select(ResolveCanonicalAbilityKey)
            .Where(t => t.Length > 0)
            .ToList();
    }

    private string ResolveCanonicalAbilityKey(AbilityEntryVm? entry)
    {
        if (entry == null)
            return string.Empty;

        var keyCandidate = (entry.AbilityKey ?? string.Empty).Trim();
        var nameCandidate = (entry.Name ?? string.Empty).Trim();

        var resolved = _abilityService.TryResolveAbilityDetails(keyCandidate)
                       ?? _abilityService.TryResolveAbilityDetails(nameCandidate);
        if (resolved != null)
            return AbilityKey.Build(resolved);

        if (keyCandidate.Length > 0)
            return keyCandidate;
        if (nameCandidate.Length > 0)
            return nameCandidate;

        return string.Empty;
    }

    private AbilityEntryVm BuildAbilityEntry(string rawKeyOrName)
    {
        var trimmed = (rawKeyOrName ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return new AbilityEntryVm(string.Empty, 0, string.Empty, OnAdvancementAbilityEntryChanged);

        var resolved = _abilityService.TryResolveAbilityDetails(trimmed);
        if (resolved != null)
        {
            var displayName = EvolutionService.NormalizeAbilityDisplayText(resolved.Index);
            var abilityKey = AbilityKey.Build(resolved);
            var adjustedCost = _abilityService.ApplyRaceAbilityCostModifiers(
                Math.Max(0, resolved.Cost),
                displayName,
                resolved,
                resolved.AbilityRef,
                _draft,
                _races);
            return new AbilityEntryVm(
                displayName,
                adjustedCost,
                abilityKey,
                OnAdvancementAbilityEntryChanged);
        }

        if (_abilityOptionsByName.TryGetValue(trimmed, out var option))
            return new AbilityEntryVm(trimmed, Math.Max(0, option.Cost), trimmed, OnAdvancementAbilityEntryChanged);

        return new AbilityEntryVm(trimmed, 0, trimmed, OnAdvancementAbilityEntryChanged);
    }

    private void UpdateAbilityPoints()
    {
        var running = 0;
        var rowIndex = 0;
        foreach (var entry in Abilities)
        {
            running += entry.Cost;
            entry.SetRunningTotal(running);
            entry.SetRowIndex(rowIndex++);
        }
        Raise(nameof(MultiClassPointsSpent));
        Raise(nameof(MultiRacePointsSpent));
        Raise(nameof(AbilityPointsSpent));
        Raise(nameof(AbilityPointsSummary));
    }

    public async Task<Dictionary<string, ManuAbilityOption>> SearchAbilityOptionsAsync(string query)
    {
        var results = await _abilityService.SearchAbilityOptionsAsync(query, _draft, _classes, _races);
        foreach (var kvp in results)
            _abilityOptionsByName[kvp.Key] = kvp.Value;

        return results;
    }

    public async Task<EvolutionService.AbilityResult?> FindAbilityByNameAsync(string? abilityName)
    {
        return await _abilityService.FindAbilityByNameAsync(abilityName);
    }

    public SpellService.SpellRaw? FindSpellByName(string? spellName)
    {
        var name = (spellName ?? string.Empty).Trim();
        if (name.Length == 0)
            return null;

        return _allSpells.FirstOrDefault(s =>
            string.Equals((s?.name ?? string.Empty).Trim(), name, StringComparison.OrdinalIgnoreCase));
    }

    public MiracleService.MiracRaw? FindMiracleByName(string? miracleName)
    {
        var name = (miracleName ?? string.Empty).Trim();
        if (name.Length == 0)
            return null;

        return _allMiracles.FirstOrDefault(m =>
            string.Equals((m?.name ?? string.Empty).Trim(), name, StringComparison.OrdinalIgnoreCase));
    }

    public DruidEvocationService.EvocRaw? FindEvocationByName(string? evocationName)
    {
        var name = (evocationName ?? string.Empty).Trim();
        if (name.Length == 0)
            return null;

        return _allEvocations.FirstOrDefault(e =>
            string.Equals((e?.name ?? string.Empty).Trim(), name, StringComparison.OrdinalIgnoreCase));
    }

    public void PersistDraft()
    {
        _draftStore.Save();
    }

    public void RefreshItems()
    {
        LoadItemsFromDraft();
    }

    private void LoadItemsFromDraft()
    {
        Items.Clear();
        foreach (var assigned in LiteDbService.GetItemsAssignedToCharacter(
                     _draft.CharacterRecordId,
                     _draft.Name,
                     _draft.PlayerName))
        {
            Items.Add(new CharacterItemEntryVm(assigned));
        }

        SyncItemsToDraft();
        Raise(nameof(HasItems));
    }

    public async Task RefreshMultiClassesAsync()
    {
        if (!_hasLoadedReferenceData)
            return;

        _draft.MultiClassLevels ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        _draft.MultiClassChoiceSelections ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var catalog = await MultiClassService.GetCatalogAsync();
        _multiClassDefinitionsByKey.Clear();
        foreach (var pair in catalog.MultiClasses)
        {
            var key = (pair.Key ?? string.Empty).Trim();
            if (key.Length == 0)
                continue;

            _multiClassDefinitionsByKey[key] = pair.Value;
        }

        var normalizedSelections = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var selection in _draft.MultiClassLevels)
        {
            var rawKey = (selection.Key ?? string.Empty).Trim();
            if (rawKey.Length == 0 || selection.Value <= 0)
                continue;

            if (!TryResolveMultiClassDefinition(rawKey, out var resolvedKey, out var definition))
                continue;

            var maxLevel = ResolveMultiClassMaxLevel(definition);
            var resolvedLevel = Math.Clamp(selection.Value, 0, maxLevel);
            if (resolvedLevel <= 0)
                continue;

            if (normalizedSelections.TryGetValue(resolvedKey, out var existingLevel) && existingLevel >= resolvedLevel)
                continue;

            normalizedSelections[resolvedKey] = resolvedLevel;
        }

        if (!AreMultiClassSelectionsEqual(_draft.MultiClassLevels, normalizedSelections))
            _draft.MultiClassLevels = normalizedSelections;

        var nextEntries = new List<MultiClassEntryVm>();
        var validChoiceSelectionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var selection in normalizedSelections.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!TryResolveMultiClassDefinition(selection.Key, out var resolvedKey, out var definition))
                continue;

            var selectedLevel = selection.Value;
            var maxLevel = ResolveMultiClassMaxLevel(definition);
            var option = FindAvailableMultiClassOption(definition.AvailabilityOptions);
            var totalCost = CalculateMultiClassTotalCost(option, selectedLevel);
            var abilityLinks = BuildMultiClassAbilityLinks(definition, selectedLevel);
            var choiceSetRefs = CollectMultiClassChoiceSetRefs(definition, selectedLevel);
            var displayName = ResolveMultiClassDisplayName(definition, resolvedKey);

            foreach (var choiceSetRef in choiceSetRefs)
                validChoiceSelectionKeys.Add(BuildMultiClassChoiceSelectionKey(resolvedKey, choiceSetRef));

            nextEntries.Add(new MultiClassEntryVm(
                key: resolvedKey,
                name: displayName,
                level: selectedLevel,
                maxLevel: maxLevel,
                cost: totalCost,
                abilityDetails: abilityLinks,
                choiceSetRefs: choiceSetRefs));
        }

        var staleChoiceSelectionKeys = _draft.MultiClassChoiceSelections.Keys
            .Where(key => !validChoiceSelectionKeys.Contains(key))
            .ToList();
        foreach (var staleChoiceSelectionKey in staleChoiceSelectionKeys)
            _draft.MultiClassChoiceSelections.Remove(staleChoiceSelectionKey);

        MultiClasses.Clear();
        foreach (var entry in nextEntries.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
            MultiClasses.Add(entry);

        UpdateTabVisibility();
        if (ShowSpellsTab)
        {
            EnsureWizardSpellListImported();
            LoadSpellListsFromDraft();
        }

        Raise(nameof(MultiClassPointsSpent));
        Raise(nameof(AbilityPointsSpent));
        Raise(nameof(AbilityPointsSummary));
    }

    public async Task RefreshMultiRaceAsync()
    {
        if (!_hasLoadedReferenceData)
            return;

        var catalog = await MultiRaceService.GetCatalogAsync();
        _multiRaceDefinitionsByKey.Clear();
        foreach (var pair in catalog.MultiRaces)
        {
            var key = (pair.Key ?? string.Empty).Trim();
            if (key.Length == 0)
                continue;

            _multiRaceDefinitionsByKey[key] = pair.Value;
        }

        var keySelection = (_draft.MultiRaceKey ?? string.Empty).Trim();
        if (keySelection.Length == 0 || _draft.MultiRaceLevel <= 0)
        {
            MultiRaceSelection = null;
            Raise(nameof(MultiRacePointsSpent));
            Raise(nameof(AbilityPointsSpent));
            Raise(nameof(AbilityPointsSummary));
            return;
        }

        if (!TryResolveMultiRaceDefinition(keySelection, out var resolvedKey, out var definition))
        {
            _draft.MultiRaceKey = string.Empty;
            _draft.MultiRaceLevel = 0;
            _draft.MultiRaceChoiceSelections ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _draft.MultiRaceChoiceSelections.Clear();
            MultiRaceSelection = null;
            Raise(nameof(MultiRacePointsSpent));
            Raise(nameof(AbilityPointsSpent));
            Raise(nameof(AbilityPointsSummary));
            return;
        }

        var maxLevel = ResolveMultiRaceMaxLevel(definition);
        var resolvedLevel = Math.Clamp(_draft.MultiRaceLevel, 0, maxLevel);
        if (resolvedLevel <= 0)
        {
            _draft.MultiRaceKey = string.Empty;
            _draft.MultiRaceLevel = 0;
            MultiRaceSelection = null;
            Raise(nameof(MultiRacePointsSpent));
            Raise(nameof(AbilityPointsSpent));
            Raise(nameof(AbilityPointsSummary));
            return;
        }

        _draft.MultiRaceKey = resolvedKey;
        _draft.MultiRaceLevel = resolvedLevel;

        var option = FindAvailableMultiRaceOption(definition.AvailabilityOptions);
        var totalCost = CalculateMultiRaceTotalCost(option, resolvedLevel);
        var abilityLinks = BuildMultiRaceAbilityLinks(definition, resolvedLevel);
        var choiceSetRefs = CollectMultiRaceChoiceSetRefs(definition, resolvedLevel);
        var displayName = ResolveMultiRaceDisplayName(definition, resolvedKey);

        _draft.MultiRaceChoiceSelections ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var staleKeys = _draft.MultiRaceChoiceSelections.Keys
            .Where(key => !choiceSetRefs.Contains(key, StringComparer.OrdinalIgnoreCase))
            .ToList();
        foreach (var stale in staleKeys)
            _draft.MultiRaceChoiceSelections.Remove(stale);

        MultiRaceSelection = new MultiRaceEntryVm(
            key: resolvedKey,
            name: displayName,
            level: resolvedLevel,
            maxLevel: maxLevel,
            cost: totalCost,
            abilityDetails: abilityLinks,
            choiceSetRefs: choiceSetRefs);

        UpdateTabVisibility();
        if (ShowSpellsTab)
        {
            EnsureWizardSpellListImported();
            LoadSpellListsFromDraft();
        }

        Raise(nameof(MultiRacePointsSpent));
        Raise(nameof(AbilityPointsSpent));
        Raise(nameof(AbilityPointsSummary));
    }

    public async Task RemoveMultiClassAsync(MultiClassEntryVm? entry)
    {
        if (entry == null)
            return;

        _draft.MultiClassLevels ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        _draft.MultiClassLevels.Remove(entry.Key);
        await RefreshMultiClassesAsync();
    }

    public async Task RemoveMultiRaceAsync(MultiRaceEntryVm? entry)
    {
        if (entry == null)
            return;

        _draft.MultiRaceKey = string.Empty;
        _draft.MultiRaceLevel = 0;
        _draft.MultiRaceChoiceSelections ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _draft.MultiRaceChoiceSelections.Clear();
        await RefreshMultiRaceAsync();
    }

    public IReadOnlyList<MultiClassAbilityLinkVm> GetMultiClassAbilityDetails(MultiClassEntryVm? entry)
        => entry?.AbilityDetails ?? Array.Empty<MultiClassAbilityLinkVm>();

    public IReadOnlyList<MultiClassAbilityLinkVm> GetMultiRaceAbilityDetails(MultiRaceEntryVm? entry)
        => entry?.AbilityDetails ?? Array.Empty<MultiClassAbilityLinkVm>();

    private bool TryResolveMultiRaceDefinition(
        string storedKey,
        out string resolvedKey,
        out MultiRaceDefinition definition)
    {
        resolvedKey = string.Empty;
        definition = null!;

        var key = (storedKey ?? string.Empty).Trim();
        if (key.Length == 0)
            return false;

        if (_multiRaceDefinitionsByKey.TryGetValue(key, out definition))
        {
            resolvedKey = key;
            return true;
        }

        var normalized = NormalizeMultiClassToken(key);
        foreach (var pair in _multiRaceDefinitionsByKey)
        {
            if (NormalizeMultiClassToken(pair.Key).Equals(normalized, StringComparison.OrdinalIgnoreCase))
            {
                resolvedKey = pair.Key;
                definition = pair.Value;
                return true;
            }

            var displayName = (pair.Value.DisplayName ?? string.Empty).Trim();
            if (displayName.Length == 0)
                continue;

            if (NormalizeMultiClassToken(displayName).Equals(normalized, StringComparison.OrdinalIgnoreCase))
            {
                resolvedKey = pair.Key;
                definition = pair.Value;
                return true;
            }
        }

        return false;
    }

    private MultiRaceAvailabilityOption? FindAvailableMultiRaceOption(IEnumerable<MultiRaceAvailabilityOption>? options)
    {
        foreach (var option in options ?? Enumerable.Empty<MultiRaceAvailabilityOption>())
        {
            var rules = option.Rules ?? new List<RuleClause>();
            if (_abilityAvailabilityService.IsAvailable(rules, _draft, _classes, _races))
                return option;
        }

        return options?.FirstOrDefault();
    }

    private static int ResolveMultiRaceMaxLevel(MultiRaceDefinition definition)
    {
        if (definition.MaxLevel > 0)
            return definition.MaxLevel;

        var parsedMax = definition.Levels.Keys
            .Select(level => int.TryParse(level, out var parsed) ? parsed : 0)
            .DefaultIfEmpty(0)
            .Max();

        return Math.Max(1, parsedMax);
    }

    private static int CalculateMultiRaceTotalCost(MultiRaceAvailabilityOption? option, int selectedLevel)
    {
        if (selectedLevel <= 0)
            return 0;

        var total = 0;
        for (var level = 1; level <= selectedLevel; level++)
        {
            if (option?.CostsByLevel != null
                && option.CostsByLevel.TryGetValue(level.ToString(), out var cost))
            {
                total += Math.Max(0, cost);
            }
        }

        return total;
    }

    private static IReadOnlyList<MultiClassAbilityLinkVm> BuildMultiRaceAbilityLinks(MultiRaceDefinition definition, int selectedLevel)
    {
        if (selectedLevel <= 0)
            return Array.Empty<MultiClassAbilityLinkVm>();

        var links = new List<MultiClassAbilityLinkVm>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var level = 1; level <= selectedLevel; level++)
        {
            if (!definition.Levels.TryGetValue(level.ToString(), out var levelAbilities) || levelAbilities == null)
                continue;

            foreach (var ability in levelAbilities)
            {
                var displayName = (ability?.Name ?? string.Empty).Trim();
                var lookupKey = !string.IsNullOrWhiteSpace((ability?.AbilityRef ?? string.Empty).Trim())
                    ? ability.AbilityRef.Trim()
                    : displayName;

                if (displayName.Length == 0 && lookupKey.Length == 0)
                    continue;

                if (displayName.Length == 0)
                    displayName = lookupKey;
                if (lookupKey.Length == 0)
                    lookupKey = displayName;

                var dedupeKey = $"{displayName}::{lookupKey}";
                if (!seen.Add(dedupeKey))
                    continue;

                links.Add(new MultiClassAbilityLinkVm(displayName, lookupKey));
            }
        }

        return links;
    }

    private static IReadOnlyList<string> CollectMultiRaceChoiceSetRefs(MultiRaceDefinition definition, int selectedLevel)
    {
        if (selectedLevel <= 0)
            return Array.Empty<string>();

        var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var level = 1; level <= selectedLevel; level++)
        {
            if (!definition.Levels.TryGetValue(level.ToString(), out var levelAbilities) || levelAbilities == null)
                continue;

            foreach (var ability in levelAbilities)
            {
                foreach (var choiceSetRef in ability?.ChoiceSetRefs ?? new List<string>())
                {
                    var key = (choiceSetRef ?? string.Empty).Trim();
                    if (key.Length > 0)
                        refs.Add(key);
                }
            }
        }

        return refs.ToList();
    }

    private static string ResolveMultiRaceDisplayName(MultiRaceDefinition definition, string fallbackKey)
    {
        var name = (definition.DisplayName ?? string.Empty).Trim();
        return name.Length > 0 ? name : fallbackKey;
    }

    private bool TryResolveMultiClassDefinition(
        string storedKey,
        out string resolvedKey,
        out MultiClassDefinition definition)
    {
        resolvedKey = string.Empty;
        definition = null!;

        var key = (storedKey ?? string.Empty).Trim();
        if (key.Length == 0)
            return false;

        if (_multiClassDefinitionsByKey.TryGetValue(key, out definition))
        {
            resolvedKey = key;
            return true;
        }

        var normalized = NormalizeMultiClassToken(key);
        foreach (var pair in _multiClassDefinitionsByKey)
        {
            if (NormalizeMultiClassToken(pair.Key).Equals(normalized, StringComparison.OrdinalIgnoreCase))
            {
                resolvedKey = pair.Key;
                definition = pair.Value;
                return true;
            }

            var displayName = (pair.Value.DisplayName ?? string.Empty).Trim();
            if (displayName.Length == 0)
                continue;

            if (NormalizeMultiClassToken(displayName).Equals(normalized, StringComparison.OrdinalIgnoreCase))
            {
                resolvedKey = pair.Key;
                definition = pair.Value;
                return true;
            }
        }

        return false;
    }

    private MultiClassAvailabilityOption? FindAvailableMultiClassOption(IEnumerable<MultiClassAvailabilityOption>? options)
    {
        foreach (var option in options ?? Enumerable.Empty<MultiClassAvailabilityOption>())
        {
            var rules = option.Rules ?? new List<RuleClause>();
            if (_abilityAvailabilityService.IsAvailable(rules, _draft, _classes, _races))
                return option;
        }

        return options?.FirstOrDefault();
    }

    private static int ResolveMultiClassMaxLevel(MultiClassDefinition definition)
    {
        if (definition.MaxLevel > 0)
            return definition.MaxLevel;

        var parsedMax = definition.Levels.Keys
            .Select(level => int.TryParse(level, out var parsed) ? parsed : 0)
            .DefaultIfEmpty(0)
            .Max();

        return Math.Max(1, parsedMax);
    }

    private static int CalculateMultiClassTotalCost(MultiClassAvailabilityOption? option, int selectedLevel)
    {
        if (selectedLevel <= 0)
            return 0;

        var total = 0;
        for (var level = 1; level <= selectedLevel; level++)
        {
            if (option?.CostsByLevel != null
                && option.CostsByLevel.TryGetValue(level.ToString(), out var cost))
            {
                total += Math.Max(0, cost);
            }
        }

        return total;
    }

    private static IReadOnlyList<MultiClassAbilityLinkVm> BuildMultiClassAbilityLinks(MultiClassDefinition definition, int selectedLevel)
    {
        if (selectedLevel <= 0)
            return Array.Empty<MultiClassAbilityLinkVm>();

        var links = new List<MultiClassAbilityLinkVm>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var level = 1; level <= selectedLevel; level++)
        {
            if (!definition.Levels.TryGetValue(level.ToString(), out var levelAbilities) || levelAbilities == null)
                continue;

            foreach (var ability in levelAbilities)
            {
                var displayName = (ability?.Name ?? string.Empty).Trim();
                var lookupKey = !string.IsNullOrWhiteSpace((ability?.AbilityRef ?? string.Empty).Trim())
                    ? ability.AbilityRef.Trim()
                    : displayName;

                if (displayName.Length == 0 && lookupKey.Length == 0)
                    continue;

                if (displayName.Length == 0)
                    displayName = lookupKey;
                if (lookupKey.Length == 0)
                    lookupKey = displayName;

                var dedupeKey = $"{displayName}::{lookupKey}";
                if (!seen.Add(dedupeKey))
                    continue;

                links.Add(new MultiClassAbilityLinkVm(displayName, lookupKey));
            }
        }

        return links;
    }

    private static IReadOnlyList<string> CollectMultiClassChoiceSetRefs(MultiClassDefinition definition, int selectedLevel)
    {
        if (selectedLevel <= 0)
            return Array.Empty<string>();

        var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var level = 1; level <= selectedLevel; level++)
        {
            if (!definition.Levels.TryGetValue(level.ToString(), out var levelAbilities) || levelAbilities == null)
                continue;

            foreach (var ability in levelAbilities)
            {
                foreach (var choiceSetRef in ability?.ChoiceSetRefs ?? new List<string>())
                {
                    var key = (choiceSetRef ?? string.Empty).Trim();
                    if (key.Length > 0)
                        refs.Add(key);
                }
            }
        }

        return refs.ToList();
    }

    public static string BuildMultiClassChoiceSelectionKey(string multiClassKey, string choiceSetRef)
        => $"{(multiClassKey ?? string.Empty).Trim()}::{(choiceSetRef ?? string.Empty).Trim()}";

    private static string ResolveMultiClassDisplayName(MultiClassDefinition definition, string fallbackKey)
    {
        var name = (definition.DisplayName ?? string.Empty).Trim();
        return name.Length > 0 ? name : fallbackKey;
    }

    private static bool AreMultiClassSelectionsEqual(
        IReadOnlyDictionary<string, int> current,
        IReadOnlyDictionary<string, int> next)
    {
        if (current.Count != next.Count)
            return false;

        foreach (var pair in current)
        {
            if (!next.TryGetValue(pair.Key, out var value))
                return false;

            if (pair.Value != value)
                return false;
        }

        return true;
    }

    private static string NormalizeMultiClassToken(string value)
        => new string((value ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

    private void RemoveItem(CharacterItemEntryVm? item)
    {
        if (item?.Item == null)
            return;

        LiteDbService.DeleteItem(item.Item.Id);
        Items.Remove(item);
        Raise(nameof(HasItems));
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncItemsToDraft();
        Raise(nameof(HasItems));
    }

    private void SyncItemsToDraft()
    {
        _draft.AdvancementItems = Items
            .Select(i => (i.Name ?? string.Empty).Trim())
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private int GetDraftCasterLevel()
    {
        var prop = _draft.GetType().GetProperty("CasterLevel");
        if (prop?.PropertyType == typeof(int))
            return (int)(prop.GetValue(_draft) ?? 0);

        return 0;
    }
    private void LoadSpellListsFromDraft()
    {
        SpellLists.Clear();
        SpecialistSpellLists.Clear();
        BaseSpellList = null;

        if (_draft.SpellLists == null)
            _draft.SpellLists = new List<SpellListDraft>();

        if (ShowSpellsTab)
        {
            NormalizeWizardSpellLists();
            if (HasAnySpecialistSlots())
                EnsureSpecialistSpellList();
            else
                RemoveSpecialistSpellLists();
        }

        var baseDraft = _draft.SpellLists.FirstOrDefault(l => l.IsBaseList);
        var specialistDraft = _draft.SpellLists.FirstOrDefault(l => !l.IsBaseList);
        var ordered = new List<SpellListDraft>();
        if (baseDraft != null)
            ordered.Add(baseDraft);
        if (specialistDraft != null)
            ordered.Add(specialistDraft);
        if (ordered.Count > 0)
            _draft.SpellLists = ordered;

        if (baseDraft != null)
            baseDraft.IsMinimized = true;
        if (specialistDraft != null)
            specialistDraft.IsMinimized = true;

        var specialistFilter = BuildSpecialistSpellFilter();
        foreach (var list in _draft.SpellLists)
        {
            var filter = list.IsBaseList ? null : specialistFilter;
            var vm = new SpellListVm(list, _allSpells, GetDraftCasterLevel, OnSpellListChanged, filter);
            SpellLists.Add(vm);
            if (list.IsBaseList && BaseSpellList == null)
                BaseSpellList = vm;
            else
                SpecialistSpellLists.Add(vm);
        }

        RefreshSpecialistSlots();
        Raise(nameof(CanAddSpecialistList));
    }

    private void NormalizeWizardSpellLists()
    {
        if (_draft.SpellLists == null || _draft.SpellLists.Count == 0)
            return;

        var baseList = _draft.SpellLists.FirstOrDefault(l => l.IsBaseList);
        if (baseList == null)
            return;

        var specialistLists = _draft.SpellLists.Where(l => !ReferenceEquals(l, baseList)).ToList();
        if (specialistLists.Count <= 1)
            return;

        var primary = specialistLists[0];
        primary.Name = "Specialists";
        var existing = new HashSet<string>(
            primary.Entries.Select(e => (e.Name ?? string.Empty).Trim()).Where(n => n.Length > 0),
            StringComparer.OrdinalIgnoreCase);

        foreach (var extra in specialistLists.Skip(1))
        {
            foreach (var entry in extra.Entries ?? new List<SpellListEntryDraft>())
            {
                var name = (entry.Name ?? string.Empty).Trim();
                if (name.Length == 0 || existing.Contains(name))
                    continue;

                primary.Entries.Add(entry);
                existing.Add(name);
            }
        }

        _draft.SpellLists = _draft.SpellLists
            .Where(l => ReferenceEquals(l, baseList) || ReferenceEquals(l, primary))
            .ToList();
    }

    private bool HasAnySpecialistSlots()
        => GetFreeSpecialistSlotsByColour().Values.Sum() > 0;

    private void RemoveSpecialistSpellLists()
    {
        if (_draft.SpellLists == null || _draft.SpellLists.Count == 0)
            return;

        _draft.SpellLists = _draft.SpellLists.Where(l => l.IsBaseList).ToList();
    }

    private SpellListDraft EnsureSpecialistSpellList()
    {
        if (_draft.SpellLists == null)
            _draft.SpellLists = new List<SpellListDraft>();

        var specialist = _draft.SpellLists.FirstOrDefault(l => !l.IsBaseList);
        if (specialist == null)
        {
            specialist = new SpellListDraft
            {
                Name = "Specialists",
                IsBaseList = false,
                IsMinimized = true
            };
            _draft.SpellLists.Add(specialist);
        }
        else
        {
            specialist.Name = "Specialists";
            specialist.IsBaseList = false;
        }

        return specialist;
    }

    private void AddSpecialistList()
    {
        if (!CanAddSpecialistList)
            return;

        if (_draft.SpellLists == null)
            _draft.SpellLists = new List<SpellListDraft>();

        var draft = new SpellListDraft
        {
            Name = "Specialists",
            IsMinimized = true
        };
        _draft.SpellLists.Add(draft);
        var vm = new SpellListVm(draft, _allSpells, GetDraftCasterLevel, OnSpellListChanged, BuildSpecialistSpellFilter());
        SpellLists.Add(vm);
        SpecialistSpellLists.Add(vm);
        RefreshSpecialistSlots();
        Raise(nameof(CanAddSpecialistList));
    }

    private void RemoveSpellList(SpellListVm? list)
    {
        if (list == null || list.IsBaseList) return;
        SpellLists.Remove(list);
        SpecialistSpellLists.Remove(list);
        _draft.SpellLists.Remove(list.Draft);
        RefreshSpecialistSlots();
        Raise(nameof(CanAddSpecialistList));
    }

    private MiracleListVm? GetBaseMiracleList()
        => MiracleLists.FirstOrDefault(m => !m.IsScriptures);

    private MiracleListVm? GetScripturesMiracleList()
        => MiracleLists.FirstOrDefault(m => m.IsScriptures);

    private void LoadMiracleListsFromDraft()
    {
        MiracleLists.Clear();

        if (_draft.MiracleLists == null)
            _draft.MiracleLists = new List<MiracleListDraft>();

        var baseList = _draft.MiracleLists.FirstOrDefault(l => !l.IsScriptures);
        if (baseList == null)
        {
            baseList = TryBuildChurchMiracleList() ?? new MiracleListDraft
            {
                Name = "Base List",
                IsScriptures = false
            };
        }

        var scripturesList = _draft.MiracleLists.FirstOrDefault(l => l.IsScriptures);
        if (scripturesList == null)
        {
            scripturesList = new MiracleListDraft
            {
                Name = "Scriptures of Faith",
                IsScriptures = true
            };
        }

        baseList.IsScriptures = false;
        scripturesList.IsScriptures = true;
        baseList.IsMinimized = true;
        scripturesList.IsMinimized = true;

        _draft.MiracleLists = new List<MiracleListDraft> { baseList, scripturesList };

        foreach (var list in _draft.MiracleLists)
        {
            if (list.IsImported)
            {
                list.IsSaved = true;
                if (string.IsNullOrWhiteSpace(list.SourceName))
                    list.SourceName = ExtractImportedSourceName(list.Name);
            }

            var vm = new MiracleListVm(
                list,
                _allMiracles,
                _domainService,
                OnMiracleListValidationChanged,
                () => _draft.Alignment,
                () => _draft.Points,
                OnMiracleListExpandRequested);
            HookMiracleList(vm);
            MiracleLists.Add(vm);
        }

        NormalizeMiracleListExpansion();
        Raise(nameof(CanAddMiracleList));
        Raise(nameof(AddMiracleListLabel));
    }

    private async Task LoadEvilStairwayAsync()
    {
        if (!ShowEvilStairway)
        {
            EvilStairway = null;
            return;
        }

        _draft.EvilStairwayList ??= new MiracleListDraft
        {
            Name = "Evil Stairway",
            IsScriptures = false,
            IsMinimized = false
        };

        if (string.IsNullOrWhiteSpace(_draft.EvilStairwayList.Name))
            _draft.EvilStairwayList.Name = "Evil Stairway";

        var prereqs = await MiracleTreeService.GetPreReqsAsync();
        var (churchName, miracleNames) = TryGetChurchMiracleNames();

        EvilStairway = new EvilStairwayVm(
            _draft.EvilStairwayList,
            _allMiracles,
            prereqs,
            churchName,
            miracleNames,
            OnEvilStairwayValidationChanged);
    }

    private (string? ChurchName, HashSet<string>? MiracleNames) TryGetChurchMiracleNames()
    {
        if (_draft.Guilds == null || _draft.Guilds.Count == 0)
            return (null, null);

        var church = _draft.Guilds
            .Select(g => g ?? string.Empty)
            .FirstOrDefault(g =>
                _guilds.TryGetValue(g, out var rec)
                && rec?.MiracleList != null
                && rec.MiracleList.Count > 0);

        if (string.IsNullOrWhiteSpace(church))
            return (null, null);

        if (!_guilds.TryGetValue(church, out var record) || record?.MiracleList == null)
            return (null, null);

        var sourceName = CleanChurchName(church);
        var allNames = record.MiracleList.Values
            .SelectMany(v => v ?? new List<string>())
            .Select(n => (n ?? string.Empty).Trim())
            .Where(n => n.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return (sourceName, allNames.Count == 0 ? null : allNames);
    }

    private MiracleListDraft? TryBuildChurchMiracleList()
    {
        if (_draft.Guilds == null || _draft.Guilds.Count == 0)
            return null;

        var church = _draft.Guilds
            .Select(g => g ?? string.Empty)
            .FirstOrDefault(g =>
                _guilds.TryGetValue(g, out var rec)
                && rec?.MiracleList != null
                && rec.MiracleList.Count > 0);

        if (string.IsNullOrWhiteSpace(church))
            return null;

        if (!_guilds.TryGetValue(church, out var record) || record?.MiracleList == null)
            return null;

        var sourceName = CleanChurchName(church);
        var name = $"Miracle List ({sourceName})";
        var list = new MiracleListDraft
        {
            Name = name,
            SourceName = sourceName,
            IsImported = true,
            IsSaved = true,
            IsScriptures = false
        };

        var allNames = record.MiracleList.Values
            .SelectMany(v => v ?? new List<string>())
            .Select(n => (n ?? string.Empty).Trim())
            .Where(n => n.Length > 0)
            .ToList();

        foreach (var entryName in allNames)
        {
            var match = _allMiracles.FirstOrDefault(m => string.Equals(m.name, entryName, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                list.Entries.Add(new MiracleListEntryDraft
                {
                    Name = match.name ?? string.Empty,
                    Power = match.power,
                    Alignment = match.alignment ?? string.Empty,
                    Sphere = match.sphere ?? string.Empty,
                    IsAdvanced = match.isAdvanced
                });
            }
            else
            {
                list.Entries.Add(new MiracleListEntryDraft
                {
                    Name = entryName,
                    Power = 0,
                    Alignment = string.Empty,
                    Sphere = string.Empty,
                    IsAdvanced = false
                });
            }
        }

        return list.Entries.Count == 0 ? null : list;
    }

    private IReadOnlyList<string> GetWizardColourSelections()
    {
        var selections = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_draft.SpecialisationSelections != null)
        {
            if (_draft.SpecialisationSelections.TryGetValue("Wizard Colour", out var wizardColour))
                AddNormalizedWizardSelections(selections, seen, wizardColour);

            if (_draft.SpecialisationSelections.TryGetValue("Witch Doctor Colour", out var witchDoctorColour))
                AddNormalizedWizardSelections(selections, seen, witchDoctorColour);

            if (_draft.SpecialisationSelections.TryGetValue("Vivomancer Colour", out var vivomancerColour))
                AddNormalizedWizardSelections(selections, seen, vivomancerColour);

            if (_draft.SpecialisationSelections.TryGetValue(SecondColourAbilityName, out var secondColourSelection))
            {
                AddSecondWizardColourSelections(selections, seen, secondColourSelection);
            }
            else if (_draft.SpecialisationSelections.TryGetValue(SecondColorAbilityName, out secondColourSelection))
            {
                AddSecondWizardColourSelections(selections, seen, secondColourSelection);
            }
        }

        if (selections.Count == 0 && _draft.Abilities != null)
        {
            foreach (var ability in _draft.Abilities)
            {
                if (ability == null || string.IsNullOrWhiteSpace(ability.Source))
                    continue;

                if (ability.Source.Contains("Specialisation:Wizard Colour", StringComparison.OrdinalIgnoreCase)
                    || ability.Source.Contains("Specialisation:Witch Doctor Colour", StringComparison.OrdinalIgnoreCase)
                    || ability.Source.Contains("Specialisation:Vivomancer Colour", StringComparison.OrdinalIgnoreCase))
                {
                    AddNormalizedWizardSelections(selections, seen, ability.Name);
                }
            }
        }

        if (selections.Count == 0)
            AddNormalizedWizardSelections(selections, seen, TryInferWizardColourFromSpellLists());

        if (selections.Count == 0)
            AddNormalizedWizardSelections(selections, seen, TryInferWizardColourFromClassName());

        foreach (var multiClassSelection in GetMultiClassWizardColourSelections())
            AddNormalizedWizardSelections(selections, seen, multiClassSelection);

        AddSecondWizardColourSelectionsFromAdvancementAbilities(selections, seen);

        if (IsSorcorialClass() && !selections.Any(s => s.Equals("Sorcorial", StringComparison.OrdinalIgnoreCase)))
            selections.Add("Sorcorial");

        return selections;
    }

    private IReadOnlyList<string> GetMultiClassWizardColourSelections()
    {
        var selections = new List<string>();
        if (_draft.MultiClassChoiceSelections == null || _draft.MultiClassChoiceSelections.Count == 0)
            return selections;

        foreach (var selection in _draft.MultiClassChoiceSelections)
        {
            if (!TryExtractChoiceSetRefFromStorageKey(selection.Key, out var choiceSetRef))
                continue;

            if (!choiceSetRef.Equals(WizardColourChoiceSetRef, StringComparison.OrdinalIgnoreCase)
                && !choiceSetRef.Equals(PowerMasterColourChoiceSetRef, StringComparison.OrdinalIgnoreCase)
                && !choiceSetRef.Equals(VivomancerColourChoiceSetRef, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = (selection.Value ?? string.Empty).Trim();
            if (value.Length > 0)
                selections.Add(value);
        }

        return selections;
    }

    private static bool TryExtractChoiceSetRefFromStorageKey(string? storageKey, out string choiceSetRef)
    {
        choiceSetRef = string.Empty;
        var key = (storageKey ?? string.Empty).Trim();
        if (key.Length == 0)
            return false;

        var separatorIndex = key.IndexOf("::", StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            choiceSetRef = key;
            return true;
        }

        var parsed = key.Substring(separatorIndex + 2).Trim();
        if (parsed.Length == 0)
            return false;

        choiceSetRef = parsed;
        return true;
    }

    private bool HasWizardTrackMultiClass()
    {
        if (_draft.MultiClassLevels == null || _draft.MultiClassLevels.Count == 0)
            return false;

        foreach (var multiClassLevel in _draft.MultiClassLevels)
        {
            if (multiClassLevel.Value <= 0)
                continue;

            var key = (multiClassLevel.Key ?? string.Empty).Trim();
            if (key.Length == 0)
                continue;

            if (TryResolveMultiClassDefinition(key, out var resolvedKey, out var definition))
            {
                var display = ResolveMultiClassDisplayName(definition, resolvedKey);
                if (IsWizardTrackClassName(display) || IsWizardTrackClassName(resolvedKey))
                    return true;
            }
            else if (IsWizardTrackClassName(key))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWizardTrackClassName(string? className)
    {
        var name = (className ?? string.Empty).Trim();
        if (name.Length == 0)
            return false;

        return name.Contains("Wizard", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Warlock", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Vivomancer", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Sorc", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddNormalizedWizardSelections(List<string> target, HashSet<string> seen, string? raw)
    {
        foreach (var selection in WizardSpellRules.ParseWizardSelections(raw))
        {
            if (seen.Add(selection))
                target.Add(selection);
        }
    }

    private void AddSecondWizardColourSelectionsFromAdvancementAbilities(List<string> selections, HashSet<string> seen)
    {
        if (!HasSecondColourAbility())
            return;

        foreach (var selectedAbility in _draft.AdvancementAbilities ?? Enumerable.Empty<string>())
        {
            var displayName = _abilityService.ResolveAbilityDisplayName(selectedAbility);
            string expectedName;
            if (AbilityNameMatches(displayName, SecondColourAbilityName))
            {
                expectedName = SecondColourAbilityName;
            }
            else if (AbilityNameMatches(displayName, SecondColorAbilityName))
            {
                expectedName = SecondColorAbilityName;
            }
            else
            {
                continue;
            }

            var encodedSelection = ExtractAbilityQualifier(displayName, expectedName);
            AddSecondWizardColourSelections(selections, seen, encodedSelection);
        }
    }

    private void AddSecondWizardColourSelections(List<string> selections, HashSet<string> seen, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return;

        var primaryColour = GetPrimaryWizardColour(selections);
        if (!primaryColour.HasValue)
            return;

        var ownedColours = selections
            .Select(ToMagicColourFromSelection)
            .Where(colour => colour.HasValue)
            .Select(colour => colour!.Value)
            .ToHashSet();
        ownedColours.UnionWith(GetRaceMagicColours());

        foreach (var parsedSelection in WizardSpellRules.ParseWizardSelections(raw))
        {
            if (!WizardSpellRules.TryParseMagicColour(parsedSelection, out var parsedColour))
                continue;

            if (ownedColours.Contains(parsedColour))
                continue;

            if (MagicColourOppositionRules.AreOpposites(primaryColour.Value, parsedColour))
                continue;

            if (ownedColours.Any(existing => MagicColourOppositionRules.AreOpposites(existing, parsedColour)))
                continue;

            var normalized = parsedColour.ToString();
            if (!seen.Add(normalized))
                continue;

            selections.Add(normalized);
            ownedColours.Add(parsedColour);
        }
    }

    private bool HasAdvancementAbilityByName(string expectedName)
    {
        if (string.IsNullOrWhiteSpace(expectedName))
            return false;

        return (_draft.AdvancementAbilities ?? Enumerable.Empty<string>())
            .Any(raw => AbilityNameMatches(_abilityService.ResolveAbilityDisplayName(raw), expectedName));
    }

    private bool HasSecondColourAbility()
        => HasAdvancementAbilityByName(SecondColourAbilityName)
           || HasAdvancementAbilityByName(SecondColorAbilityName);

    private static bool AbilityNameMatches(string? rawAbilityName, string expectedName)
    {
        var trimmed = (rawAbilityName ?? string.Empty).Trim();
        if (trimmed.Length == 0 || expectedName.Length == 0)
            return false;

        if (trimmed.Equals(expectedName, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!trimmed.StartsWith(expectedName, StringComparison.OrdinalIgnoreCase))
            return false;

        var suffix = trimmed.Substring(expectedName.Length);
        if (suffix.Length == 0)
            return true;

        var first = suffix[0];
        return first is ' ' or ':' or '-' or '(' or '[' or '/';
    }

    private static string ExtractAbilityQualifier(string? rawAbilityName, string expectedName)
    {
        var trimmed = (rawAbilityName ?? string.Empty).Trim();
        if (!trimmed.StartsWith(expectedName, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        var suffix = trimmed.Substring(expectedName.Length).Trim();
        if (suffix.Length == 0)
            return string.Empty;

        suffix = suffix.TrimStart(':', '-', '/').Trim();
        if (suffix.Length >= 2 && suffix[0] == '(' && suffix[^1] == ')')
            suffix = suffix[1..^1].Trim();
        else if (suffix.Length >= 2 && suffix[0] == '[' && suffix[^1] == ']')
            suffix = suffix[1..^1].Trim();

        return suffix;
    }

    private static MagicColours? ToMagicColourFromSelection(string? rawSelection)
        => WizardSpellRules.TryParseMagicColour(rawSelection, out var parsed) ? parsed : null;

    private HashSet<MagicColours> GetRaceMagicColours()
    {
        var raceColours = new HashSet<MagicColours>();

        if (_draft.SpecialisationSelections != null)
        {
            if (_draft.SpecialisationSelections.TryGetValue(FaerieColourSelectionKey, out var faerieSelection))
                AddRaceMagicColoursFromRaw(raceColours, faerieSelection);

            if (_draft.SpecialisationSelections.TryGetValue(ElfColourAbilitiesSelectionKey, out var elfSelection))
                AddRaceMagicColoursFromRaw(raceColours, elfSelection);
        }

        foreach (var overrideColour in _draft.ColourChoiceOverride ?? Enumerable.Empty<string>())
            AddRaceMagicColoursFromRaw(raceColours, overrideColour);

        AddRaceMagicColoursFromRaw(raceColours, _draft.RaceSubtypeValue);

        return raceColours;
    }

    private static void AddRaceMagicColoursFromRaw(HashSet<MagicColours> target, string? raw)
    {
        foreach (var token in WizardSpellRules.ParseWizardSelections(raw))
        {
            if (TryMapRaceTokenToMagicColour(token, out var mappedColour))
                target.Add(mappedColour);
        }
    }

    private static bool TryMapRaceTokenToMagicColour(string? token, out MagicColours colour)
    {
        if (WizardSpellRules.TryParseMagicColour(token, out colour))
            return true;

        var normalized = NormalizeClassKey(token);
        colour = normalized switch
        {
            "light" => MagicColours.White,
            "dark" => MagicColours.Black,
            "air" => MagicColours.Blue,
            "earth" => MagicColours.Brown,
            "fire" => MagicColours.Red,
            "water" => MagicColours.Green,
            _ => default
        };

        return normalized is "light" or "dark" or "air" or "earth" or "fire" or "water";
    }

    private HashSet<MagicColours> GetRaceRestrictedOppositeColours()
    {
        var raceColours = GetRaceMagicColours();
        var blocked = new HashSet<MagicColours>();
        foreach (var raceColour in raceColours)
        {
            if (MagicColourOppositionRules.TryGetOpposite(raceColour, out var opposite)
                && !raceColours.Contains(opposite))
            {
                blocked.Add(opposite);
            }
        }

        return blocked;
    }

    private string? TryInferWizardColourFromSpellLists()
    {
        var baseList = _draft.SpellLists?.FirstOrDefault(l => l.IsBaseList);
        var name = baseList?.Name ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            return null;

        foreach (var colour in Enum.GetValues<MagicColours>())
        {
            var token = colour.ToString();
            if (name.Contains(token, StringComparison.OrdinalIgnoreCase))
                return token;
        }

        if (name.Contains("Sorc", StringComparison.OrdinalIgnoreCase))
            return "Sorcorial";

        return null;
    }

    private string? TryInferWizardColourFromClassName()
    {
        var className = (_draft.Class ?? string.Empty).Trim();
        if (className.Length == 0)
            return null;

        foreach (var colour in Enum.GetValues<MagicColours>())
        {
            var token = colour.ToString();
            if (className.Contains(token, StringComparison.OrdinalIgnoreCase))
                return token;
        }

        if (className.Contains("Sorc", StringComparison.OrdinalIgnoreCase))
            return "Sorcorial";

        return null;
    }

    private MagicColours? GetPrimaryWizardColour(IReadOnlyList<string>? selections = null)
    {
        var picked = selections ?? GetWizardColourSelections();
        foreach (var selection in picked)
        {
            if (WizardSpellRules.TryParseMagicColour(selection, out var colour))
                return colour;
        }

        return null;
    }

    private bool IsSorcorialClass()
    {
        var className = (_draft.Class ?? string.Empty).Trim();
        if (className.Length == 0)
            return false;

        return className.Contains("Sorcerer", StringComparison.OrdinalIgnoreCase)
               || className.Contains("Sorceror", StringComparison.OrdinalIgnoreCase)
               || className.Contains("Sorcorial", StringComparison.OrdinalIgnoreCase)
               || className.Contains("Sorcery", StringComparison.OrdinalIgnoreCase);
    }

    private Func<SpellService.SpellRaw, bool>? BuildSpecialistSpellFilter()
    {
        var baseSelections = GetWizardColourSelections();
        var hasCompetence = HasAdvancementAbilityByName(CompetenceAbilityName);

        if (baseSelections.Count == 0 && !hasCompetence)
            return null;

        var blockedOppositeColours = GetBlockedSpecialistColours(baseSelections);

        return spell =>
        {
            if (spell == null)
                return false;

            if (baseSelections.Count > 0
                && WizardSpellRules.SpellMatchesAnyWizardSelection(spell.colour, baseSelections))
            {
                return false;
            }

            if (baseSelections.Count == 0 && hasCompetence)
            {
                var allowedByCompetence = GreyWizardColours.Any(colour =>
                    WizardSpellRules.SpellMatchesWizardSelection(spell.colour, colour.ToString()));
                if (!allowedByCompetence)
                    return false;
            }

            if (blockedOppositeColours.Count > 0
                && WizardSpellRules.TryExtractSingleMagicColour(spell.colour, out var spellColour)
                && blockedOppositeColours.Contains(spellColour))
            {
                return false;
            }

            return true;
        };
    }

    private HashSet<MagicColours> GetBlockedSpecialistColours(IReadOnlyList<string> selectedColours)
    {
        var blocked = GetRaceRestrictedOppositeColours();
        if (selectedColours == null || selectedColours.Count == 0)
            return blocked;

        var classRecord = ResolveClassRecord();
        if (IsVivomancerClass() || (!IsWizardTrackClass(classRecord) && !IsWarlockClass()))
            return blocked;

        var primary = GetPrimaryWizardColour(selectedColours);
        if (!primary.HasValue || primary.Value == MagicColours.Grey)
            return blocked;

        var owned = selectedColours
            .Select(ToMagicColourFromSelection)
            .Where(colour => colour.HasValue)
            .Select(colour => colour!.Value)
            .ToHashSet();
        owned.UnionWith(GetRaceMagicColours());

        foreach (var colour in owned)
        {
            if (MagicColourOppositionRules.TryGetOpposite(colour, out var opposite)
                && !owned.Contains(opposite))
            {
                blocked.Add(opposite);
            }
        }

        return blocked;
    }

    private void EnsureWizardSpellListImported()
    {
        if (!ShowSpellsTab)
            return;

        var selectedColours = GetWizardColourSelections();
        var listLabel = BuildBaseSpellListLabel(selectedColours);

        if (_draft.SpellLists == null)
            _draft.SpellLists = new List<SpellListDraft>();

        var baseList = FindBaseSpellList();
        if (baseList == null)
        {
            baseList = new SpellListDraft
            {
                Name = listLabel,
                IsBaseList = true,
                IsMinimized = true
            };
            _draft.SpellLists.Insert(0, baseList);
        }
        else
        {
            baseList.IsBaseList = true;
            baseList.Name = listLabel;

            var idx = _draft.SpellLists.IndexOf(baseList);
            if (idx > 0)
            {
                _draft.SpellLists.RemoveAt(idx);
                _draft.SpellLists.Insert(0, baseList);
            }
        }

        RebuildBaseSpellEntries(baseList, selectedColours);
    }

    private SpellListDraft? FindBaseSpellList()
    {
        if (_draft.SpellLists == null || _draft.SpellLists.Count == 0)
            return null;

        var baseList = _draft.SpellLists.FirstOrDefault(l => l.IsBaseList);
        if (baseList != null)
            return baseList;

        return _draft.SpellLists.FirstOrDefault(l =>
            !string.IsNullOrWhiteSpace(l?.Name)
            && l.Name.Contains("Spells", StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildBaseSpellListLabel(IReadOnlyList<string> selectedColours)
    {
        if (selectedColours == null || selectedColours.Count == 0)
            return "Base Spells";

        if (selectedColours.Count == 1)
            return $"{selectedColours[0]} Spells";

        return $"{string.Join(" / ", selectedColours)} Spells";
    }

    private void RebuildBaseSpellEntries(SpellListDraft target, IReadOnlyList<string> selectedColours)
    {
        if (target == null)
            return;

        target.Entries ??= new List<SpellListEntryDraft>();
        if (_allSpells.Count == 0)
            return;

        var includeWizardGreyBonus = ShouldIncludeNonGreyWizardGreyBonus(selectedColours);
        var rebuilt = WizardSpellRules.BuildBaseSpellEntries(_allSpells, selectedColours, includeWizardGreyBonus);
        if (rebuilt.Count == 0 && target.Entries.Count > 0)
            return;

        target.Entries = rebuilt;
    }

    private bool ShouldIncludeNonGreyWizardGreyBonus(IReadOnlyList<string> selectedColours)
    {
        if (selectedColours == null || selectedColours.Count == 0)
            return false;

        if (_draft.ColourChoiceOverride.Count > 0
            && !_draft.ColourChoiceOverride.Any(s =>
                WizardSpellRules.TryParseMagicColour(s, out var parsedOverride) && parsedOverride == MagicColours.Grey))
        {
            return false;
        }

        var classRecord = ResolveClassRecord();
        if (IsVivomancerClass() || IsWarlockClass() || !IsWizardTrackClass(classRecord))
            return false;

        if (selectedColours.Any(s => WizardSpellRules.TryParseMagicColour(s, out var parsed) && parsed == MagicColours.Grey))
            return false;

        var primaryColour = GetPrimaryWizardColour(selectedColours);
        return primaryColour.HasValue && primaryColour.Value != MagicColours.Grey;
    }

    internal static bool SpellMatchesWizardSelection(string? rawColour, string selection)
        => WizardSpellRules.SpellMatchesWizardSelection(rawColour, selection);

    private void OnSpellListChanged()
    {
        RefreshSpecialistSlots();
        PersistDraft();
    }

    private void RefreshSpecialistSlots()
    {
        var available = GetFreeSpecialistSlotsByColour();
        var totalAvailable = available.Values.Sum();
        var selected = GetSelectedSpecialistCountsByColour();
        var segments = new List<SpecialistSlotSegmentVm>();
        var legend = new List<SpecialistSlotLegendVm>();
        var selectedTotal = selected.Values.Sum();

        foreach (var colour in Enum.GetValues<MagicColours>())
        {
            var used = selected.TryGetValue(colour, out var count) ? count : 0;
            if (used <= 0)
                continue;

            var colourValue = GetMagicColourColor(colour);
            segments.Add(new SpecialistSlotSegmentVm(used, colourValue));
            legend.Add(new SpecialistSlotLegendVm(colour.ToString(), used, colourValue));
        }

        var totalUsed = segments.Sum(s => s.Weight);
        var unselected = Math.Max(0, totalAvailable - totalUsed);
        var unselectedColour = Color.FromArgb("#D1D5DB");
        if (unselected > 0)
            segments.Add(new SpecialistSlotSegmentVm(unselected, unselectedColour, isUnselected: true));
        if (totalAvailable > 0)
            legend.Add(new SpecialistSlotLegendVm("Unselected", unselected, unselectedColour, isUnselected: true));

        SpecialistSlotSegments = segments;
        SpecialistSlotLegendItems = legend;
        SpecialistSlotsUsed = selectedTotal;
        SpecialistSlotsTotal = totalAvailable;
        SpecialistSlotsSummaryColor = totalAvailable > 0 && selectedTotal > totalAvailable
            ? Color.FromArgb("#B91C1C")
            : Colors.Black;
        Raise(nameof(SpecialistSlotsSummary));
        Raise(nameof(ShowSpecialistSlotsBar));
        Raise(nameof(ShowSpecialistSlotsLegend));
        Raise(nameof(CanAddSpecialistList));
    }

    private Dictionary<MagicColours, int> GetSelectedSpecialistCountsByColour()
    {
        var counts = new Dictionary<MagicColours, int>();
        foreach (var list in _draft.SpellLists ?? new List<SpellListDraft>())
        {
            if (list.IsBaseList)
                continue;

            foreach (var entry in list.Entries ?? new List<SpellListEntryDraft>())
            {
                if (string.IsNullOrWhiteSpace(entry?.Name))
                    continue;

                var colourText = (entry.Colour ?? string.Empty).Trim();
                if (colourText.Length == 0)
                    continue;

                MagicColours colour;
                if (WizardSpellRules.TryExtractSingleMagicColour(colourText, out var extracted))
                {
                    colour = extracted;
                }
                else
                {
                    var normalized = WizardSpellRules.NormalizeWizardSelection(colourText);
                    if (!WizardSpellRules.TryParseMagicColour(normalized, out colour))
                        continue;
                }

                counts[colour] = counts.TryGetValue(colour, out var current) ? current + 1 : 1;
            }
        }

        return counts;
    }

    private static Color GetMagicColourColor(MagicColours colour)
        => colour switch
        {
            MagicColours.Red => Color.FromArgb("#EF4444"),
            MagicColours.Blue => Color.FromArgb("#3B82F6"),
            MagicColours.Green => Color.FromArgb("#10B981"),
            MagicColours.Brown => Color.FromArgb("#8B5E3C"),
            MagicColours.White => Color.FromArgb("#F3F4F6"),
            MagicColours.Black => Color.FromArgb("#111827"),
            MagicColours.Grey => Color.FromArgb("#9CA3AF"),
            MagicColours.Gold => Color.FromArgb("#D4AF37"),
            MagicColours.Bronze => Color.FromArgb("#CD7F32"),
            MagicColours.Silver => Color.FromArgb("#C0C0C0"),
            MagicColours.Ivory => Color.FromArgb("#F5F5DC"),
            MagicColours.Ebony => Color.FromArgb("#2F1B0C"),
            MagicColours.Jade => Color.FromArgb("#00A86B"),
            MagicColours.Onyx => Color.FromArgb("#353839"),
            _ => Color.FromArgb("#6B7280")
        };


    private static string ExtractImportedSourceName(string? name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return string.Empty;

        var start = trimmed.IndexOf('(');
        var end = trimmed.LastIndexOf(')');
        if (start >= 0 && end > start)
        {
            var inner = trimmed.Substring(start + 1, end - start - 1);
            return CleanChurchName(inner);
        }

        var cleaned = trimmed.Replace("Miracle List", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        return CleanChurchName(cleaned);
    }

    private static string CleanChurchName(string? name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        const string prefix = "Church of ";
        if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return trimmed.Substring(prefix.Length).Trim();
        return trimmed;
    }

    private void AddMiracleList()
    {
        var baseList = GetBaseMiracleList();
        if (baseList == null)
        {
            var draft = new MiracleListDraft
            {
                Name = "Base List",
                IsScriptures = false
            };
            _draft.MiracleLists.Add(draft);
            var vm = new MiracleListVm(
                draft,
                _allMiracles,
                _domainService,
                OnMiracleListValidationChanged,
                () => _draft.Alignment,
                () => _draft.Points,
                OnMiracleListExpandRequested);
            HookMiracleList(vm);
            MiracleLists.Add(vm);
            SetActiveMiracleList(vm);
            RaiseMiracleListStateChanged();
            return;
        }

        if (!baseList.IsSaved || GetScripturesMiracleList() != null)
            return;

        var scripturesDraft = new MiracleListDraft
        {
            Name = "Scriptures of Faith",
            IsScriptures = true
        };
        _draft.MiracleLists.Add(scripturesDraft);
        var scripturesVm = new MiracleListVm(
            scripturesDraft,
            _allMiracles,
            _domainService,
            OnMiracleListValidationChanged,
            () => _draft.Alignment,
            () => _draft.Points,
            OnMiracleListExpandRequested);
        HookMiracleList(scripturesVm);
        MiracleLists.Add(scripturesVm);
        SetActiveMiracleList(scripturesVm);
        RaiseMiracleListStateChanged();
    }

    private void RemoveMiracleList(MiracleListVm? list)
    {
        if (list == null) return;
        UnhookMiracleList(list);
        MiracleLists.Remove(list);
        _draft.MiracleLists.Remove(list.Draft);

        if (!list.IsScriptures)
        {
            var scriptures = GetScripturesMiracleList();
            if (scriptures != null)
            {
                UnhookMiracleList(scriptures);
                MiracleLists.Remove(scriptures);
                _draft.MiracleLists.Remove(scriptures.Draft);
            }
        }

        NormalizeMiracleListExpansion();
        RaiseMiracleListStateChanged();
    }

    private const string BaseEvocationListName = "base list";
    private const string PostEighthEvocationListName = "post 8th";

    private void LoadEvocationListsFromDraft()
    {
        EvocationLists.Clear();
        _draft.EvocationLists ??= new List<EvocationListDraft>();

        var baseDraft = ResolveBaseEvocationDraft(_draft.EvocationLists);
        var postEighthDraft = ResolvePostEighthEvocationDraft(_draft.EvocationLists, baseDraft);

        baseDraft.IsPost8th = false;
        postEighthDraft.IsPost8th = true;
        baseDraft.Name = BaseEvocationListName;
        postEighthDraft.Name = PostEighthEvocationListName;
        baseDraft.Entries ??= new List<EvocationListEntryDraft>();
        postEighthDraft.Entries ??= new List<EvocationListEntryDraft>();

        _draft.EvocationLists = new List<EvocationListDraft> { baseDraft, postEighthDraft };

        EvocationLists.Add(new EvocationListVm(baseDraft, _allEvocations, _domainService));
        EvocationLists.Add(new EvocationListVm(postEighthDraft, _allEvocations, _domainService));
    }

    private static EvocationListDraft ResolveBaseEvocationDraft(IReadOnlyList<EvocationListDraft> drafts)
    {
        var baseDraft = drafts.FirstOrDefault(d => !d.IsPost8th);
        if (baseDraft != null)
            return baseDraft;

        if (drafts.Count > 0)
            return drafts[0];

        return new EvocationListDraft
        {
            Name = BaseEvocationListName,
            IsPost8th = false,
            IsMinimized = false
        };
    }

    private static EvocationListDraft ResolvePostEighthEvocationDraft(
        IReadOnlyList<EvocationListDraft> drafts,
        EvocationListDraft baseDraft)
    {
        var postDraft = drafts.FirstOrDefault(d => d.IsPost8th && !ReferenceEquals(d, baseDraft));
        if (postDraft != null)
            return postDraft;

        var byName = drafts.FirstOrDefault(d =>
            !ReferenceEquals(d, baseDraft)
            && string.Equals(d.Name?.Trim(), PostEighthEvocationListName, StringComparison.OrdinalIgnoreCase));
        if (byName != null)
            return byName;

        var next = drafts.FirstOrDefault(d => !ReferenceEquals(d, baseDraft));
        if (next != null)
            return next;

        return new EvocationListDraft
        {
            Name = PostEighthEvocationListName,
            IsPost8th = true,
            IsMinimized = true
        };
    }

    private void OnMiracleListValidationChanged()
    {
        Raise(nameof(CanSave));
        (SaveCommand as Command)?.ChangeCanExecute();
        RaiseMiracleListStateChanged();
        PersistDraft();
    }

    private void OnEvilStairwayValidationChanged()
    {
        Raise(nameof(CanSave));
        (SaveCommand as Command)?.ChangeCanExecute();
        PersistDraft();
    }

    private void HookMiracleList(MiracleListVm vm)
    {
        vm.PropertyChanged += OnMiracleListPropertyChanged;
    }

    private void UnhookMiracleList(MiracleListVm vm)
    {
        vm.PropertyChanged -= OnMiracleListPropertyChanged;
    }

    private void OnMiracleListPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MiracleListVm.IsSaved))
            RaiseMiracleListStateChanged();
    }

    private void RaiseMiracleListStateChanged()
    {
        Raise(nameof(CanAddMiracleList));
        Raise(nameof(AddMiracleListLabel));
    }

    private void NormalizeMiracleListExpansion()
    {
        var expanded = MiracleLists.Where(m => m.IsExpanded).ToList();
        if (expanded.Count <= 1)
            return;

        var keep = expanded.First();
        foreach (var list in MiracleLists)
            list.IsMinimized = !ReferenceEquals(list, keep);
    }

    private void SetActiveMiracleList(MiracleListVm list)
    {
        foreach (var vm in MiracleLists)
            vm.IsMinimized = !ReferenceEquals(vm, list);
    }

    private void OnMiracleListExpandRequested(MiracleListVm list)
    {
        if (list.IsMinimized)
        {
            foreach (var vm in MiracleLists)
                vm.IsMinimized = !ReferenceEquals(vm, list);
            return;
        }

        list.IsMinimized = true;
    }

    private void SaveDraft()
    {
        if (!CanSave)
            return;

        _draftStore.Save();
    }

    private async Task ExportSpellsToExcelAsync()
    {
        var path = BuildSpellsExcel();
        await _exportService.ShareFileAsync($"{_draft.Name}'s spells", path);
    }

    private async Task CopySpellsToClipboardAsync()
    {
        var text = BuildSpellsText();
        await _exportService.CopyTextAsync(text);
    }

    private async Task SaveSpellsToTextAsync()
    {
        var path = await BuildSpellsTextFileAsync();
        await _exportService.OpenFileAsync(path);
    }

    private async Task ExportMiraclesToExcelAsync()
    {
        var path = BuildMiraclesExcel();
        await _exportService.ShareFileAsync($"{_draft.Name}'s miracles", path);
    }

    private async Task CopyMiraclesToClipboardAsync()
    {
        var text = BuildMiraclesText();
        await _exportService.CopyTextAsync(text);
    }

    private async Task SaveMiraclesToTextAsync()
    {
        var path = await BuildMiraclesTextFileAsync();
        await _exportService.OpenFileAsync(path);
    }

    private string BuildSpellsExcel()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Spells");

        sheet.Cell(1, 1).Value = "List";
        sheet.Cell(1, 2).Value = "Name";
        sheet.Cell(1, 3).Value = "Level";
        sheet.Cell(1, 4).Value = "Colour";
        sheet.Cell(1, 5).Value = "Advanced";

        var row = 2;
        foreach (var (listName, entry) in EnumerateSpellEntries())
        {
            sheet.Cell(row, 1).Value = listName;
            sheet.Cell(row, 2).Value = entry.Name;
            sheet.Cell(row, 3).Value = entry.Level;
            sheet.Cell(row, 4).Value = entry.Colour;
            sheet.Cell(row, 5).Value = entry.IsAdvanced ? "Yes" : "No";
            row++;
        }

        sheet.Columns().AdjustToContents();

        var fileName = $"Spells_{SanitizeFileName(_draft.Name)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
        var path = _fileService.CombineCachePath(fileName);
        workbook.SaveAs(path);
        return path;
    }

    private string BuildMiraclesExcel()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Miracles");

        sheet.Cell(1, 1).Value = "List";
        sheet.Cell(1, 2).Value = "Name";
        sheet.Cell(1, 3).Value = "Power";
        sheet.Cell(1, 4).Value = "Alignment";
        sheet.Cell(1, 5).Value = "Sphere";
        sheet.Cell(1, 6).Value = "Advanced";

        var row = 2;
        foreach (var (listName, entry) in EnumerateMiracleEntries())
        {
            sheet.Cell(row, 1).Value = listName;
            sheet.Cell(row, 2).Value = entry.Name;
            sheet.Cell(row, 3).Value = entry.Power;
            sheet.Cell(row, 4).Value = entry.Alignment;
            sheet.Cell(row, 5).Value = entry.Sphere;
            sheet.Cell(row, 6).Value = entry.IsAdvanced ? "Yes" : "No";
            row++;
        }

        sheet.Columns().AdjustToContents();

        var fileName = $"Miracles_{SanitizeFileName(_draft.Name)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
        var path = _fileService.CombineCachePath(fileName);
        workbook.SaveAs(path);
        return path;
    }

    private string BuildSpellsText()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{_draft.Name} - Spells");

        var baseList = _draft.SpellLists?.FirstOrDefault(l => l.IsBaseList);
        if (baseList != null)
            AppendSpellListText(sb, string.IsNullOrWhiteSpace(baseList.Name) ? "Base List" : baseList.Name, baseList);

        var specialistList = _draft.SpellLists?.FirstOrDefault(l => !l.IsBaseList);
        if (specialistList != null)
            AppendSpellListText(sb, "Specialists", specialistList);

        return sb.ToString().TrimEnd();
    }

    private string BuildMiraclesText()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{_draft.Name} - Miracles");

        var baseList = _draft.MiracleLists?.FirstOrDefault(l => !l.IsScriptures);
        if (baseList != null && (ShowPriestMiracleLists || baseList.Entries.Any(e => !string.IsNullOrWhiteSpace(e.Name))))
            AppendMiracleListText(sb, FormatMiracleListHeader(baseList), baseList);

        var scripturesList = _draft.MiracleLists?.FirstOrDefault(l => l.IsScriptures);
        if (scripturesList != null && (ShowPriestMiracleLists || scripturesList.Entries.Any(e => !string.IsNullOrWhiteSpace(e.Name))))
            AppendMiracleListText(sb, "Scriptures of Faith", scripturesList);

        var evilStairway = _draft.EvilStairwayList;
        if (evilStairway != null && evilStairway.Entries.Any(e => !string.IsNullOrWhiteSpace(e.Name)))
            AppendMiracleListText(sb, "Evil Stairway", evilStairway);

        return sb.ToString().TrimEnd();
    }

    private async Task<string> BuildSpellsTextFileAsync()
    {
        var content = BuildSpellsText();
        var fileName = $"Spells_{SanitizeFileName(_draft.Name)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt";
        var path = _fileService.CombineCachePath(fileName);
        await _fileService.WriteTextAsync(path, content);
        return path;
    }

    private async Task<string> BuildMiraclesTextFileAsync()
    {
        var content = BuildMiraclesText();
        var fileName = $"Miracles_{SanitizeFileName(_draft.Name)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt";
        var path = _fileService.CombineCachePath(fileName);
        await _fileService.WriteTextAsync(path, content);
        return path;
    }

    private IEnumerable<(string ListName, SpellListEntryDraft Entry)> EnumerateSpellEntries()
    {
        if (_draft.SpellLists == null)
            yield break;

        foreach (var list in _draft.SpellLists)
        {
            var listName = list.IsBaseList ? (string.IsNullOrWhiteSpace(list.Name) ? "Base List" : list.Name) : "Specialists";
            foreach (var entry in list.Entries ?? new List<SpellListEntryDraft>())
            {
                if (string.IsNullOrWhiteSpace(entry.Name))
                    continue;
                yield return (listName, entry);
            }
        }
    }

    private IEnumerable<(string ListName, MiracleListEntryDraft Entry)> EnumerateMiracleEntries()
    {
        if (_draft.MiracleLists != null)
        {
            foreach (var list in _draft.MiracleLists)
            {
                var listName = list.IsScriptures ? "Scriptures of Faith" : FormatMiracleListHeader(list);
                foreach (var entry in list.Entries ?? new List<MiracleListEntryDraft>())
                {
                    if (string.IsNullOrWhiteSpace(entry.Name))
                        continue;
                    yield return (listName, entry);
                }
            }
        }

        if (_draft.EvilStairwayList != null)
        {
            var listName = string.IsNullOrWhiteSpace(_draft.EvilStairwayList.Name)
                ? "Evil Stairway"
                : _draft.EvilStairwayList.Name;
            foreach (var entry in _draft.EvilStairwayList.Entries ?? new List<MiracleListEntryDraft>())
            {
                if (string.IsNullOrWhiteSpace(entry.Name))
                    continue;
                yield return (listName, entry);
            }
        }
    }

    private static string FormatMiracleListHeader(MiracleListDraft list)
    {
        var source = list.SourceName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(source))
            return "Base List";

        return $"Base List - {source}";
    }

    private static void AppendSpellListText(StringBuilder sb, string header, SpellListDraft list)
    {
        sb.AppendLine();
        sb.AppendLine(header);
        foreach (var entry in list.Entries ?? new List<SpellListEntryDraft>())
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
                continue;
            var adv = entry.IsAdvanced ? "Advanced" : "Standard";
            var colour = string.IsNullOrWhiteSpace(entry.Colour) ? string.Empty : $" [{entry.Colour}]";
            sb.AppendLine($"- {entry.Name} (Lvl {entry.Level}){colour} [{adv}]");
        }
    }

    private static void AppendMiracleListText(StringBuilder sb, string header, MiracleListDraft list)
    {
        sb.AppendLine();
        sb.AppendLine(header);
        foreach (var entry in list.Entries ?? new List<MiracleListEntryDraft>())
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
                continue;
            var adv = entry.IsAdvanced ? "Advanced" : "Standard";
            sb.AppendLine($"- {entry.Name} ({entry.Power}) [{entry.Alignment}] [{entry.Sphere}] [{adv}]");
        }
    }

    private static string SanitizeFileName(string? name)
    {
        var safe = string.IsNullOrWhiteSpace(name) ? "Character" : name.Trim();
        foreach (var ch in Path.GetInvalidFileNameChars())
            safe = safe.Replace(ch, '_');
        return safe;
    }
}
