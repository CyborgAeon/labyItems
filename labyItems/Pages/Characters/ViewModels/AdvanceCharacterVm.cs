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

    public AdvanceCharacterVm(CharacterDraft draft)
    {
        _draft = draft;

        Items.CollectionChanged += (_, __) => SyncItemsToDraft();

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

    public bool CanSave => MiracleLists.All(m => m.IsAlignmentCompatible(_draft.Alignment));

    public async Task InitializeAsync()
    {
        _guilds = await GuildsService.GetAllAsync() ?? new Dictionary<string, GuildRecord>(StringComparer.OrdinalIgnoreCase);

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

        LoadItemsFromDraft();
        if (SpellListsEnabled)
            LoadSpellListsFromDraft();
        LoadMiracleListsFromDraft();
        LoadEvocationListsFromDraft();

        Raise(nameof(CanSave));
        (SaveCommand as Command)?.ChangeCanExecute();
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

    private void LoadMiracleListsFromDraft()
    {
        MiracleLists.Clear();

        if (_draft.MiracleLists == null)
            _draft.MiracleLists = new List<MiracleListDraft>();

        if (_draft.MiracleLists.Count == 0)
        {
            var churchList = TryBuildChurchMiracleList();
            if (churchList != null)
                _draft.MiracleLists.Add(churchList);
        }

        foreach (var list in _draft.MiracleLists)
        {
            var vm = new MiracleListVm(list, _allMiracles, OnMiracleListValidationChanged);
            MiracleLists.Add(vm);
        }

        if (MiracleLists.Count == 0)
            AddMiracleList();
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

        var name = $"Miracle List ({church})";
        var list = new MiracleListDraft
        {
            Name = name,
            IsImported = true
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

    private void AddMiracleList()
    {
        var draft = new MiracleListDraft
        {
            Name = $"Miracle List {MiracleLists.Count + 1}"
        };
        _draft.MiracleLists.Add(draft);
        var vm = new MiracleListVm(draft, _allMiracles, OnMiracleListValidationChanged);
        MiracleLists.Add(vm);
        Raise(nameof(CanSave));
        (SaveCommand as Command)?.ChangeCanExecute();
    }

    private void RemoveMiracleList(MiracleListVm? list)
    {
        if (list == null) return;
        MiracleLists.Remove(list);
        _draft.MiracleLists.Remove(list.Draft);
        Raise(nameof(CanSave));
        (SaveCommand as Command)?.ChangeCanExecute();
    }

    private void LoadEvocationListsFromDraft()
    {
        EvocationLists.Clear();
        foreach (var list in _draft.EvocationLists ?? new List<EvocationListDraft>())
        {
            var vm = new EvocationListVm(list, _allEvocations);
            EvocationLists.Add(vm);
        }

        if (EvocationLists.Count == 0)
            AddEvocationList();
    }

    private void AddEvocationList()
    {
        var draft = new EvocationListDraft
        {
            Name = $"Evocation List {EvocationLists.Count + 1}"
        };
        _draft.EvocationLists.Add(draft);
        var vm = new EvocationListVm(draft, _allEvocations);
        EvocationLists.Add(vm);
    }

    private void RemoveEvocationList(EvocationListVm? list)
    {
        if (list == null) return;
        EvocationLists.Remove(list);
        _draft.EvocationLists.Remove(list.Draft);
    }

    private void OnMiracleListValidationChanged()
    {
        Raise(nameof(CanSave));
        (SaveCommand as Command)?.ChangeCanExecute();
    }

    private void SaveDraft()
    {
        if (!CanSave)
            return;

        LiteDbService.UpsertDraft(_draft);
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

    public ICommand AddEntryCommand { get; }
    public ICommand RemoveEntryCommand { get; }

    public EvocationListVm(EvocationListDraft draft, IReadOnlyList<DruidEvocationService.EvocRaw> allEvocations)
    {
        Draft = draft;
        _allEvocations = allEvocations ?? Array.Empty<DruidEvocationService.EvocRaw>();
        _evocationLookup = _allEvocations
            .Where(e => !string.IsNullOrWhiteSpace(e?.name))
            .GroupBy(e => e.name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        AddEntryCommand = new Command(AddEntry);
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

        if (Entries.Count == 0)
            AddEntry();
    }

    private void AddEntry()
    {
        var draft = new EvocationListEntryDraft();
        Draft.Entries.Add(draft);
        var vm = new EvocationEntryVm(draft, OnEntryChanged);
        Entries.Add(vm);
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

    public bool IsReadOnly => Draft.IsImported;
    public bool CanEdit => !IsReadOnly;

    public ObservableCollection<MiracleEntryVm> Entries { get; } = new();

    public ObservableCollection<string> AlignmentFilterOptions { get; } = new() { "Good", "Neutral", "Evil" };
    public ObservableCollection<string> SelectedAlignmentFilters { get; } = new();
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

    public ICommand AddEntryCommand { get; }
    public ICommand RemoveEntryCommand { get; }

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

    public MiracleListVm(MiracleListDraft draft, IReadOnlyList<MiracleService.MiracRaw> allMiracles, Action onValidationChanged)
    {
        Draft = draft;
        _allMiracles = allMiracles ?? Array.Empty<MiracleService.MiracRaw>();
        _onValidationChanged = onValidationChanged;
        _sphereLookup = BuildSphereLookup();

        foreach (var sphere in Enum.GetValues<SpiritualSpheres>())
            SphereFilterOptions.Add(EnumDisplayFormatter.Format(sphere));

        AddEntryCommand = new Command(AddEntry, () => !IsReadOnly);
        RemoveEntryCommand = new Command<MiracleEntryVm>(RemoveEntry, _ => !IsReadOnly);

        SelectedAlignmentFilters.CollectionChanged += (_, __) => UpdateFilteredOptions();
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

        if (Entries.Count == 0 && !IsReadOnly)
            AddEntry();
    }

    private void AddEntry()
    {
        if (IsReadOnly)
            return;

        var draft = new MiracleListEntryDraft();
        Draft.Entries.Add(draft);
        var vm = new MiracleEntryVm(draft, OnEntryChanged);
        Entries.Add(vm);
        UpdateValidation();
    }

    private void RemoveEntry(MiracleEntryVm? entry)
    {
        if (entry == null || IsReadOnly) return;
        Entries.Remove(entry);
        Draft.Entries.Remove(entry.Draft);
        UpdateValidation();
    }

    private void OnEntryChanged()
    {
        UpdateValidation();
    }

    private void UpdateFilteredOptions()
    {
        var filtered = _allMiracles;

        if (SelectedAlignmentFilters.Count > 0)
        {
            var alignSet = SelectedAlignmentFilters
                .Select(NormalizeAlignment)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            filtered = filtered.Where(m => alignSet.Contains(NormalizeAlignment(m.alignment))).ToList();
        }

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
            var selected = entry.SelectedMiracle;
            if (selected == null)
                continue;

            var power = Math.Max(0, selected.Value.Power);
            var alignment = NormalizeAlignment(selected.Value.Alignment);

            if (alignment == "good")
                good += power;
            else if (alignment == "evil")
                evil += power;
            else
                neutral += power;

            if (selected.Value.IsAdvanced)
            {
                advanced += power;
                if (!string.IsNullOrWhiteSpace(selected.Value.Sphere))
                    advancedSpheres.Add(selected.Value.Sphere);
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

        var messages = new List<string>();
        if (hasMixed)
            messages.Add("Cannot mix Good and Evil miracles in the same list.");
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

        ValidationMessage = string.Join(" ", messages);
        HasValidationError = messages.Count > 0;
        ShowNeutralAlignmentChoice = needsNeutralChoice;

        Raise(nameof(EffectiveAlignment));
        _onValidationChanged();
    }

    public bool IsAlignmentCompatible(Alignment? alignment)
    {
        if (HasValidationError)
            return false;

        if (!alignment.HasValue)
            return true;

        var moral = alignment.Value.Moral;
        var effective = NormalizeAlignment(EffectiveAlignment);

        if (effective == "neutral")
            return true;

        if (effective == "good")
            return moral == MoralAxis.Good || moral == MoralAxis.Neutral;

        if (effective == "evil")
            return moral == MoralAxis.Evil || moral == MoralAxis.Neutral;

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
