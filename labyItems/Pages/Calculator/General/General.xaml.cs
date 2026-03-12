using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using labyItems.Services;
using Microsoft.Maui.Graphics;
using AbilityCardPage = labyItems.Pages.AbilityCard.AbilityCard;

namespace labyItems.Pages.Calculator;

public partial class General : ContentPage
{
    private readonly GeneralAbilitySearchVm _vm = new();
    private TaskCompletionSource<IReadOnlyList<EvolutionService.AbilityResult>>? _tcs;
    private bool _isCompleting;

    public General()
    {
        InitializeComponent();
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.EnsureLoadedAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CompleteAndCloseAsync();
        return true;
    }

    private async Task CompleteAndCloseAsync()
    {
        if (_isCompleting)
            return;

        _isCompleting = true;
        try
        {
            _tcs?.TrySetResult(_vm.GetSelectedAbilities());

            if (Navigation.NavigationStack.Count > 1)
            {
                await Navigation.PopAsync();
                return;
            }

            if (Shell.Current != null)
                await Shell.Current.GoToAsync("..");
        }
        finally
        {
            _isCompleting = false;
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await CompleteAndCloseAsync();
    }

    private async void OnDoneClicked(object sender, EventArgs e)
    {
        await CompleteAndCloseAsync();
    }

    private void OnFilterChipTapped(object? sender, TappedEventArgs e)
    {
        var chip = e.Parameter as GeneralAbilityTableFilterChipVm
            ?? (sender as BindableObject)?.BindingContext as GeneralAbilityTableFilterChipVm;
        if (chip == null)
            return;

        _vm.ToggleTableFilter(chip);
    }

    private void OnResultTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not GeneralAbilitySearchResultVm row)
            return;

        _vm.ToggleSelected(row);
    }

    private async void OnInfoTapped(object sender, EventArgs e)
    {
        if (sender is not Button button
            || button.CommandParameter is not GeneralAbilitySearchResultVm row
            || row.Ability == null)
        {
            return;
        }

        await Navigation.PushAsync(new AbilityCardPage(row.Ability));
    }

    public async Task<IReadOnlyList<EvolutionService.AbilityResult>> PickAsync(
        INavigation nav,
        IEnumerable<string>? initiallySelectedAbilityNames = null)
    {
        _tcs = new TaskCompletionSource<IReadOnlyList<EvolutionService.AbilityResult>>();
        _vm.SetSelectedAbilities(initiallySelectedAbilityNames ?? Array.Empty<string>());
        await nav.PushAsync(this);
        return await _tcs.Task;
    }
}

