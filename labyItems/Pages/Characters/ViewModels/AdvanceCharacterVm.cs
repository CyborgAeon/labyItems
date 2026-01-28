using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Windows.Input;
using System.Threading.Tasks;
using labyItems.Controls;
using labyItems.Models.Characters;
using labyItems.Models.Enums;
using labyItems.Services;
using Microsoft.Maui.Controls;
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

    private readonly CharacterDraft _draft;
    private IReadOnlyList<MiracleService.MiracRaw> _allMiracles = Array.Empty<MiracleService.MiracRaw>();
    private IReadOnlyList<SpellService.SpellRaw> _allSpells = Array.Empty<SpellService.SpellRaw>();
    private IReadOnlyList<DruidEvocationService.EvocRaw> _allEvocations = Array.Empty<DruidEvocationService.EvocRaw>();
    private Dictionary<string, GuildRecord> _guilds = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, ServiceCharacterClassRecord> _classes = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, ManuAbilityOption> _abilityOptions = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, ManuAbilityOption> _abilityOptionsByName = new(StringComparer.OrdinalIgnoreCase);

    private bool _showSpellsTab;
    public bool ShowSpellsTab { get => _showSpellsTab; private set => Set(ref _showSpellsTab, value); }

    private bool _showMiraclesTab;
    public bool ShowMiraclesTab { get => _showMiraclesTab; private set => Set(ref _showMiraclesTab, value); }

    private bool _showEvocationsTab;
    public bool ShowEvocationsTab { get => _showEvocationsTab; private set => Set(ref _showEvocationsTab, value); }

    public AdvanceCharacterVm(CharacterDraft draft)
    {
        _draft = draft;

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

        AddSpellListCommand = new Command(AddSpellList);
        RemoveSpellListCommand = new Command<SpellListVm>(RemoveSpellList);

        AddMiracleListCommand = new Command(AddMiracleList);
        RemoveMiracleListCommand = new Command<MiracleListVm>(RemoveMiracleList);

        AddEvocationListCommand = new Command(AddEvocationList);
        RemoveEvocationListCommand = new Command<EvocationListVm>(RemoveEvocationList);

        SaveCommand = new Command(SaveDraft, () => CanSave);
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
    public string AbilityPointsSummary => $"Points spent: {AbilityPointsSpent} / {Points}";

    public ObservableCollection<ItemLineVm> Items { get; } = new();
    public ICommand AddItemCommand { get; }
    public ICommand RemoveItemCommand { get; }

    public ObservableCollection<SpellListVm> SpellLists { get; } = new();
    public ICommand AddSpellListCommand { get; }
    public ICommand RemoveSpellListCommand { get; }

    public ObservableCollection<MiracleListVm> MiracleLists { get; } = new();
    public ICommand AddMiracleListCommand { get; }
    public ICommand RemoveMiracleListCommand { get; }

    public ObservableCollection<EvocationListVm> EvocationLists { get; } = new();
    public ICommand AddEvocationListCommand { get; }
    public ICommand RemoveEvocationListCommand { get; }

    public ICommand SaveCommand { get; }

    public bool SpellListsEnabled => false;
    public string SpellListsDisabledReason => "Spell list builder is disabled for now.";

    public bool CanAddMiracleList
        => GetBaseMiracleList() == null
           || (GetBaseMiracleList()?.IsSaved == true && GetScripturesMiracleList() == null);

    public string AddMiracleListLabel
        => GetBaseMiracleList() == null ? "+ Add miracle list" : "Add Scriptures of Faith";

    public bool CanAddEvocationList => EvocationLists.Count == 0;

    public bool CanSave => MiracleLists.All(m => m.IsAlignmentCompatible(_draft.Alignment));

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

        if (SpellListsEnabled)
        {
            try
            {
                _allSpells = await SpellService.GetAllAsync();
            }
            catch
            {
                _allSpells = Array.Empty<SpellService.SpellRaw>();
            }
        }
        else
        {
            _allSpells = Array.Empty<SpellService.SpellRaw>();
        }

        try
        {
            _allEvocations = await DruidEvocationService.GetAllAsync();
        }
        catch
        {
            _allEvocations = Array.Empty<DruidEvocationService.EvocRaw>();
        }

        try
        {
            var allAbilities = await ManuAbilityService.GetAllAsync();
            _abilityOptionsByName = allAbilities
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

            AbilityOptions = _abilityOptionsByName
                .ToDictionary(
                    kvp => BuildAbilityOptionLabel(kvp.Value),
                    kvp => kvp.Value,
                    StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            AbilityOptions = new Dictionary<string, ManuAbilityOption>(StringComparer.OrdinalIgnoreCase);
            _abilityOptionsByName = new Dictionary<string, ManuAbilityOption>(StringComparer.OrdinalIgnoreCase);
        }

        LoadAbilitiesFromDraft();
        LoadItemsFromDraft();
        if (SpellListsEnabled)
            LoadSpellListsFromDraft();
        LoadMiracleListsFromDraft();
        LoadEvocationListsFromDraft();

        UpdateTabVisibility();

        Raise(nameof(CanSave));
        (SaveCommand as Command)?.ChangeCanExecute();
        UpdateAbilityPoints();
    }

    private void UpdateTabVisibility()
    {
        ServiceCharacterClassRecord? classRecord = null;
        if (!string.IsNullOrWhiteSpace(_draft.Class))
            _classes.TryGetValue(_draft.Class.Trim(), out classRecord);

        var isWizard = HasBracket(classRecord?.Brackets, "Wizard");
        var isPriest = HasBracket(classRecord?.Brackets, "Priest");
        var isDruid = HasBracket(classRecord?.Brackets, "Druid");

        ShowSpellsTab = isWizard && HasWizardColourSelection();
        ShowMiraclesTab = isPriest;
        ShowEvocationsTab = isDruid;
    }

    private bool HasWizardColourSelection()
    {
        if (_draft.SpecialisationSelections != null)
        {
            var hasSelection = _draft.SpecialisationSelections.Any(kvp =>
                kvp.Key.Contains("Wizard Colour", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(kvp.Value));
            if (hasSelection)
                return true;
        }

        if (_draft.Abilities != null)
        {
            var hasAbility = _draft.Abilities.Any(a =>
                !string.IsNullOrWhiteSpace(a?.Source)
                && a.Source.Contains("Specialisation:Wizard Colour", StringComparison.OrdinalIgnoreCase));
            if (hasAbility)
                return true;
        }

        return false;
    }

    private static bool HasBracket(IEnumerable<string>? brackets, string token)
        => brackets != null && brackets.Any(b => b.Contains(token, StringComparison.OrdinalIgnoreCase));

    private void LoadAbilitiesFromDraft()
    {
        Abilities.Clear();
        foreach (var ability in _draft.AdvancementAbilities ?? new List<string>())
        {
            var line = BuildAbilityEntry(ability);
            Abilities.Add(line);
        }
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
        Raise(nameof(AbilityPointsSpent));
        Raise(nameof(AbilityPointsSummary));
    }

    private static string BuildAbilityOptionLabel(ManuAbilityOption option)
    {
        var name = option.Name ?? string.Empty;
        return $"{name} ({option.Cost})";
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

    private void LoadSpellListsFromDraft()
    {
        SpellLists.Clear();
        if (!SpellListsEnabled)
            return;

        foreach (var list in _draft.SpellLists ?? new List<SpellListDraft>())
        {
            var vm = new SpellListVm(list, _allSpells);
            SpellLists.Add(vm);
        }

        if (SpellLists.Count == 0)
            AddSpellList();
    }

    private void AddSpellList()
    {
        if (!SpellListsEnabled)
            return;

        var draft = new SpellListDraft
        {
            Name = $"Spell List {SpellLists.Count + 1}"
        };
        _draft.SpellLists.Add(draft);
        var vm = new SpellListVm(draft, _allSpells);
        SpellLists.Add(vm);
    }

    private void RemoveSpellList(SpellListVm? list)
    {
        if (list == null) return;
        SpellLists.Remove(list);
        _draft.SpellLists.Remove(list.Draft);
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

        if (_draft.MiracleLists.Count == 0)
        {
            var churchList = TryBuildChurchMiracleList();
            if (churchList != null)
            {
                churchList.IsSaved = true;
                churchList.IsScriptures = false;
                _draft.MiracleLists.Add(churchList);
            }
        }

        var baseList = _draft.MiracleLists.FirstOrDefault(l => !l.IsScriptures);
        var scripturesList = _draft.MiracleLists.FirstOrDefault(l => l.IsScriptures);

        _draft.MiracleLists = new List<MiracleListDraft>();
        if (baseList != null)
            _draft.MiracleLists.Add(baseList);
        if (scripturesList != null)
            _draft.MiracleLists.Add(scripturesList);

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

    private void LoadEvocationListsFromDraft()
    {
        EvocationLists.Clear();
        if (_draft.EvocationLists == null)
            _draft.EvocationLists = new List<EvocationListDraft>();

        var first = _draft.EvocationLists.FirstOrDefault();
        _draft.EvocationLists = first != null ? new List<EvocationListDraft> { first } : new List<EvocationListDraft>();

        if (first != null)
        {
            var vm = new EvocationListVm(first, _allEvocations);
            EvocationLists.Add(vm);
        }

        Raise(nameof(CanAddEvocationList));
    }

    private void AddEvocationList()
    {
        if (!CanAddEvocationList)
            return;

        var draft = new EvocationListDraft
        {
            Name = $"Evocation List {EvocationLists.Count + 1}"
        };
        _draft.EvocationLists.Add(draft);
        var vm = new EvocationListVm(draft, _allEvocations);
        EvocationLists.Add(vm);
        Raise(nameof(CanAddEvocationList));
    }

    private void RemoveEvocationList(EvocationListVm? list)
    {
        if (list == null) return;
        EvocationLists.Remove(list);
        _draft.EvocationLists.Remove(list.Draft);
        Raise(nameof(CanAddEvocationList));
    }

    private void OnMiracleListValidationChanged()
    {
        Raise(nameof(CanSave));
        (SaveCommand as Command)?.ChangeCanExecute();
        RaiseMiracleListStateChanged();
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

        LiteDbService.UpsertDraft(_draft);
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

    public string Name
    {
        get => _name;
        set
        {
            var next = value ?? string.Empty;
            if (_name == next) return;
            _name = next;
            Raise();
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
            _onChanged();
        }
    }

    public AbilityEntryVm(string name, int cost, Action onChanged)
    {
        _name = name ?? string.Empty;
        _cost = cost;
        _onChanged = onChanged;
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

    public SpellListDraft Draft { get; }

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

    public ObservableCollection<SpellEntryVm> Entries { get; } = new();

    private Dictionary<string, SpellOption> _filteredOptions = new();
    public Dictionary<string, SpellOption> FilteredOptions
    {
        get => _filteredOptions;
        private set => Set(ref _filteredOptions, value);
    }

    public ICommand AddEntryCommand { get; }
    public ICommand RemoveEntryCommand { get; }

    public SpellListVm(SpellListDraft draft, IReadOnlyList<SpellService.SpellRaw> allSpells)
    {
        Draft = draft;
        _allSpells = allSpells ?? Array.Empty<SpellService.SpellRaw>();

        AddEntryCommand = new Command(AddEntry);
        RemoveEntryCommand = new Command<SpellEntryVm>(RemoveEntry);

        LoadEntriesFromDraft();
        UpdateFilteredOptions();
    }

    private void LoadEntriesFromDraft()
    {
        Entries.Clear();
        foreach (var entry in Draft.Entries ?? new List<SpellListEntryDraft>())
        {
            var vm = new SpellEntryVm(entry, OnEntryChanged);
            if (!string.IsNullOrWhiteSpace(entry.Name))
                vm.SelectedSpell = new SpellOption(entry.Name, entry.Level, entry.Colour ?? string.Empty, entry.IsAdvanced);
            Entries.Add(vm);
        }

        if (Entries.Count == 0)
            AddEntry();
    }

    private void AddEntry()
    {
        var draft = new SpellListEntryDraft();
        Draft.Entries.Add(draft);
        var vm = new SpellEntryVm(draft, OnEntryChanged);
        Entries.Add(vm);
    }

    private void RemoveEntry(SpellEntryVm? entry)
    {
        if (entry == null) return;
        Entries.Remove(entry);
        Draft.Entries.Remove(entry.Draft);
    }

    private void OnEntryChanged()
    {
        UpdateFilteredOptions();
    }

    private void UpdateFilteredOptions()
    {
        var dict = new Dictionary<string, SpellOption>(StringComparer.OrdinalIgnoreCase);
        foreach (var spell in _allSpells)
        {
            if (string.IsNullOrWhiteSpace(spell?.name))
                continue;

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

            _onChanged();
        }
    }

    public SpellEntryVm(SpellListEntryDraft draft, Action onChanged)
    {
        Draft = draft;
        _onChanged = onChanged;
    }
}

public readonly record struct SpellOption(string Name, int Level, string Colour, bool IsAdvanced);

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

    public EvocationListDraft Draft { get; }

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

    public bool CanAddSelected => SelectedEvocationOption != null;

    public ObservableCollection<string> FieldFilterOptions { get; } = new();
    public ObservableCollection<string> SelectedFieldFilters { get; } = new();
    public ObservableCollection<string> TierFilterOptions { get; } = new() { "Advanced", "Standard" };
    public ObservableCollection<string> SelectedTierFilters { get; } = new();

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

    public EvocationListVm(EvocationListDraft draft, IReadOnlyList<DruidEvocationService.EvocRaw> allEvocations)
    {
        Draft = draft;
        _allEvocations = allEvocations ?? Array.Empty<DruidEvocationService.EvocRaw>();
        _evocationLookup = _allEvocations
            .Where(e => !string.IsNullOrWhiteSpace(e?.name))
            .GroupBy(e => e.name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        AddSelectedCommand = new Command(AddSelectedEvocation);
        RemoveEntryCommand = new Command<EvocationEntryVm>(RemoveEntry);

        SelectedFieldFilters.CollectionChanged += (_, __) => UpdateFilteredOptions();
        SelectedTierFilters.CollectionChanged += (_, __) => UpdateFilteredOptions();

        LoadFieldOptions();
        LoadEntriesFromDraft();
        UpdateFilteredOptions();
        UpdateStats();
    }

    private void LoadFieldOptions()
    {
        FieldFilterOptions.Clear();
        var fields = _allEvocations
            .SelectMany(e => e.fields ?? new List<string>())
            .Select(f => (f ?? string.Empty).Trim())
            .Where(f => f.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var field in fields)
            FieldFilterOptions.Add(field);
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
        SelectedEvocationOption = null;
        UpdateStats();
    }

    private void RemoveEntry(EvocationEntryVm? entry)
    {
        if (entry == null) return;
        Entries.Remove(entry);
        Draft.Entries.Remove(entry.Draft);
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
                var fields = ev.fields ?? new List<string>();
                if (!fields.Any(f => selectedFields.Contains(f)))
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

    private void UpdateStats()
    {
        var total = Draft.Entries.Sum(e => Math.Max(0, e.Power));
        var advanced = Draft.Entries.Where(e => e.IsAdvanced).Sum(e => Math.Max(0, e.Power));

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
                HashSet<string>? commonFields = null;
                foreach (var entry in advancedEntries)
                {
                    if (!_evocationLookup.TryGetValue(entry.Name, out var ev) || ev.fields == null || ev.fields.Count == 0)
                    {
                        commonFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        break;
                    }

                    if (commonFields == null)
                        commonFields = new HashSet<string>(ev.fields, StringComparer.OrdinalIgnoreCase);
                    else
                        commonFields.IntersectWith(ev.fields);

                    if (commonFields.Count == 0)
                        break;
                }

                if (commonFields == null || commonFields.Count == 0)
                    message = "Advanced evocations must all come from the same field.";
            }
        }

        ValidationMessage = message;
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
        => string.IsNullOrWhiteSpace(Draft.Name) ? string.Empty : $"{Draft.Name} ({Draft.Power})";

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
            _onChanged();
        }
    }

    public EvocationEntryVm(EvocationListEntryDraft draft, Action onChanged)
    {
        Draft = draft;
        _onChanged = onChanged;
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
            Raise(nameof(ScripturesRemaining));
            Raise(nameof(ScripturesUsed));
            Raise(nameof(ScripturesProgressText));
            Raise(nameof(CanAddSelected));
        }
    }

    public int ScripturesUsed => Entries.Count(e => !string.IsNullOrWhiteSpace(e.Draft.Name));

    public int ScripturesRemaining => Math.Max(0, ScripturesAllowed - ScripturesUsed);

    public string ScripturesProgressText => $"Scriptures remaining: {ScripturesRemaining}/{ScripturesAllowed}";

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
        Action onValidationChanged,
        Func<Alignment?> getAlignment,
        Func<int> getPoints,
        Action<MiracleListVm>? onExpandRequested)
    {
        Draft = draft;
        _allMiracles = allMiracles ?? Array.Empty<MiracleService.MiracRaw>();
        _onValidationChanged = onValidationChanged;
        _getAlignment = getAlignment;
        _getPoints = getPoints;
        _onExpandRequested = onExpandRequested;
        _sphereLookup = BuildSphereLookup();

        foreach (var sphere in Enum.GetValues<SpiritualSpheres>())
            SphereFilterOptions.Add(EnumDisplayFormatter.Format(sphere));

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
        SelectedMiracleOption = null;
        UpdateFilteredOptions();
        UpdateValidation();
    }

    private void RemoveEntry(MiracleEntryVm? entry)
    {
        if (entry == null || !CanEdit) return;
        Entries.Remove(entry);
        Draft.Entries.Remove(entry.Draft);
        UpdateFilteredOptions();
        UpdateValidation();
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

        var allowedAlignments = GetAllowedAlignmentTokens(_getAlignment(), lockTrueNeutral: true);
        filtered = filtered
            .Where(m => allowedAlignments.Contains(NormalizeAlignment(m.alignment)))
            .ToList();

        if (SelectedSphereFilters.Count > 0)
        {
            var sphereSet = SelectedSphereFilters
                .Select(NormalizeToken)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            filtered = filtered.Where(m => sphereSet.Contains(NormalizeToken(MapSphereLabel(m.sphere)))).ToList();
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
        var key = NormalizeToken(raw);
        if (_sphereLookup.TryGetValue(key, out var label))
            return label;

        return raw?.Trim() ?? string.Empty;
    }

    private static string NormalizeToken(string? value)
    {
        var text = value ?? string.Empty;
        var chars = text.Where(char.IsLetterOrDigit).ToArray();
        return new string(chars).ToLowerInvariant();
    }

    private static string NormalizeAlignment(string? value)
    {
        var token = NormalizeToken(value);
        if (token.StartsWith("good", StringComparison.OrdinalIgnoreCase))
            return "good";
        if (token.StartsWith("evil", StringComparison.OrdinalIgnoreCase))
            return "evil";
        return "neutral";
    }

    private HashSet<string> GetAllowedAlignmentTokens(Alignment? alignment, bool lockTrueNeutral)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!alignment.HasValue)
        {
            allowed.Add("good");
            allowed.Add("neutral");
            allowed.Add("evil");
            return allowed;
        }

        var moral = alignment.Value.Moral;
        var order = alignment.Value.Order;

        if (moral == MoralAxis.Good)
        {
            allowed.Add("good");
            allowed.Add("neutral");
            return allowed;
        }

        if (moral == MoralAxis.Evil)
        {
            allowed.Add("evil");
            allowed.Add("neutral");
            return allowed;
        }

        if (order == OrderAxis.Lawful)
        {
            allowed.Add("good");
            allowed.Add("neutral");
            return allowed;
        }

        if (order == OrderAxis.Chaotic)
        {
            allowed.Add("evil");
            allowed.Add("neutral");
            return allowed;
        }

        // True Neutral: allow all, but optionally lock to the first non-neutral alignment chosen.
        if (lockTrueNeutral)
        {
            var hasGood = Entries.Any(e => NormalizeAlignment(e.Draft.Alignment) == "good");
            var hasEvil = Entries.Any(e => NormalizeAlignment(e.Draft.Alignment) == "evil");

            if (hasGood && !hasEvil)
            {
                allowed.Add("good");
                allowed.Add("neutral");
                return allowed;
            }

            if (hasEvil && !hasGood)
            {
                allowed.Add("evil");
                allowed.Add("neutral");
                return allowed;
            }
        }

        allowed.Add("good");
        allowed.Add("neutral");
        allowed.Add("evil");
        return allowed;
    }

    private Dictionary<string, string> BuildSphereLookup()
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sphere in Enum.GetValues<SpiritualSpheres>())
        {
            var label = EnumDisplayFormatter.Format(sphere);
            var key = NormalizeToken(label);
            if (!lookup.ContainsKey(key))
                lookup[key] = label;
        }
        return lookup;
    }

    private void UpdateValidation()
    {
        var good = 0;
        var evil = 0;
        var neutral = 0;
        var advanced = 0;
        var advancedSpheres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Draft.Name))
                continue;

            var power = Math.Max(0, entry.Draft.Power);
            var alignment = NormalizeAlignment(entry.Draft.Alignment);

            if (alignment == "good")
                good += power;
            else if (alignment == "evil")
                evil += power;
            else
                neutral += power;

            if (entry.Draft.IsAdvanced)
            {
                advanced += power;
                var sphere = MapSphereLabel(entry.Draft.Sphere);
                if (!string.IsNullOrWhiteSpace(sphere))
                    advancedSpheres.Add(sphere);
            }
        }

        GoodPoints = good;
        EvilPoints = evil;
        NeutralPoints = neutral;
        TotalPoints = good + evil + neutral;
        AdvancedPoints = advanced;

        Raise(nameof(UnselectedPoints));
        Raise(nameof(GoodWidth));
        Raise(nameof(NeutralWidth));
        Raise(nameof(EvilWidth));
        Raise(nameof(UnselectedWidth));

        var hasMixed = good > 0 && evil > 0;

        var messages = new List<string>();
        if (hasMixed)
            messages.Add("Cannot mix Good and Evil miracles in the same list.");
        if (IsScriptures)
        {
            ScripturesAllowed = GetScriptureTablesReached(_getPoints());
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
            var needsNeutralChoice = good <= 10 && evil <= 10 && (good > 0 || evil > 0);
            var hasNeutralMismatch = needsNeutralChoice && string.IsNullOrWhiteSpace(Draft.NeutralAlignmentChoice);
            var neutralChoiceMismatch = false;
            if (needsNeutralChoice && !string.IsNullOrWhiteSpace(Draft.NeutralAlignmentChoice))
            {
                var choice = NormalizeAlignment(Draft.NeutralAlignmentChoice);
                neutralChoiceMismatch = (good > 0 && choice != "good") || (evil > 0 && choice != "evil");
            }

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

            ShowNeutralAlignmentChoice = needsNeutralChoice;
        }

        ValidationMessage = string.Join(" ", messages);
        HasValidationError = messages.Count > 0;

        Raise(nameof(EffectiveAlignment));
        Raise(nameof(CanSaveList));
        Raise(nameof(CanAddSelected));
        Raise(nameof(ScripturesUsed));
        Raise(nameof(ScripturesRemaining));
        Raise(nameof(ScripturesProgressText));
        _onValidationChanged();
    }

    public static int GetScriptureTablesReached(int points)
    {
        var thresholds = new[]
        {
            0, 200, 250, 275, 450, 600, 650, 1000, 1500, 3000, 5250, 7500, 10000
        };

        return thresholds.Count(t => points >= t);
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

        var allowed = GetAllowedAlignmentTokens(alignment, lockTrueNeutral: true);
        foreach (var entry in Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Draft.Name))
                continue;

            var token = NormalizeAlignment(entry.Draft.Alignment);
            if (!allowed.Contains(token))
                return false;
        }

        return true;
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
            _onChanged();
        }
    }

    public MiracleEntryVm(MiracleListEntryDraft draft, Action onChanged)
    {
        Draft = draft;
        _onChanged = onChanged;
    }
}

public readonly record struct MiracleOption(string Name, int Power, string Alignment, string Sphere, bool IsAdvanced);
