using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using labyItems.Controls;
using labyItems.Helpers;
using labyItems.Models.Characters;
using labyItems.Models.Enums;
using labyItems.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace labyItems.Pages.Characters.ViewModels;

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
    private bool _isAddingSelected;

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
    public string CenterColumnHeader => IsSpecialistList ? "Colour" : "Level";

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
        && !_isAddingSelected
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

        AddSelectedCommand = new Command(async () => await AddSelectedSpellAsync());
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
            var vm = new SpellEntryVm(entry, OnEntryChanged, _getCasterLevel, IsSpecialistList);
            if (!IsReadOnly && !string.IsNullOrWhiteSpace(entry.Name))
                vm.SelectedSpell = new SpellOption(entry.Name, entry.Level, entry.Colour ?? string.Empty, entry.IsAdvanced);
            Entries.Add(vm);
        }
        ReindexEntries();
    }

    private async Task AddSelectedSpellAsync()
    {
        if (!CanAddSelected || SelectedSpellOption == null)
            return;

        _isAddingSelected = true;
        Raise(nameof(CanAddSelected));
        try
        {
            var selectedOption = SelectedSpellOption.Value;
            if (IsSpecialistList && !WizardSpellRules.TryExtractSingleMagicColour(selectedOption.Colour, out _))
            {
                var chosenColour = await PromptForSpecialistColourAsync(selectedOption.Name);
                if (!chosenColour.HasValue)
                    return;

                selectedOption = selectedOption with { Colour = chosenColour.Value.ToString() };
            }

            var draft = new SpellListEntryDraft();
            Draft.Entries.Add(draft);
            var vm = new SpellEntryVm(draft, OnEntryChanged, _getCasterLevel, IsSpecialistList);
            vm.SelectedSpell = selectedOption;
            Entries.Add(vm);
            ReindexEntries();
            SearchPickerStateHelper.ClearForNextSearch<SpellOption>(
                setSelection: v => SelectedSpellOption = v,
                setSearchText: text => SearchText = text);
        }
        finally
        {
            _isAddingSelected = false;
            Raise(nameof(CanAddSelected));
        }
    }

    private static async Task<MagicColours?> PromptForSpecialistColourAsync(string spellName)
    {
        var page = Shell.Current?.CurrentPage ?? Application.Current?.MainPage;
        if (page == null)
            return null;

        var colourOptions = Enum.GetValues<MagicColours>()
            .Select(c => (Colour: c, Label: EnumDisplayFormatter.Format(c)))
            .ToList();
        var options = colourOptions
            .Select(c => c.Label)
            .ToArray();

        var title = string.IsNullOrWhiteSpace(spellName)
            ? "Choose a magic colour"
            : $"Choose a magic colour for {spellName}";
        var picked = await page.DisplayActionSheet(title, "Cancel", null, options);
        if (string.IsNullOrWhiteSpace(picked) || picked.Equals("Cancel", StringComparison.OrdinalIgnoreCase))
            return null;

        var match = colourOptions.FirstOrDefault(c => string.Equals(c.Label, picked, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(match.Label))
            return match.Colour;

        return WizardSpellRules.TryParseMagicColour(picked, out var parsedColour)
            ? parsedColour
            : null;
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
    private readonly bool _showColourInCenterColumn;

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
            Raise(nameof(CenterColumnText));
            Raise(nameof(HasSpell));
            _onChanged();
        }
    }

    public string DisplayText
        => string.IsNullOrWhiteSpace(Draft.Name) ? string.Empty : $"{Draft.Name} (Lvl {Draft.Level})";
    public string NameText => Draft.Name ?? string.Empty;
    public string LevelText => string.IsNullOrWhiteSpace(Draft.Name) ? string.Empty : Draft.Level.ToString();
    public string CenterColumnText => _showColourInCenterColumn
        ? ResolveCenterColourText()
        : LevelText;

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

    public SpellEntryVm(SpellListEntryDraft draft, Action onChanged, Func<int> getCasterLevel, bool showColourInCenterColumn = false)
    {
        Draft = draft;
        _onChanged = onChanged;
        _getCasterLevel = getCasterLevel ?? (() => 0);
        _showColourInCenterColumn = showColourInCenterColumn;
        RefreshLearningWarning();
    }

    private string ResolveCenterColourText()
    {
        var colour = (Draft.Colour ?? string.Empty).Trim();
        if (colour.Length > 0)
            return colour;

        return LevelText;
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

public sealed class ScriptureSegmentVm
{
    public int Weight { get; }
    public Color Colour { get; }
    public bool IsUnselected { get; }

    public ScriptureSegmentVm(int weight, Color colour, bool isUnselected = false)
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
