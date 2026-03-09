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
using labyItems.Models.Characters;
using labyItems.Models.Enums;
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
    private readonly CharacterDraft _draft;
    private IReadOnlyList<MiracleService.MiracRaw> _allMiracles = Array.Empty<MiracleService.MiracRaw>();
    private IReadOnlyList<SpellService.SpellRaw> _allSpells = Array.Empty<SpellService.SpellRaw>();
    private IReadOnlyList<DruidEvocationService.EvocRaw> _allEvocations = Array.Empty<DruidEvocationService.EvocRaw>();
    private Dictionary<string, GuildRecord> _guilds = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, ServiceCharacterClassRecord> _classes = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, ManuAbilityOption> _abilityOptions = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, ManuAbilityOption> _abilityOptionsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, EvolutionService.AbilityResult> _abilityDetailsByKey = new(StringComparer.OrdinalIgnoreCase);

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
        ICharacterDraftStore? draftStore = null,
        ICharacterAdvancementDomainService? domainService = null,
        IAdvancementTabVisibilityService? tabVisibilityService = null,
        IAdvancementValidationService? validationService = null,
        IExportService? exportService = null,
        IFileService? fileService = null)
    {
        _draftStore = draftStore ?? new CharacterDraftStore(draft);
        _draft = _draftStore.Draft;
        _domainService = domainService ?? new CharacterAdvancementDomainService();
        _tabVisibilityService = tabVisibilityService ?? new AdvancementTabVisibilityService();
        _validationService = validationService ?? new AdvancementValidationService();
        _fileService = fileService ?? new MauiFileService();
        _exportService = exportService ?? new ExportService(
            new MauiClipboardService(),
            new MauiLauncherService(),
            new MauiShareService());

        Items.CollectionChanged += (_, __) => SyncItemsToDraft();
        Abilities.CollectionChanged += (_, __) =>
        {
            SyncAbilitiesToDraft();
            UpdateAbilityPoints();
        };

        if (!_draft.HasSetCurrentVitae)
        {
            _draft.CurrentVitae = 100;
            _draft.HasSetCurrentVitae = true;
            Raise(nameof(CurrentVitae));
        }

        AddAbilityCommand = new Command(AddAbility);
        RemoveAbilityCommand = new Command<AbilityEntryVm>(RemoveAbility);

        AddItemCommand = new Command(AddItem);
        RemoveItemCommand = new Command<ItemLineVm>(RemoveItem);

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
            foreach (var list in MiracleLists)
                list.RefreshExternalLimits();
        }
    }

    public int CurrentVitae
    {
        get => _draft.CurrentVitae;
        set
        {
            if (_draft.CurrentVitae == value) return;
            _draft.CurrentVitae = value;
            _draft.HasSetCurrentVitae = true;
            Raise();
        }
    }

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
    public ICommand AddAbilityCommand { get; }
    public ICommand RemoveAbilityCommand { get; }

    public int AbilityPointsSpent => Abilities.Sum(a => a.Cost);
    public string AbilityPointsSummary => $"Spent: {AbilityPointsSpent} / {Points} Pts";

    public ObservableCollection<ItemLineVm> Items { get; } = new();
    public ICommand AddItemCommand { get; }
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

    public async Task InitializeAsync()
    {
        _guilds = await GuildsService.GetAllAsync() ?? new Dictionary<string, GuildRecord>(StringComparer.OrdinalIgnoreCase);
        try
        {
            _classes = await ClassService.GetAllAsync() ?? new Dictionary<string, ServiceCharacterClassRecord>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            _classes = new Dictionary<string, ServiceCharacterClassRecord>(StringComparer.OrdinalIgnoreCase);
        }
        try
        {
            _allMiracles = await MiracleService.GetAllAsync();
        }
        catch
        {
            _allMiracles = Array.Empty<MiracleService.MiracRaw>();
        }
        await EnsureSpellCatalogueLoadedAsync(forceReload: true);
        await EnsureEvocationCatalogueLoadedAsync();
        try
        {
            var allAbilities = await ManuAbilityService.GetAllAsync();
            _abilityOptionsByName = BuildAbilityOptionsByName(allAbilities);
            AbilityOptions = BuildAbilityOptionsWithLabels(_abilityOptionsByName.Values);
        }
        catch
        {
            AbilityOptions = new Dictionary<string, ManuAbilityOption>(StringComparer.OrdinalIgnoreCase);
            _abilityOptionsByName = new Dictionary<string, ManuAbilityOption>(StringComparer.OrdinalIgnoreCase);
        }

        LoadAbilitiesFromDraft();
        LoadItemsFromDraft();
        UpdateTabVisibility();
        if (ShowSpellsTab)
            EnsureWizardSpellListImported();
        LoadSpellListsFromDraft();
        await EnsureWizardBaseListHasEntriesAsync();
        LoadMiracleListsFromDraft();
        await LoadEvilStairwayAsync();
        LoadEvocationListsFromDraft();

        Raise(nameof(CanSave));
        (SaveCommand as Command)?.ChangeCanExecute();
        UpdateAbilityPoints();
    }

    private async Task<bool> EnsureSpellCatalogueLoadedAsync(bool forceReload = false)
    {
        if (!forceReload && _allSpells.Count > 0)
            return true;

        try
        {
            _allSpells = await SpellService.GetAllAsync();
        }
        catch
        {
            _allSpells = Array.Empty<SpellService.SpellRaw>();
        }

        System.Diagnostics.Debug.WriteLine($"[ADVANCE][SPELLS] Loaded spell catalogue count: {_allSpells.Count}");
        return _allSpells.Count > 0;
    }

    private async Task EnsureEvocationCatalogueLoadedAsync()
    {
        _allEvocations = await EvocationCatalogService.GetAllAsync();
        System.Diagnostics.Debug.WriteLine($"[ADVANCE][EVOCS] Loaded evocation catalogue count: {_allEvocations.Count}");
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
            return result;

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
            var hasWizardToken = className.Contains("Wizard", StringComparison.OrdinalIgnoreCase);
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
        ShowSpellsTab = state.ShowSpellsTab;
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
        Abilities.Clear();
        foreach (var ability in _draft.AdvancementAbilities ?? new List<string>())
        {
            var line = BuildAbilityEntry(ability);
            Abilities.Add(line);
        }
        UpdateAbilityPoints();
    }

    private void AddAbility()
    {
        if (SelectedAbilityOption == null)
            return;

        var name = SelectedAbilityOption.Value.Name ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            return;

        var line = new AbilityEntryVm(name, SelectedAbilityOption.Value.Cost, SyncAbilitiesToDraft);
        Abilities.Add(line);
        SelectedAbilityOption = null;
        SyncAbilitiesToDraft();
    }

    private void RemoveAbility(AbilityEntryVm? ability)
    {
        if (ability == null) return;
        Abilities.Remove(ability);
        SyncAbilitiesToDraft();
    }

    private void SyncAbilitiesToDraft()
    {
        _draft.AdvancementAbilities = Abilities
            .Select(a => (a.Name ?? string.Empty).Trim())
            .Where(t => t.Length > 0)
            .ToList();
    }

    private AbilityEntryVm BuildAbilityEntry(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (_abilityOptionsByName.TryGetValue(trimmed, out var option))
            return new AbilityEntryVm(trimmed, option.Cost, SyncAbilitiesToDraft);

        return new AbilityEntryVm(trimmed, 0, SyncAbilitiesToDraft);
    }

    private void UpdateAbilityPoints()
    {
        var running = 0;
        foreach (var entry in Abilities)
        {
            running += entry.Cost;
            entry.SetRunningTotal(running);
        }
        Raise(nameof(AbilityPointsSpent));
        Raise(nameof(AbilityPointsSummary));
    }

    private static string BuildAbilityOptionLabel(ManuAbilityOption option)
    {
        var name = option.Name ?? string.Empty;
        return $"{name} ({option.Cost})";
    }

    private static Dictionary<string, ManuAbilityOption> BuildAbilityOptionsByName(IEnumerable<ManuAbilityService.ManuAbilityEntry> entries)
    {
        return entries
            .GroupBy(a => a.name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key))
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var entry = g.First();
                    return new ManuAbilityOption(
                        entry.name ?? string.Empty,
                        entry.cost,
                        entry.table,
                        entry.availability ?? string.Empty,
                        entry.description ?? string.Empty);
                },
                StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, ManuAbilityOption> BuildAbilityOptionsWithLabels(IEnumerable<ManuAbilityOption> entries)
    {
        return entries
            .Where(e => !string.IsNullOrWhiteSpace(e.Name))
            .ToDictionary(
                e => BuildAbilityOptionLabel(e),
                e => e,
                StringComparer.OrdinalIgnoreCase);
    }

    public async Task<Dictionary<string, ManuAbilityOption>> SearchAbilityOptionsAsync(string query)
    {
        var results = await ManuAbilityService.SearchAsync(query ?? string.Empty);
        var byName = BuildAbilityOptionsByName(results);

        foreach (var kvp in byName)
            _abilityOptionsByName[kvp.Key] = kvp.Value;

        return BuildAbilityOptionsWithLabels(byName.Values);
    }

    public async Task<EvolutionService.AbilityResult?> FindAbilityByNameAsync(string? abilityName)
    {
        var key = AbilityDetailsLookupService.NormalizeKey(abilityName);
        if (key.Length == 0)
            return null;

        if (_abilityDetailsByKey.TryGetValue(key, out var cached))
            return cached;

        try
        {
            var ability = await AbilityDetailsLookupService.FindByIndexAsync(abilityName);
            if (ability != null)
                _abilityDetailsByKey[key] = ability;
            return ability;
        }
        catch
        {
            return null;
        }
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

    private void LoadItemsFromDraft()
    {
        Items.Clear();
        foreach (var item in _draft.AdvancementItems ?? new List<string>())
        {
            var line = new ItemLineVm(item);
            line.PropertyChanged += OnItemChanged;
            Items.Add(line);
        }

        if (Items.Count == 0)
            AddItem();
    }

    private void AddItem()
    {
        var line = new ItemLineVm(string.Empty);
        line.PropertyChanged += OnItemChanged;
        Items.Add(line);
        SyncItemsToDraft();
    }

    private void RemoveItem(ItemLineVm? item)
    {
        if (item == null) return;
        item.PropertyChanged -= OnItemChanged;
        Items.Remove(item);
        SyncItemsToDraft();
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
        => SyncItemsToDraft();

    private void SyncItemsToDraft()
    {
        _draft.AdvancementItems = Items
            .Select(i => (i.Text ?? string.Empty).Trim())
            .Where(t => t.Length > 0)
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

            if (_draft.SpecialisationSelections.TryGetValue("Vivomancer Colour", out var vivomancerColour))
                AddNormalizedWizardSelections(selections, seen, vivomancerColour);
        }

        if (selections.Count == 0 && _draft.Abilities != null)
        {
            foreach (var ability in _draft.Abilities)
            {
                if (ability == null || string.IsNullOrWhiteSpace(ability.Source))
                    continue;

                if (ability.Source.Contains("Specialisation:Wizard Colour", StringComparison.OrdinalIgnoreCase)
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

        if (selections.Count == 0 && IsSorcorialClass())
            selections.Add("Sorcorial");

        return selections;
    }

    private static void AddNormalizedWizardSelections(List<string> target, HashSet<string> seen, string? raw)
    {
        foreach (var selection in WizardSpellRules.ParseWizardSelections(raw))
        {
            if (seen.Add(selection))
                target.Add(selection);
        }
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
               || className.Contains("Sorcorial", StringComparison.OrdinalIgnoreCase)
               || className.Contains("Sorcery", StringComparison.OrdinalIgnoreCase);
    }

    private Func<SpellService.SpellRaw, bool>? BuildSpecialistSpellFilter()
    {
        var baseSelections = GetWizardColourSelections();
        if (baseSelections.Count == 0)
            return null;

        return spell => !WizardSpellRules.SpellMatchesAnyWizardSelection(spell?.colour, baseSelections);
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

                if (!WizardSpellRules.TryParseMagicColour(colourText, out var colour))
                    continue;

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

public sealed class AbilityEntryVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private readonly Action _onChanged;
    private string _name;
    private int _cost;
    private int _runningTotal;

    public string Name
    {
        get => _name;
        set
        {
            var next = value ?? string.Empty;
            if (_name == next) return;
            _name = next;
            Raise();
            Raise(nameof(NameWithCost));
            _onChanged();
        }
    }

    public int Cost
    {
        get => _cost;
        private set
        {
            if (_cost == value) return;
            _cost = value;
            Raise();
            Raise(nameof(NameWithCost));
            _onChanged();
        }
    }

    public int RunningTotal
    {
        get => _runningTotal;
        private set
        {
            if (_runningTotal == value) return;
            _runningTotal = value;
            Raise();
            Raise(nameof(RunningTotalText));
        }
    }

    public string NameWithCost => $"{Name} ({Cost})";
    public string RunningTotalText => $"Total: {RunningTotal}";

    public AbilityEntryVm(string name, int cost, Action onChanged)
    {
        _name = name ?? string.Empty;
        _cost = cost;
        _onChanged = onChanged;
    }

    public void SetRunningTotal(int total)
    {
        RunningTotal = total;
    }
}

public sealed class ItemLineVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private string _text;
    public string Text
    {
        get => _text;
        set
        {
            if (_text == value) return;
            _text = value ?? string.Empty;
            Raise();
        }
    }

    public ItemLineVm(string text)
    {
        _text = text ?? string.Empty;
    }
}

public sealed class SpellListVm : INotifyPropertyChanged
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

    private readonly IReadOnlyList<SpellService.SpellRaw> _allSpells;
    private readonly Func<SpellService.SpellRaw, bool>? _spellFilter;
    private readonly Action? _onListChanged;

    public SpellListDraft Draft { get; }

    public string Name
    {
        get => Draft.Name;
        set
        {
            if (Draft.Name == value) return;
            Draft.Name = value ?? string.Empty;
            Raise();
            Raise(nameof(HeaderTitle));
        }
    }

    public bool IsBaseList => Draft.IsBaseList;

    public bool IsSpecialistList => !IsBaseList;

    public bool IsReadOnly => IsBaseList;

    public bool CanEdit => !IsReadOnly;

    public bool ShowNameEditor => false;

    public bool ShowHeaderLabel => true;

    public string HeaderTitle => IsBaseList
        ? (string.IsNullOrWhiteSpace(Name) ? "Spell List" : Name)
        : "Specialists";

    public bool CanRemoveList => false;

    public bool IsMinimized
    {
        get => Draft.IsMinimized;
        set
        {
            if (Draft.IsMinimized == value) return;
            Draft.IsMinimized = value;
            Raise();
            Raise(nameof(IsExpanded));
            Raise(nameof(CanAddSelected));
        }
    }

    public bool IsExpanded => !IsMinimized;

    public ObservableCollection<SpellEntryVm> Entries { get; } = new();

    private SpellOption? _selectedSpellOption;
    public SpellOption? SelectedSpellOption
    {
        get => _selectedSpellOption;
        set
        {
            if (!Set(ref _selectedSpellOption, value)) return;
            Raise(nameof(CanAddSelected));
        }
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set => Set(ref _searchText, value ?? string.Empty);
    }

    public ObservableCollection<string> ColourFilterOptions { get; } = new();
    public ObservableCollection<string> SelectedColourFilters { get; } = new();
    public ObservableCollection<string> TierFilterOptions { get; } = new() { "Advanced", "Standard" };
    public ObservableCollection<string> SelectedTierFilters { get; } = new();

    private Dictionary<string, SpellOption> _filteredOptions = new();
    public Dictionary<string, SpellOption> FilteredOptions
    {
        get => _filteredOptions;
        private set => Set(ref _filteredOptions, value);
    }

    public ICommand AddSelectedCommand { get; }
    public ICommand RemoveEntryCommand { get; }
    public ICommand ToggleExpandedCommand { get; }

    public bool CanAddSelected =>
        CanEdit
        && IsExpanded
        && !string.IsNullOrWhiteSpace(SelectedSpellOption?.Name);

    private readonly Func<int> _getCasterLevel;

    public SpellListVm(
        SpellListDraft draft,
        IReadOnlyList<SpellService.SpellRaw> allSpells,
        Func<int> getCasterLevel,
        Action? onListChanged = null,
        Func<SpellService.SpellRaw, bool>? spellFilter = null)
    {
        Draft = draft;
        _allSpells = allSpells ?? Array.Empty<SpellService.SpellRaw>();
        _getCasterLevel = getCasterLevel ?? (() => 0);
        _onListChanged = onListChanged;
        _spellFilter = spellFilter;

        if (!IsBaseList)
            Draft.Name = "Specialists";

        AddSelectedCommand = new Command(AddSelectedSpell);
        RemoveEntryCommand = new Command<SpellEntryVm>(RemoveEntry);
        ToggleExpandedCommand = new Command(() => IsMinimized = !IsMinimized);

        SelectedColourFilters.CollectionChanged += (_, __) => UpdateFilteredOptions();
        SelectedTierFilters.CollectionChanged += (_, __) => UpdateFilteredOptions();

        LoadColourFilterOptions();
        LoadEntriesFromDraft();
        UpdateFilteredOptions();
    }


    private void LoadEntriesFromDraft()
    {
        Entries.Clear();
        var entries = Draft.Entries ?? new List<SpellListEntryDraft>();
        if (IsReadOnly)
            entries = entries
                .OrderBy(e => e.Level)
                .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

        foreach (var entry in entries)
        {
            var vm = new SpellEntryVm(entry, OnEntryChanged, _getCasterLevel);
            if (!IsReadOnly && !string.IsNullOrWhiteSpace(entry.Name))
                vm.SelectedSpell = new SpellOption(entry.Name, entry.Level, entry.Colour ?? string.Empty, entry.IsAdvanced);
            Entries.Add(vm);
        }
        ReindexEntries();
    }

    private void AddSelectedSpell()
    {
        if (!CanAddSelected || SelectedSpellOption == null)
            return;

        var draft = new SpellListEntryDraft();
        Draft.Entries.Add(draft);
        var vm = new SpellEntryVm(draft, OnEntryChanged, _getCasterLevel);
        vm.SelectedSpell = SelectedSpellOption.Value;
        Entries.Add(vm);
        ReindexEntries();
        SearchPickerStateHelper.ClearForNextSearch<SpellOption>(
            setSelection: v => SelectedSpellOption = v,
            setSearchText: text => SearchText = text);
    }

    private void RemoveEntry(SpellEntryVm? entry)
    {
        if (IsReadOnly)
            return;

        if (entry == null) return;
        Entries.Remove(entry);
        Draft.Entries.Remove(entry.Draft);
        ReindexEntries();
        _onListChanged?.Invoke();
    }

    private void OnEntryChanged()
    {
        UpdateFilteredOptions();
        _onListChanged?.Invoke();
    }

    private void ReindexEntries()
    {
        for (var i = 0; i < Entries.Count; i++)
            Entries[i].SetRowIndex(i);
    }

    private void LoadColourFilterOptions()
    {
        ColourFilterOptions.Clear();
        foreach (var colour in Enum.GetValues<MagicColours>())
            ColourFilterOptions.Add(colour.ToString());

        if (!ColourFilterOptions.Any(c => c.Equals("Sorcorial", StringComparison.OrdinalIgnoreCase)))
            ColourFilterOptions.Add("Sorcorial");
    }

    private void UpdateFilteredOptions()
    {
        var dict = new Dictionary<string, SpellOption>(StringComparer.OrdinalIgnoreCase);
        var selectedColours = SelectedColourFilters != null
            ? new HashSet<string>(SelectedColourFilters, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var selectedTiers = SelectedTierFilters != null
            ? new HashSet<string>(SelectedTierFilters, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var filterByColour = selectedColours.Count > 0;
        var filterAdvanced = selectedTiers.Contains("Advanced");
        var filterStandard = selectedTiers.Contains("Standard");
        var filterByTier = selectedTiers.Count == 1;

        foreach (var spell in _allSpells)
        {
            if (string.IsNullOrWhiteSpace(spell?.name))
                continue;
            if (_spellFilter != null && !_spellFilter(spell))
                continue;

            if (filterByColour)
            {
                var matchesColour = selectedColours.Any(c => AdvanceCharacterVm.SpellMatchesWizardSelection(spell.colour, c));
                if (!matchesColour)
                    continue;
            }

            if (filterByTier)
            {
                var isAdvanced = spell.isAdvanced ?? false;
                if (filterAdvanced && !isAdvanced)
                    continue;
                if (filterStandard && isAdvanced)
                    continue;
            }

            var option = new SpellOption(spell.name, spell.level, spell.colour ?? string.Empty, spell.isAdvanced ?? false);
            var label = $"{spell.name} (Lvl {spell.level})";
            if (!dict.ContainsKey(label))
                dict[label] = option;
        }

        FilteredOptions = dict;
    }
}

public sealed class SpellEntryVm : INotifyPropertyChanged
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

    private readonly Action _onChanged;

    public SpellListEntryDraft Draft { get; }

    private SpellOption? _selectedSpell;
    public SpellOption? SelectedSpell
    {
        get => _selectedSpell;
        set
        {
            if (!Set(ref _selectedSpell, value)) return;

            if (value == null)
            {
                Draft.Name = string.Empty;
                Draft.Level = 0;
                Draft.Colour = string.Empty;
                Draft.IsAdvanced = false;
            }
            else if (value is SpellOption option)
            {
                Draft.Name = option.Name;
                Draft.Level = option.Level;
                Draft.Colour = option.Colour;
                Draft.IsAdvanced = option.IsAdvanced;
            }

            RefreshLearningWarning();
            Raise(nameof(DisplayText));
            Raise(nameof(NameText));
            Raise(nameof(LevelText));
            Raise(nameof(HasSpell));
            _onChanged();
        }
    }

    public string DisplayText
        => string.IsNullOrWhiteSpace(Draft.Name) ? string.Empty : $"{Draft.Name} (Lvl {Draft.Level})";
    public string NameText => Draft.Name ?? string.Empty;
    public string LevelText => string.IsNullOrWhiteSpace(Draft.Name) ? string.Empty : Draft.Level.ToString();

    private int _rowIndex;
    public Color RowBackgroundColor => (_rowIndex % 2) == 0 ? Colors.White : Color.FromArgb("#FAF8F3");

    public bool HasSpell => !string.IsNullOrWhiteSpace(Draft.Name);

    private void RefreshLearningWarning()
    {
        if (_selectedSpell == null)
        {
            ShowLearningWarning = false;
            LearningWarningText = string.Empty;
            return;
        }

        var casterLevel = Math.Max(0, _getCasterLevel());
        var spellLevel = Math.Max(0, _selectedSpell.Value.Level);

        if (spellLevel <= casterLevel)
        {
            // Still does damage per your table, but your UX ask is
            // specifically: highlight if power higher than caster level.
            ShowLearningWarning = false;
            LearningWarningText = string.Empty;
            return;
        }

        var dmg = GetSpellLearningDamage(casterLevel, spellLevel);
        ShowLearningWarning = true;
        LearningWarningText = $"Learning this will deal {dmg} damage to you.";
    }

    private static int GetSpellLearningDamage(int casterLevel, int spellLevel)
    {
        var delta = spellLevel - casterLevel;

        return delta switch
        {
            <= -2 => 2,
            -1 => 8,
            0 => 18,
            1 => 32,
            2 => 50,
            3 => 72,
            4 => 98,
            >= 5 => 128
        };
    }

    private readonly Func<int> _getCasterLevel;

    private bool _showLearningWarning;
    public bool ShowLearningWarning { get => _showLearningWarning; private set => Set(ref _showLearningWarning, value); }

    private string _learningWarningText = string.Empty;
    public string LearningWarningText { get => _learningWarningText; private set => Set(ref _learningWarningText, value); }

    public SpellEntryVm(SpellListEntryDraft draft, Action onChanged, Func<int> getCasterLevel)
    {
        Draft = draft;
        _onChanged = onChanged;
        _getCasterLevel = getCasterLevel ?? (() => 0);
        RefreshLearningWarning();
    }

    public void SetRowIndex(int rowIndex)
    {
        if (_rowIndex == rowIndex)
            return;

        _rowIndex = rowIndex;
        Raise(nameof(RowBackgroundColor));
    }
}

public readonly record struct SpellOption(string Name, int Level, string Colour, bool IsAdvanced);

public sealed class SpecialistSlotSegmentVm
{
    public int Weight { get; }
    public Color Colour { get; }
    public bool IsUnselected { get; }

    public SpecialistSlotSegmentVm(int weight, Color colour, bool isUnselected = false)
    {
        Weight = weight;
        Colour = colour;
        IsUnselected = isUnselected;
    }
}

public sealed class SpecialistSlotLegendVm
{
    public string Label { get; }
    public int Amount { get; }
    public Color Colour { get; }
    public bool IsUnselected { get; }
    public string DisplayText => $"{Label} {Amount}";

    public SpecialistSlotLegendVm(string label, int amount, Color colour, bool isUnselected = false)
    {
        Label = label;
        Amount = amount;
        Colour = colour;
        IsUnselected = isUnselected;
    }
}

public sealed class EvocationListVm : INotifyPropertyChanged
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

    private const int MaxTotalPower = 60;
    private const int MaxAdvancedPower = 20;

    private readonly IReadOnlyList<DruidEvocationService.EvocRaw> _allEvocations;
    private readonly Dictionary<string, DruidEvocationService.EvocRaw> _evocationLookup;
    private readonly ICharacterAdvancementDomainService _domainService;

    public EvocationListDraft Draft { get; }

    public string Name => Draft.Name;
    public string HeaderTitle =>
        string.IsNullOrWhiteSpace(Draft.Name)
            ? (Draft.IsPost8th ? "post 8th" : "base list")
            : Draft.Name;
    public bool IsPost8th => Draft.IsPost8th;

    public bool IsMinimized
    {
        get => Draft.IsMinimized;
        set
        {
            if (Draft.IsMinimized == value) return;
            Draft.IsMinimized = value;
            Raise();
            Raise(nameof(IsExpanded));
            Raise(nameof(CanAddSelected));
        }
    }

    public bool IsExpanded => !IsMinimized;

    public ObservableCollection<EvocationEntryVm> Entries { get; } = new();

    private EvocationOption? _selectedEvocationOption;
    public EvocationOption? SelectedEvocationOption
    {
        get => _selectedEvocationOption;
        set
        {
            if (!Set(ref _selectedEvocationOption, value)) return;
            Raise(nameof(CanAddSelected));
        }
    }

    public bool CanAddSelected => IsExpanded && SelectedEvocationOption != null;

    public ObservableCollection<string> FieldFilterOptions { get; } = new();
    public ObservableCollection<string> SelectedFieldFilters { get; } = new();
    public ObservableCollection<string> TierFilterOptions { get; } = new() { "Advanced", "Standard" };
    public ObservableCollection<string> SelectedTierFilters { get; } = new();
    public ObservableCollection<EvocationFieldSegmentVm> FieldBreakdownSegments { get; } = new();
    public ObservableCollection<EvocationFieldLegendVm> FieldLegendItems { get; } = new();
    public bool HasFieldBreakdown => FieldBreakdownSegments.Count > 0;

    private bool _showFieldLegend;
    public bool ShowFieldLegend
    {
        get => _showFieldLegend;
        set
        {
            if (!Set(ref _showFieldLegend, value))
                return;
            Raise(nameof(FieldLegendChevronText));
        }
    }

    public string FieldLegendChevronText => ShowFieldLegend ? "▴" : "▾";

    private Dictionary<string, EvocationOption> _filteredOptions = new();
    public Dictionary<string, EvocationOption> FilteredOptions
    {
        get => _filteredOptions;
        private set => Set(ref _filteredOptions, value);
    }

    private int _totalPower;
    public int TotalPower
    {
        get => _totalPower;
        private set => Set(ref _totalPower, value);
    }

    private int _advancedPower;
    public int AdvancedPower
    {
        get => _advancedPower;
        private set => Set(ref _advancedPower, value);
    }

    private string _validationMessage = string.Empty;
    public string ValidationMessage
    {
        get => _validationMessage;
        private set
        {
            if (Set(ref _validationMessage, value))
                Raise(nameof(HasValidationError));
        }
    }

    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationMessage);

    public ICommand AddSelectedCommand { get; }
    public ICommand RemoveEntryCommand { get; }
    public ICommand ToggleExpandedCommand { get; }
    public ICommand ToggleFieldLegendCommand { get; }

    public EvocationListVm(
        EvocationListDraft draft,
        IReadOnlyList<DruidEvocationService.EvocRaw> allEvocations,
        ICharacterAdvancementDomainService domainService)
    {
        Draft = draft;
        _allEvocations = allEvocations ?? Array.Empty<DruidEvocationService.EvocRaw>();
        _domainService = domainService;
        _evocationLookup = _allEvocations
            .Where(e => !string.IsNullOrWhiteSpace(e?.name))
            .GroupBy(e => e.name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        AddSelectedCommand = new Command(AddSelectedEvocation);
        RemoveEntryCommand = new Command<EvocationEntryVm>(RemoveEntry);
        ToggleExpandedCommand = new Command(() => IsMinimized = !IsMinimized);
        ToggleFieldLegendCommand = new Command(() => ShowFieldLegend = !ShowFieldLegend);

        SelectedFieldFilters.CollectionChanged += (_, __) => UpdateFilteredOptions();
        SelectedTierFilters.CollectionChanged += (_, __) => UpdateFilteredOptions();
        FieldBreakdownSegments.CollectionChanged += (_, __) => Raise(nameof(HasFieldBreakdown));

        LoadFieldOptions();
        LoadEntriesFromDraft();
        UpdateFilteredOptions();
        UpdateStats();
    }

    private void LoadFieldOptions()
    {
        FieldFilterOptions.Clear();
        foreach (var field in EvocationFieldCatalog.Definitions)
            FieldFilterOptions.Add(field.DisplayName);
    }

    private void LoadEntriesFromDraft()
    {
        Entries.Clear();
        foreach (var entry in Draft.Entries ?? new List<EvocationListEntryDraft>())
        {
            var vm = new EvocationEntryVm(entry, OnEntryChanged);
            if (!string.IsNullOrWhiteSpace(entry.Name))
            {
                if (_evocationLookup.TryGetValue(entry.Name, out var match))
                {
                    vm.SelectedEvocation = new EvocationOption(match.name, match.power, match.isAdvanced, match.fields);
                }
                else
                {
                    vm.SelectedEvocation = new EvocationOption(entry.Name, entry.Power, entry.IsAdvanced, Array.Empty<string>());
                }
            }
            Entries.Add(vm);
        }
        ReindexEntries();
    }

    private void AddSelectedEvocation()
    {
        if (SelectedEvocationOption == null)
            return;

        var draft = new EvocationListEntryDraft();
        Draft.Entries.Add(draft);
        var vm = new EvocationEntryVm(draft, OnEntryChanged);
        vm.SelectedEvocation = SelectedEvocationOption.Value;
        Entries.Add(vm);
        ReindexEntries();
        SearchPickerStateHelper.ClearForNextSearch<EvocationOption>(
            setSelection: v => SelectedEvocationOption = v,
            setSearchText: _ => { });
        UpdateStats();
    }

    private void RemoveEntry(EvocationEntryVm? entry)
    {
        if (entry == null) return;
        Entries.Remove(entry);
        Draft.Entries.Remove(entry.Draft);
        ReindexEntries();
        UpdateStats();
    }

    private void OnEntryChanged()
    {
        UpdateFilteredOptions();
        UpdateStats();
    }

    private void UpdateFilteredOptions()
    {
        var dict = new Dictionary<string, EvocationOption>(StringComparer.OrdinalIgnoreCase);
        var selectedFields = SelectedFieldFilters != null
            ? new HashSet<string>(SelectedFieldFilters, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var selectedTiers = SelectedTierFilters != null
            ? new HashSet<string>(SelectedTierFilters, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var filterByFields = selectedFields.Count > 0;
        var filterAdvanced = selectedTiers.Contains("Advanced");
        var filterStandard = selectedTiers.Contains("Standard");
        var filterByTier = selectedTiers.Count == 1;

        foreach (var ev in _allEvocations)
        {
            if (string.IsNullOrWhiteSpace(ev?.name))
                continue;

            if (filterByFields)
            {
                if (!EvocationFieldCatalog.MatchesAnySelectedField(ev.fields, selectedFields))
                    continue;
            }

            if (filterByTier)
            {
                if (filterAdvanced && !ev.isAdvanced)
                    continue;
                if (filterStandard && ev.isAdvanced)
                    continue;
            }

            var option = new EvocationOption(ev.name, ev.power, ev.isAdvanced, ev.fields ?? new List<string>());
            var label = $"{ev.name} ({ev.power})";
            if (!dict.ContainsKey(label))
                dict[label] = option;
        }

        FilteredOptions = dict;
    }

    private void ReindexEntries()
    {
        for (var i = 0; i < Entries.Count; i++)
            Entries[i].SetRowIndex(i);
    }

    private void UpdateStats()
    {
        var totals = _domainService.ComputeEvocationPointTotals(Draft.Entries);
        var total = totals.Total;
        var advanced = totals.Advanced;

        TotalPower = total;
        AdvancedPower = advanced;

        var message = string.Empty;
        if (total > MaxTotalPower)
        {
            message = $"Total evocation power exceeds {MaxTotalPower} EP.";
        }
        else if (advanced > MaxAdvancedPower)
        {
            message = $"Advanced evocations exceed {MaxAdvancedPower} EP.";
        }
        else
        {
            var advancedEntries = Draft.Entries
                .Where(e => e.IsAdvanced && !string.IsNullOrWhiteSpace(e.Name))
                .ToList();

            if (advancedEntries.Count > 1)
            {
                var allShareField = _domainService.AdvancedEvocationsShareAField(
                    advancedEntries,
                    name =>
                    {
                        if (!_evocationLookup.TryGetValue(name, out var ev))
                            return null;
                        return EvocationFieldCatalog.GetComparableFieldKeys(ev.fields);
                    });

                if (!allShareField)
                    message = "Advanced evocations must all come from the same field.";
            }
        }

        RebuildFieldBreakdown();
        ValidationMessage = message;
    }

    private void RebuildFieldBreakdown()
    {
        var totals = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in Draft.Entries ?? new List<EvocationListEntryDraft>())
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
                continue;

            var power = Math.Max(0, entry.Power);
            if (power <= 0)
                continue;

            if (!_evocationLookup.TryGetValue(entry.Name, out var evocation))
                continue;

            var fields = EvocationFieldCatalog.ResolveFields(evocation.fields);
            if (fields.Count == 0)
                continue;

            var splitPower = power / (double)fields.Count;
            foreach (var field in fields)
                totals[field.Key] = totals.GetValueOrDefault(field.Key, 0d) + splitPower;
        }

        FieldBreakdownSegments.Clear();
        FieldLegendItems.Clear();

        if (totals.Count == 0)
        {
            ShowFieldLegend = false;
            return;
        }

        var ordered = EvocationFieldCatalog.Definitions
            .Where(def => totals.TryGetValue(def.Key, out var value) && value > 0d)
            .ToList();

        var totalWeight = ordered.Sum(def => totals[def.Key]);
        foreach (var field in ordered)
        {
            var value = totals[field.Key];
            var ratio = totalWeight > 0d ? (value / totalWeight) * 100d : 0d;

            FieldBreakdownSegments.Add(new EvocationFieldSegmentVm(Math.Max(0.01, value), field.Colour));
            FieldLegendItems.Add(new EvocationFieldLegendVm(
                field.DisplayName,
                field.Colour,
                $"{FormatFieldPower(value)} EP ({ratio:0.#}%)"));
        }
    }

    private static string FormatFieldPower(double value)
    {
        var rounded = Math.Round(value, 1);
        if (Math.Abs(rounded - Math.Round(rounded)) < 0.01)
            return ((int)Math.Round(rounded)).ToString();
        return rounded.ToString("0.0");
    }
}

public sealed class EvocationEntryVm : INotifyPropertyChanged
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

    private readonly Action _onChanged;

    public EvocationListEntryDraft Draft { get; }

    public string DisplayText
        => string.IsNullOrWhiteSpace(Draft.Name) ? string.Empty : $"{Draft.Name} ({Draft.Power} EP)";
    public string NameText => Draft.Name ?? string.Empty;
    public string PowerText => string.IsNullOrWhiteSpace(Draft.Name) ? string.Empty : $"{Draft.Power} EP";

    public bool HasEvocation => !string.IsNullOrWhiteSpace(Draft.Name);

    private int _rowIndex;
    public Color RowBackgroundColor => (_rowIndex % 2) == 0 ? Colors.White : Color.FromArgb("#FAF8F3");

    private EvocationOption? _selectedEvocation;
    public EvocationOption? SelectedEvocation
    {
        get => _selectedEvocation;
        set
        {
            if (!Set(ref _selectedEvocation, value)) return;

            if (value == null)
            {
                Draft.Name = string.Empty;
                Draft.Power = 0;
                Draft.IsAdvanced = false;
            }
            else if (value is EvocationOption option)
            {
                Draft.Name = option.Name;
                Draft.Power = option.Power;
                Draft.IsAdvanced = option.IsAdvanced;
            }

            Raise(nameof(DisplayText));
            Raise(nameof(NameText));
            Raise(nameof(PowerText));
            Raise(nameof(HasEvocation));
            _onChanged();
        }
    }

    public EvocationEntryVm(EvocationListEntryDraft draft, Action onChanged)
    {
        Draft = draft;
        _onChanged = onChanged;
    }

    public void SetRowIndex(int rowIndex)
    {
        if (_rowIndex == rowIndex)
            return;

        _rowIndex = rowIndex;
        Raise(nameof(RowBackgroundColor));
    }
}

public sealed class EvocationFieldSegmentVm
{
    public double Weight { get; }
    public Color Colour { get; }

    public EvocationFieldSegmentVm(double weight, Color colour)
    {
        Weight = weight;
        Colour = colour;
    }
}

public sealed class EvocationFieldLegendVm
{
    public string FieldName { get; }
    public Color Colour { get; }
    public string SummaryText { get; }

    public EvocationFieldLegendVm(string fieldName, Color colour, string summaryText)
    {
        FieldName = fieldName;
        Colour = colour;
        SummaryText = summaryText;
    }
}

internal sealed class EvocationFieldDefinition
{
    public EvocationFields Field { get; }
    public string DisplayName { get; }
    public string Key { get; }
    public Color Colour { get; }

    public EvocationFieldDefinition(EvocationFields field, string displayName, string key, Color colour)
    {
        Field = field;
        DisplayName = displayName;
        Key = key;
        Colour = colour;
    }
}

internal static class EvocationFieldCatalog
{
    private const string AllToken = "all";

    public static IReadOnlyList<EvocationFieldDefinition> Definitions { get; } = new List<EvocationFieldDefinition>
    {
        BuildDefinition(EvocationFields.Spring, "Spring", "#22C55E"),
        BuildDefinition(EvocationFields.Summer, "Summer", "#F59E0B"),
        BuildDefinition(EvocationFields.Autumn, "Autumn", "#B45309"),
        BuildDefinition(EvocationFields.Winter, "Winter", "#60A5FA"),
        BuildDefinition(EvocationFields.HornedMan, "Horned Man", "#7C3AED"),
        BuildDefinition(EvocationFields.MotherNature, "Mother Nature", "#16A34A"),
        BuildDefinition(EvocationFields.ShadowOfTheDawn, "Shadow of the Dawn", "#4B5563"),
        BuildDefinition(EvocationFields.KeeperOfWinds, "Keeper of the Winds", "#0EA5E9"),
        BuildDefinition(EvocationFields.FatherOfTheWorld, "Father of the World", "#92400E"),
        BuildDefinition(EvocationFields.Deserts, "Deserts", "#D97706"),
        BuildDefinition(EvocationFields.Skies, "Skies", "#38BDF8"),
        BuildDefinition(EvocationFields.Mountains, "Mountains", "#6B7280"),
        BuildDefinition(EvocationFields.Rivers, "Rivers", "#0F766E"),
        BuildDefinition(EvocationFields.Forests, "Forests", "#166534"),
        BuildDefinition(EvocationFields.Cities, "Cities", "#334155")
    };

    private static readonly Dictionary<string, EvocationFieldDefinition> DefinitionLookup = BuildLookup();

    private static readonly Dictionary<string, string> Synonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["forest"] = NormalizeToken(EvocationFields.Forests.ToString()),
        ["river"] = NormalizeToken(EvocationFields.Rivers.ToString()),
        ["mountain"] = NormalizeToken(EvocationFields.Mountains.ToString()),
        ["desert"] = NormalizeToken(EvocationFields.Deserts.ToString()),
        ["city"] = NormalizeToken(EvocationFields.Cities.ToString()),
        ["sky"] = NormalizeToken(EvocationFields.Skies.ToString()),
        ["keeperofthewind"] = NormalizeToken(EvocationFields.KeeperOfWinds.ToString())
    };

    public static bool MatchesAnySelectedField(IEnumerable<string>? rawFields, HashSet<string> selectedDisplayNames)
    {
        if (selectedDisplayNames.Count == 0)
            return true;

        var selectedKeys = new HashSet<string>(
            selectedDisplayNames.Select(NormalizeToken),
            StringComparer.OrdinalIgnoreCase);

        var keys = ExtractComparableKeys(rawFields, includeAllAsEveryField: true);
        return keys.Overlaps(selectedKeys);
    }

    public static HashSet<string> GetComparableFieldKeys(IEnumerable<string>? rawFields)
        => ExtractComparableKeys(rawFields, includeAllAsEveryField: true);

    public static IReadOnlyList<EvocationFieldDefinition> ResolveFields(IEnumerable<string>? rawFields)
    {
        var keys = ExtractComparableKeys(rawFields, includeAllAsEveryField: true);
        if (keys.Count == 0)
            return Array.Empty<EvocationFieldDefinition>();

        return Definitions
            .Where(def => keys.Contains(def.Key))
            .ToList();
    }

    private static HashSet<string> ExtractComparableKeys(IEnumerable<string>? rawFields, bool includeAllAsEveryField)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasAll = false;

        foreach (var rawField in rawFields ?? Array.Empty<string>())
        {
            var normalized = NormalizeToken(rawField);
            if (normalized.Length == 0)
                continue;

            if (string.Equals(normalized, AllToken, StringComparison.OrdinalIgnoreCase))
            {
                hasAll = true;
                continue;
            }

            if (!DefinitionLookup.TryGetValue(normalized, out var definition)
                && Synonyms.TryGetValue(normalized, out var canonicalKey))
            {
                DefinitionLookup.TryGetValue(canonicalKey, out definition);
            }

            if (definition != null)
                keys.Add(definition.Key);
        }

        if (hasAll && includeAllAsEveryField)
        {
            foreach (var definition in Definitions)
                keys.Add(definition.Key);
        }

        return keys;
    }

    private static EvocationFieldDefinition BuildDefinition(EvocationFields field, string displayName, string colourHex)
    {
        var key = NormalizeToken(field.ToString());
        return new EvocationFieldDefinition(field, displayName, key, Color.FromArgb(colourHex));
    }

    private static Dictionary<string, EvocationFieldDefinition> BuildLookup()
    {
        var lookup = new Dictionary<string, EvocationFieldDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in Definitions)
        {
            lookup[definition.Key] = definition;
            lookup[NormalizeToken(definition.DisplayName)] = definition;
        }

        return lookup;
    }

    private static string NormalizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var chars = value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray();

        return new string(chars);
    }
}

