using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using labyItems.Services;
using Microsoft.Maui.Graphics;
using AbilityCardPage = labyItems.Pages.AbilityCard.AbilityCard;

namespace labyItems.Pages.Calculator;

public partial class General : ContentPage
{
    private readonly GeneralAbilitySearchVm _vm = new();
    private TaskCompletionSource<IReadOnlyList<EvolutionService.AbilityResult>>? _tcs;
    private bool _isCompleting;
    public ICommand BackNavigationCommand { get; }

    public General()
    {
        BackNavigationCommand = new Command(async () => await CompleteAndCloseAsync());
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
    private readonly HashSet<string> _selectedSourceBooks = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _selectedAbilityKeys = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<EvolutionService.AbilityResult> _allAbilities = Array.Empty<EvolutionService.AbilityResult>();
    private bool _isLoaded;

    public ObservableCollection<GeneralAbilityTableFilterChipVm> TableFilterChips { get; } = new();
    public ObservableCollection<GeneralAbilitySourceBookFilterChipVm> SourceBookFilterChips { get; } = new();
    public ObservableCollection<GeneralAbilitySearchResultVm> FilteredResults { get; } = new();

    public ICommand OpenFiltersCommand { get; }
    public ICommand CancelFiltersCommand { get; }
    public ICommand ApplyFiltersCommand { get; }
    public ICommand ToggleSourceBookFilterCommand { get; }
    public ICommand ToggleTableFilterCommand { get; }

    public GeneralAbilitySearchVm()
    {
        OpenFiltersCommand = new Command(OpenFilters);
        CancelFiltersCommand = new Command(CancelFilters);
        ApplyFiltersCommand = new Command(ApplyFilterModal);
        ToggleSourceBookFilterCommand = new Command<GeneralAbilitySourceBookFilterChipVm>(ToggleSourceBookFilter);
        ToggleTableFilterCommand = new Command<GeneralAbilityTableFilterChipVm>(TogglePendingTableFilter);
    }

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

    private bool _isFilterModalOpen;
    public bool IsFilterModalOpen
    {
        get => _isFilterModalOpen;
        private set => Set(ref _isFilterModalOpen, value);
    }

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
            .ThenBy(ability => NormalizeSourceBook(ability.SourceBook), StringComparer.OrdinalIgnoreCase)
            .ThenBy(ability => ability.Index, StringComparer.OrdinalIgnoreCase)
            .ToList();

        RebuildFilterChips();
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

        SyncFilterChipSelection();
        ApplyFilters();
    }

    public void ToggleSourceBookFilter(GeneralAbilitySourceBookFilterChipVm? chip)
    {
        if (chip == null)
            return;

        chip.IsSelected = !chip.IsSelected;
    }

    private static void TogglePendingTableFilter(GeneralAbilityTableFilterChipVm? chip)
    {
        if (chip == null)
            return;

        chip.IsSelected = !chip.IsSelected;
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

    private void OpenFilters()
    {
        SyncFilterChipSelection();
        IsFilterModalOpen = true;
    }

    private void CancelFilters()
    {
        SyncFilterChipSelection();
        IsFilterModalOpen = false;
    }

    private void ApplyFilterModal()
    {
        _selectedSourceBooks.Clear();
        foreach (var chip in SourceBookFilterChips.Where(chip => chip.IsSelected))
            _selectedSourceBooks.Add(chip.SourceBook);

        _selectedTables.Clear();
        foreach (var chip in TableFilterChips.Where(chip => chip.IsSelected))
            _selectedTables.Add(chip.Table);

        IsFilterModalOpen = false;
        ApplyFilters();
    }

    private void RebuildFilterChips()
    {
        TableFilterChips.Clear();
        foreach (var table in _allAbilities.Select(ability => ability.Table).Distinct().OrderBy(table => table))
        {
            TableFilterChips.Add(new GeneralAbilityTableFilterChipVm(table, $"T{table}"));
        }

        SourceBookFilterChips.Clear();
        foreach (var sourceBook in _allAbilities
                     .Select(ability => NormalizeSourceBook(ability.SourceBook))
                     .Where(sourceBook => sourceBook.Length > 0)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(sourceBook => sourceBook, StringComparer.OrdinalIgnoreCase))
        {
            SourceBookFilterChips.Add(new GeneralAbilitySourceBookFilterChipVm(sourceBook));
        }

        SyncFilterChipSelection();
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
        var hasSourceBookFilters = _selectedSourceBooks.Count > 0;

        IEnumerable<EvolutionService.AbilityResult> filtered = _allAbilities;
        if (hasSourceBookFilters)
            filtered = filtered.Where(ability => _selectedSourceBooks.Contains(NormalizeSourceBook(ability.SourceBook)));

        if (hasTableFilters)
            filtered = filtered.Where(ability => _selectedTables.Contains(ability.Table));

        if (hasQuery)
        {
            filtered = filtered.Where(ability =>
                Matches(ability.Index, query)
                || Matches(NormalizeSourceBook(ability.SourceBook), query)
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

    private void SyncFilterChipSelection()
    {
        foreach (var chip in SourceBookFilterChips)
            chip.IsSelected = _selectedSourceBooks.Contains(chip.SourceBook);

        foreach (var chip in TableFilterChips)
            chip.IsSelected = _selectedTables.Contains(chip.Table);
    }

    private static string NormalizeSourceBook(string? sourceBook)
    {
        var value = (sourceBook ?? string.Empty).Trim();
        return value.Length == 0 ? "Unknown" : value;
    }

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

public sealed class GeneralAbilitySourceBookFilterChipVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public string SourceBook { get; }
    public string Label => SourceBook;

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

    public GeneralAbilitySourceBookFilterChipVm(string sourceBook)
    {
        SourceBook = string.IsNullOrWhiteSpace(sourceBook)
            ? "Unknown"
            : sourceBook.Trim();
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