public sealed class GeneralAbilitySearchVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        Raise(name);
        return true;
    }

    private readonly HashSet<int> _selectedTables = new();
    private readonly HashSet<string> _selectedAbilityKeys = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<EvolutionService.AbilityResult> _allAbilities = Array.Empty<EvolutionService.AbilityResult>();
    private bool _isLoaded;

    public ObservableCollection<GeneralAbilityTableFilterChipVm> TableFilterChips { get; } = new();
    public ObservableCollection<GeneralAbilitySearchResultVm> FilteredResults { get; } = new();

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (!Set(ref _isLoading, value))
                return;

            Raise(nameof(HasNoResults));
        }
    }

    public bool HasNoResults => !IsLoading && FilteredResults.Count == 0;

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!Set(ref _searchText, value ?? string.Empty))
                return;

            ApplyFilters();
        }
    }

    public string DoneButtonText => _selectedAbilityKeys.Count > 0
        ? $"Done ({_selectedAbilityKeys.Count})"
        : "Done";

    public async Task EnsureLoadedAsync()
    {
        if (_isLoaded)
        {
            ApplyFilters();
            return;
        }

        IsLoading = true;
        var abilities = await EvolutionService.GetAllAbilitiesAsync();
        _allAbilities = (abilities ?? Array.Empty<EvolutionService.AbilityResult>())
            .Where(ability => ability != null && ability.Cost > 0)
            .OrderBy(ability => ability.Table)
            .ThenBy(ability => ability.Index, StringComparer.OrdinalIgnoreCase)
            .ToList();

        RebuildTableChips();
        _isLoaded = true;
        IsLoading = false;
        ApplyFilters();
    }

    public void ToggleTableFilter(GeneralAbilityTableFilterChipVm chip)
    {
        if (chip == null)
            return;

        if (_selectedTables.Contains(chip.Table))
            _selectedTables.Remove(chip.Table);
        else
            _selectedTables.Add(chip.Table);

        foreach (var tableChip in TableFilterChips)
            tableChip.IsSelected = _selectedTables.Contains(tableChip.Table);

        ApplyFilters();
    }

    public void ToggleSelected(GeneralAbilitySearchResultVm result)
    {
        if (result?.Ability == null)
            return;

        var key = NormalizeAbilityKey(result.Ability.Index);
        if (key.Length == 0)
            return;

        if (_selectedAbilityKeys.Contains(key))
            _selectedAbilityKeys.Remove(key);
        else
            _selectedAbilityKeys.Add(key);

        result.IsSelected = _selectedAbilityKeys.Contains(key);
        Raise(nameof(DoneButtonText));
    }

    public IReadOnlyList<EvolutionService.AbilityResult> GetSelectedAbilities()
    {
        if (_selectedAbilityKeys.Count == 0)
            return Array.Empty<EvolutionService.AbilityResult>();

        return _allAbilities
            .Where(ability => _selectedAbilityKeys.Contains(NormalizeAbilityKey(ability.Index)))
            .ToList();
    }

    public void SetSelectedAbilities(IEnumerable<string> abilityNames)
    {
        _selectedAbilityKeys.Clear();
        foreach (var key in (abilityNames ?? Array.Empty<string>())
            .Select(NormalizeAbilityKey)
            .Where(key => key.Length > 0))
        {
            _selectedAbilityKeys.Add(key);
        }

        foreach (var row in FilteredResults)
        {
            row.IsSelected = row.Ability != null
                && _selectedAbilityKeys.Contains(NormalizeAbilityKey(row.Ability.Index));
        }

        Raise(nameof(DoneButtonText));
    }

    private void RebuildTableChips()
    {
        TableFilterChips.Clear();
        foreach (var table in _allAbilities.Select(ability => ability.Table).Distinct().OrderBy(table => table))
        {
            TableFilterChips.Add(new GeneralAbilityTableFilterChipVm(table, $"T{table}"));
        }
    }

    private void ApplyFilters()
    {
        if (_allAbilities.Count == 0)
        {
            FilteredResults.Clear();
            Raise(nameof(HasNoResults));
            return;
        }

        var query = (SearchText ?? string.Empty).Trim();
        var hasQuery = query.Length > 0;
        var hasTableFilters = _selectedTables.Count > 0;

        IEnumerable<EvolutionService.AbilityResult> filtered = _allAbilities;
        if (hasTableFilters)
            filtered = filtered.Where(ability => _selectedTables.Contains(ability.Table));

        if (hasQuery)
        {
            filtered = filtered.Where(ability =>
                Matches(ability.Index, query)
                || Matches(ability.Description, query)
                || Matches($"table {ability.Table}", query)
                || Matches($"cost {ability.Cost}", query));
        }

        var rows = filtered
            .Select(ability => new GeneralAbilitySearchResultVm(ability)
            {
                IsSelected = _selectedAbilityKeys.Contains(NormalizeAbilityKey(ability.Index))
            })
            .ToList();

        FilteredResults.Clear();
        foreach (var row in rows)
            FilteredResults.Add(row);

        Raise(nameof(HasNoResults));
        Raise(nameof(DoneButtonText));
    }

    private static bool Matches(string? source, string query)
        => !string.IsNullOrWhiteSpace(source)
           && source.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeAbilityKey(string? name)
        => new string((name ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
}

public sealed class GeneralAbilityTableFilterChipVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public int Table { get; }
    public string Label { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            Raise();
        }
    }

    public GeneralAbilityTableFilterChipVm(int table, string label)
    {
        Table = table;
        Label = label;
    }
}

public sealed class GeneralAbilitySearchResultVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public EvolutionService.AbilityResult Ability { get; }
    public string Name => Ability.Index;
    public string MetaText => $"Table {Ability.Table} • Cost {Ability.Cost}";
    public string DescriptionText => Ability.Description ?? string.Empty;
    public bool HasDescription => !string.IsNullOrWhiteSpace(DescriptionText);

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            Raise();
            Raise(nameof(SelectionGlyph));
            Raise(nameof(SelectionGlyphColor));
            Raise(nameof(SelectionBackgroundColor));
            Raise(nameof(SelectionBorderColor));
        }
    }

    public string SelectionGlyph => IsSelected ? "\uf00c" : string.Empty;
    public Color SelectionGlyphColor => IsSelected ? Colors.White : Colors.Transparent;
    public Color SelectionBackgroundColor => IsSelected ? Color.FromArgb("#7F1D1D") : Colors.Transparent;
    public Color SelectionBorderColor => IsSelected ? Color.FromArgb("#530000") : Color.FromArgb("#D1D5DB");

    public GeneralAbilitySearchResultVm(EvolutionService.AbilityResult ability)
    {
        Ability = ability;
    }
}