public readonly record struct EvocationOption(string Name, int Power, bool IsAdvanced, IReadOnlyList<string> Fields)
{
    public bool Equals(EvocationOption other)
        => string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase)
           && Power == other.Power
           && IsAdvanced == other.IsAdvanced;

    public override int GetHashCode()
        => HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(Name ?? string.Empty), Power, IsAdvanced);
}

public readonly record struct ManuAbilityOption(string Name, int Cost, int Table, string Availability, string Description)
{
    public bool Equals(ManuAbilityOption other)
        => string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase)
           && Cost == other.Cost
           && Table == other.Table;

    public override int GetHashCode()
        => HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(Name ?? string.Empty), Cost, Table);
}

public sealed class MiracleListVm : INotifyPropertyChanged
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

    private readonly IReadOnlyList<MiracleService.MiracRaw> _allMiracles;
    private readonly Action _onValidationChanged;
    private readonly Dictionary<string, string> _sphereLookup;
    private readonly ICharacterAdvancementDomainService _domainService;
    private readonly Func<Alignment?> _getAlignment;
    private readonly Func<int> _getPoints;
    private readonly Action<MiracleListVm>? _onExpandRequested;

    private const int MaxListPower = 60;
    private const int MaxAdvancedPower = 10;

    public MiracleListDraft Draft { get; }

    public string Name
    {
        get => Draft.Name;
        set
        {
            if (Draft.Name == value) return;
            Draft.Name = value ?? string.Empty;
            Raise();
        }
    }

    public bool IsScriptures => Draft.IsScriptures;

    public string SourceName => Draft.SourceName ?? string.Empty;

    public string HeaderTitle
    {
        get
        {
            if (IsScriptures)
                return "Scriptures of Faith";

            return string.IsNullOrWhiteSpace(SourceName)
                ? "Base List"
                : $"Base List - {SourceName}";
        }
    }

    public bool IsSaved
    {
        get => Draft.IsImported || Draft.IsSaved;
        set
        {
            if (Draft.IsImported) return;
            if (Draft.IsSaved == value) return;
            Draft.IsSaved = value;
            Raise();
            Raise(nameof(IsReadOnly));
            Raise(nameof(CanEdit));
            Raise(nameof(CanRemoveList));
            Raise(nameof(CanAddSelected));
            Raise(nameof(CanSaveList));
            Raise(nameof(StateLabel));
            Raise(nameof(CanReopenScriptures));
        }
    }

    public bool IsMinimized
    {
        get => Draft.IsMinimized;
        set
        {
            if (Draft.IsMinimized == value) return;
            Draft.IsMinimized = value;
            Raise();
            Raise(nameof(IsExpanded));
            Raise(nameof(CanAddSelected));
        }
    }

    public bool IsExpanded => !IsMinimized;

    public bool IsReadOnly => Draft.IsImported || Draft.IsSaved;
    public bool CanEdit => !IsReadOnly;
    public bool CanRemoveList => CanEdit && !IsScriptures;
    public bool CanReopenScriptures => IsScriptures && IsSaved && !Draft.IsImported;

    public ObservableCollection<MiracleEntryVm> Entries { get; } = new();

    private MiracleOption? _selectedMiracleOption;
    public MiracleOption? SelectedMiracleOption
    {
        get => _selectedMiracleOption;
        set
        {
            if (!Set(ref _selectedMiracleOption, value)) return;
            Raise(nameof(CanAddSelected));
        }
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set => Set(ref _searchText, value ?? string.Empty);
    }

    public ObservableCollection<string> SphereFilterOptions { get; } = new();
    public ObservableCollection<string> SelectedSphereFilters { get; } = new();
    public ObservableCollection<string> AdvancedFilterOptions { get; } = new() { "Advanced", "Handbook" };
    public ObservableCollection<string> SelectedAdvancedFilters { get; } = new();
    public ObservableCollection<string> NeutralAlignmentOptions { get; } = new() { "Good", "Evil" };

    private Dictionary<string, MiracleOption> _filteredOptions = new();
    public Dictionary<string, MiracleOption> FilteredOptions
    {
        get => _filteredOptions;
        private set => Set(ref _filteredOptions, value);
    }

    public ICommand AddSelectedCommand { get; }
    public ICommand RemoveEntryCommand { get; }
    public ICommand SaveListCommand { get; }
    public ICommand ToggleExpandedCommand { get; }
    public ICommand ReopenScripturesCommand { get; }

    private int _goodPoints;
    public int GoodPoints { get => _goodPoints; private set => Set(ref _goodPoints, value); }

    private int _neutralPoints;
    public int NeutralPoints { get => _neutralPoints; private set => Set(ref _neutralPoints, value); }

    private int _evilPoints;
    public int EvilPoints { get => _evilPoints; private set => Set(ref _evilPoints, value); }

    private int _totalPoints;
    public int TotalPoints { get => _totalPoints; private set => Set(ref _totalPoints, value); }

    private int _advancedPoints;
    public int AdvancedPoints { get => _advancedPoints; private set => Set(ref _advancedPoints, value); }

    public int UnselectedPoints => Math.Max(0, MaxListPower - TotalPoints);

    public GridLength GoodWidth => new GridLength(GoodPoints, GridUnitType.Star);
    public GridLength NeutralWidth => new GridLength(NeutralPoints, GridUnitType.Star);
    public GridLength EvilWidth => new GridLength(EvilPoints, GridUnitType.Star);
    public GridLength UnselectedWidth => new GridLength(UnselectedPoints, GridUnitType.Star);

    private int _scripturesAllowed;
    public int ScripturesAllowed
    {
        get => _scripturesAllowed;
        private set
        {
            if (!Set(ref _scripturesAllowed, value)) return;
            RaiseScripturesProgressProperties();
            UpdateScripturesVisuals();
            Raise(nameof(CanAddSelected));
        }
    }

    public int ScripturesUsed => Entries.Count(e => !string.IsNullOrWhiteSpace(e.Draft.Name));

    public int ScripturesRemaining => Math.Max(0, ScripturesAllowed - ScripturesUsed);

    public string ScripturesProgressText => $"Scriptures remaining: {ScripturesRemaining}/{ScripturesAllowed}";

    public string ScripturesUnusedText => $"Unused scriptures: {ScripturesRemaining}/{ScripturesAllowed}";

    public double ScripturesUsedRatio
        => ScripturesAllowed <= 0 ? 0 : Math.Clamp((double)ScripturesUsed / ScripturesAllowed, 0d, 1d);

    public double ScripturesRemainingRatio
        => ScripturesAllowed <= 0 ? 1 : Math.Clamp((double)ScripturesRemaining / ScripturesAllowed, 0d, 1d);

    public bool HasUsedScriptures => ScripturesUsed > 0;

    public GridLength ScripturesUsedWidth
    {
        get
        {
            if (ScripturesAllowed <= 0)
                return new GridLength(0, GridUnitType.Star);

            var used = Math.Clamp(ScripturesUsed, 0, ScripturesAllowed);
            return new GridLength(used, GridUnitType.Star);
        }
    }

    public GridLength ScripturesUnusedWidth
    {
        get
        {
            if (ScripturesAllowed <= 0)
                return new GridLength(1, GridUnitType.Star);

            var used = Math.Clamp(ScripturesUsed, 0, ScripturesAllowed);
            var remaining = Math.Max(0, ScripturesAllowed - used);
            return new GridLength(remaining, GridUnitType.Star);
        }
    }

    private Color _scripturesFillColor = Colors.White;
    public Color ScripturesFillColor
    {
        get => _scripturesFillColor;
        private set => Set(ref _scripturesFillColor, value);
    }

    private Color _scripturesFillStrokeColor = Color.FromArgb("#374151");
    public Color ScripturesFillStrokeColor
    {
        get => _scripturesFillStrokeColor;
        private set => Set(ref _scripturesFillStrokeColor, value);
    }

    public string StateLabel => IsSaved ? "Saved" : "Editing";

    public bool CanAddSelected =>
        CanEdit
        && IsExpanded
        && !string.IsNullOrWhiteSpace(SelectedMiracleOption?.Name)
        && (!IsScriptures || ScripturesRemaining > 0);

    public bool CanSaveList => CanEdit && !HasValidationError && Entries.Any(e => !string.IsNullOrWhiteSpace(e.Draft.Name));

    public string EffectiveAlignment
    {
        get
        {
            if (GoodPoints > 10) return "Good";
            if (EvilPoints > 10) return "Evil";
            if (ShowNeutralAlignmentChoice && !string.IsNullOrWhiteSpace(Draft.NeutralAlignmentChoice))
                return Draft.NeutralAlignmentChoice!;
            return "Neutral";
        }
    }

    public string NeutralAlignmentChoice
    {
        get => Draft.NeutralAlignmentChoice ?? string.Empty;
        set
        {
            if (Draft.NeutralAlignmentChoice == value) return;
            Draft.NeutralAlignmentChoice = value;
            Raise();
            UpdateValidation();
        }
    }

    private bool _hasValidationError;
    public bool HasValidationError { get => _hasValidationError; private set => Set(ref _hasValidationError, value); }

    private string _validationMessage = string.Empty;
    public string ValidationMessage { get => _validationMessage; private set => Set(ref _validationMessage, value); }

    private bool _showNeutralAlignmentChoice;
    public bool ShowNeutralAlignmentChoice { get => _showNeutralAlignmentChoice; private set => Set(ref _showNeutralAlignmentChoice, value); }

    public bool HasSelection => Entries.Any(e => e.SelectedMiracle != null);

    public MiracleListVm(
        MiracleListDraft draft,
        IReadOnlyList<MiracleService.MiracRaw> allMiracles,
        ICharacterAdvancementDomainService domainService,
        Action onValidationChanged,
        Func<Alignment?> getAlignment,
        Func<int> getPoints,
        Action<MiracleListVm>? onExpandRequested)
    {
        Draft = draft;
        _allMiracles = allMiracles ?? Array.Empty<MiracleService.MiracRaw>();
        _domainService = domainService;
        _onValidationChanged = onValidationChanged;
        _getAlignment = getAlignment;
        _getPoints = getPoints;
        _onExpandRequested = onExpandRequested;
        _sphereLookup = BuildSphereLookup();

        foreach (var sphere in Enum.GetValues<SpiritualSpheres>())
        {
            var label = StripMajorMinorPrefix(EnumDisplayFormatter.Format(sphere));
            if (IsUniversalSphere(label))
                continue;
            SphereFilterOptions.Add(label);
        }

        AddSelectedCommand = new Command(AddSelectedMiracle);
        RemoveEntryCommand = new Command<MiracleEntryVm>(RemoveEntry);
        SaveListCommand = new Command(SaveList);
        ToggleExpandedCommand = new Command(() => _onExpandRequested?.Invoke(this));
        ReopenScripturesCommand = new Command(ReopenScriptures);

        SelectedSphereFilters.CollectionChanged += (_, __) => UpdateFilteredOptions();
        SelectedAdvancedFilters.CollectionChanged += (_, __) => UpdateFilteredOptions();

        LoadEntriesFromDraft();
        UpdateFilteredOptions();
        UpdateValidation();
    }

    private void LoadEntriesFromDraft()
    {
        Entries.Clear();
        foreach (var entry in Draft.Entries ?? new List<MiracleListEntryDraft>())
        {
            var vm = new MiracleEntryVm(entry, OnEntryChanged);
            if (!string.IsNullOrWhiteSpace(entry.Name))
            {
                var sphereLabel = MapSphereLabel(entry.Sphere);
                vm.SelectedMiracle = new MiracleOption(
                    entry.Name,
                    entry.Power,
                    entry.Alignment ?? string.Empty,
                    sphereLabel,
                    entry.IsAdvanced);
            }
            Entries.Add(vm);
        }
        ReindexEntries();
    }

    private void AddSelectedMiracle()
    {
        if (!CanAddSelected || SelectedMiracleOption == null)
            return;

        var draft = new MiracleListEntryDraft();
        Draft.Entries.Add(draft);
        var vm = new MiracleEntryVm(draft, OnEntryChanged);
        vm.SelectedMiracle = SelectedMiracleOption.Value;
        Entries.Add(vm);
        ReindexEntries();
        SearchPickerStateHelper.ClearForNextSearch<MiracleOption>(
            setSelection: v => SelectedMiracleOption = v,
            setSearchText: text => SearchText = text);
        UpdateFilteredOptions();
        UpdateValidation();
    }

    private void RemoveEntry(MiracleEntryVm? entry)
    {
        if (entry == null || !CanEdit) return;
        Entries.Remove(entry);
        Draft.Entries.Remove(entry.Draft);
        ReindexEntries();
        UpdateFilteredOptions();
        UpdateValidation();
    }

    private void ReindexEntries()
    {
        for (var i = 0; i < Entries.Count; i++)
            Entries[i].SetRowIndex(i);
    }

    private void OnEntryChanged()
    {
        UpdateFilteredOptions();
        UpdateValidation();
    }

    private void SaveList()
    {
        if (!CanSaveList)
            return;

        IsSaved = true;
    }

    private void ReopenScriptures()
    {
        if (!CanReopenScriptures)
            return;

        IsSaved = false;
        if (IsMinimized)
            _onExpandRequested?.Invoke(this);
    }

    private void UpdateFilteredOptions()
    {
        var filtered = _allMiracles;

        var allowedAlignments = _domainService.GetAllowedMiracleAlignments(
            _getAlignment(),
            Draft.Entries ?? new List<MiracleListEntryDraft>(),
            lockTrueNeutral: true);
        filtered = filtered
            .Where(m => allowedAlignments.Contains(_domainService.NormalizeAlignmentToken(m.alignment)))
            .ToList();

        if (SelectedSphereFilters.Count > 0)
        {
            var sphereSet = SelectedSphereFilters
                .Select(NormalizeToken)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            filtered = filtered.Where(m =>
            {
                var label = MapSphereLabel(m.sphere);
                if (IsUniversalSphere(label))
                    return true;
                return sphereSet.Contains(NormalizeToken(label));
            }).ToList();
        }

        var advanced = SelectedAdvancedFilters.Any(x => x.Equals("Advanced", StringComparison.OrdinalIgnoreCase));
        var handbook = SelectedAdvancedFilters.Any(x => x.Equals("Handbook", StringComparison.OrdinalIgnoreCase));
        if (advanced ^ handbook)
            filtered = filtered.Where(m => m.isAdvanced == advanced).ToList();

        var dict = new Dictionary<string, MiracleOption>(StringComparer.OrdinalIgnoreCase);
        foreach (var miracle in filtered)
        {
            if (string.IsNullOrWhiteSpace(miracle?.name))
                continue;

            var option = new MiracleOption(
                miracle.name ?? string.Empty,
                miracle.power,
                miracle.alignment ?? string.Empty,
                MapSphereLabel(miracle.sphere),
                miracle.isAdvanced);

            var label = $"{miracle.name} ({miracle.power})";
            if (!dict.ContainsKey(label))
                dict[label] = option;
        }

        FilteredOptions = dict;
    }

    private string MapSphereLabel(string raw)
    {
        var cleaned = StripMajorMinorPrefix(raw);
        var key = NormalizeToken(cleaned);
        if (_sphereLookup.TryGetValue(key, out var label))
            return label;

        return cleaned?.Trim() ?? string.Empty;
    }

    private static bool IsUniversalSphere(string? label)
        => NormalizeToken(label) == "universal";

    private static string StripMajorMinorPrefix(string? raw)
    {
        var text = raw?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        if (text.StartsWith("Major", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("Minor", StringComparison.OrdinalIgnoreCase))
        {
            if (text.Length <= 5)
                return string.Empty;

            var trimmed = text.Substring(5).TrimStart(' ', ':', '-');
            return trimmed.Trim();
        }

        return text;
    }

    private static string NormalizeToken(string? value)
    {
        var text = value ?? string.Empty;
        var chars = text.Where(char.IsLetterOrDigit).ToArray();
        return new string(chars).ToLowerInvariant();
    }

    private Dictionary<string, string> BuildSphereLookup()
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sphere in Enum.GetValues<SpiritualSpheres>())
        {
            var label = StripMajorMinorPrefix(EnumDisplayFormatter.Format(sphere));
            var key = NormalizeToken(label);
            if (!lookup.ContainsKey(key))
                lookup[key] = label;
        }
        return lookup;
    }

    private void UpdateValidation()
    {
        var totals = _domainService.ComputeMiraclePointTotals(Entries.Select(e => e.Draft));
        var advancedSpheres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Draft.Name))
                continue;

            if (entry.Draft.IsAdvanced)
            {
                var sphere = MapSphereLabel(entry.Draft.Sphere);
                if (!string.IsNullOrWhiteSpace(sphere))
                    advancedSpheres.Add(sphere);
            }
        }

        GoodPoints = totals.Good;
        EvilPoints = totals.Evil;
        NeutralPoints = totals.Neutral;
        TotalPoints = totals.Total;
        AdvancedPoints = totals.Advanced;

        Raise(nameof(UnselectedPoints));
        Raise(nameof(GoodWidth));
        Raise(nameof(NeutralWidth));
        Raise(nameof(EvilWidth));
        Raise(nameof(UnselectedWidth));

        var hasMixed = totals.Good > 0 && totals.Evil > 0;

        var messages = new List<string>();
        if (hasMixed)
            messages.Add("Cannot mix Good and Evil miracles in the same list.");
        if (IsScriptures)
        {
            ScripturesAllowed = _domainService.GetScriptureTablesReached(_getPoints());
            if (ScripturesUsed > ScripturesAllowed)
                messages.Add($"Exceeds scriptures allowed ({ScripturesAllowed}).");

            ShowNeutralAlignmentChoice = false;
        }
        else
        {
            ScripturesAllowed = 0;

            var overTotal = TotalPoints > MaxListPower;
            var overAdvanced = AdvancedPoints > MaxAdvancedPower;
            var tooManyAdvancedSpheres = advancedSpheres.Count > 1;
            var needsNeutralChoice = totals.Good <= 10 && totals.Evil <= 10 && (totals.Good > 0 || totals.Evil > 0);
            var hasNeutralMismatch = needsNeutralChoice && string.IsNullOrWhiteSpace(Draft.NeutralAlignmentChoice);
            var neutralChoiceMismatch = false;
            if (needsNeutralChoice && !string.IsNullOrWhiteSpace(Draft.NeutralAlignmentChoice))
            {
                var choice = _domainService.NormalizeAlignmentToken(Draft.NeutralAlignmentChoice);
                neutralChoiceMismatch = (totals.Good > 0 && choice != "good") || (totals.Evil > 0 && choice != "evil");
            }

            if (SourceName == string.Empty)
            {
                if (overTotal)
                    messages.Add($"Exceeds {MaxListPower} spirit limit.");
                if (overAdvanced)
                    messages.Add($"Advanced miracles exceed {MaxAdvancedPower} spirit limit.");
                if (tooManyAdvancedSpheres)
                    messages.Add("Advanced miracles must be from a single Sphere.");
                if (hasNeutralMismatch)
                    messages.Add("Pick Light or Darkness alignment for this neutral list.");
                if (neutralChoiceMismatch)
                    messages.Add("Neutral alignment choice must match selected aligned miracles.");
            }

            ShowNeutralAlignmentChoice = needsNeutralChoice;
        }

        ValidationMessage = string.Join(" ", messages);
        HasValidationError = messages.Count > 0;

        Raise(nameof(EffectiveAlignment));
        Raise(nameof(CanSaveList));
        Raise(nameof(CanAddSelected));
        UpdateScripturesVisuals();
        RaiseScripturesProgressProperties();
        _onValidationChanged();
    }

    private void RaiseScripturesProgressProperties()
    {
        Raise(nameof(ScripturesUsed));
        Raise(nameof(ScripturesRemaining));
        Raise(nameof(ScripturesProgressText));
        Raise(nameof(ScripturesUnusedText));
        Raise(nameof(ScripturesUsedRatio));
        Raise(nameof(ScripturesRemainingRatio));
        Raise(nameof(HasUsedScriptures));
        Raise(nameof(ScripturesUsedWidth));
        Raise(nameof(ScripturesUnusedWidth));
    }

    private void UpdateScripturesVisuals()
    {
        var alignmentToken = ResolveScripturesAlignmentToken();
        switch (alignmentToken)
        {
            case "good":
                ScripturesFillColor = Colors.White;
                ScripturesFillStrokeColor = Color.FromArgb("#374151");
                break;
            case "evil":
                ScripturesFillColor = Color.FromArgb("#111827");
                ScripturesFillStrokeColor = Color.FromArgb("#374151");
                break;
            default:
                ScripturesFillColor = Color.FromArgb("#4B5563");
                ScripturesFillStrokeColor = Color.FromArgb("#374151");
                break;
        }
    }

    private string ResolveScripturesAlignmentToken()
    {
        var selectedEntries = Entries
            .Where(e => !string.IsNullOrWhiteSpace(e.Draft.Name))
            .ToList();
        if (selectedEntries.Count == 0)
            return "neutral";

        var hasGood = selectedEntries.Any(e => _domainService.NormalizeAlignmentToken(e.Draft.Alignment) == "good");
        var hasEvil = selectedEntries.Any(e => _domainService.NormalizeAlignmentToken(e.Draft.Alignment) == "evil");
        if (hasGood && !hasEvil)
            return "good";
        if (hasEvil && !hasGood)
            return "evil";

        return "neutral";
    }

    public void RefreshExternalLimits()
    {
        UpdateFilteredOptions();
        UpdateValidation();
    }

    public bool IsAlignmentCompatible(Alignment? alignment)
    {
        if (HasValidationError)
            return false;

        if (!alignment.HasValue)
            return true;

        return _domainService.AreMiracleEntriesAlignmentCompatible(alignment, Entries.Select(e => e.Draft));
    }
}

public sealed class MiracleEntryVm : INotifyPropertyChanged
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

    private readonly Action _onChanged;

    public MiracleListEntryDraft Draft { get; }

    public string DisplayText
        => string.IsNullOrWhiteSpace(Draft.Name) ? string.Empty : $"{Draft.Name} ({Draft.Power})";
    public string NameText => Draft.Name ?? string.Empty;
    public string PowerText => string.IsNullOrWhiteSpace(Draft.Name) ? string.Empty : Draft.Power.ToString();

    public bool HasMiracle => !string.IsNullOrWhiteSpace(Draft.Name);
    private int _rowIndex;
    public Color RowBackgroundColor => (_rowIndex % 2) == 0 ? Colors.White : Color.FromArgb("#FAF8F3");

    private MiracleOption? _selectedMiracle;
    public MiracleOption? SelectedMiracle
    {
        get => _selectedMiracle;
        set
        {
            if (!Set(ref _selectedMiracle, value)) return;

            if (value == null)
            {
                Draft.Name = string.Empty;
                Draft.Power = 0;
                Draft.Alignment = string.Empty;
                Draft.Sphere = string.Empty;
                Draft.IsAdvanced = false;
            }
            else
            {
                Draft.Name = value.Value.Name;
                Draft.Power = value.Value.Power;
                Draft.Alignment = value.Value.Alignment;
                Draft.Sphere = value.Value.Sphere;
                Draft.IsAdvanced = value.Value.IsAdvanced;
            }

            Raise(nameof(DisplayText));
            Raise(nameof(NameText));
            Raise(nameof(PowerText));
            Raise(nameof(HasMiracle));
            _onChanged();
        }
    }

    public MiracleEntryVm(MiracleListEntryDraft draft, Action onChanged)
    {
        Draft = draft;
        _onChanged = onChanged;
    }

    public void SetRowIndex(int rowIndex)
    {
        if (_rowIndex == rowIndex)
            return;

        _rowIndex = rowIndex;
        Raise(nameof(RowBackgroundColor));
    }
}

public readonly record struct MiracleOption(string Name, int Power, string Alignment, string Sphere, bool IsAdvanced);
