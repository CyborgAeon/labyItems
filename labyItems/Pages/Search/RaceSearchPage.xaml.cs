using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using labyItems.Helpers;
using labyItems.Models.Characters;
using labyItems.Pages.Characters;
using labyItems.Services;
using RaceCardPage = labyItems.Pages.RaceCard.RaceCard;

namespace labyItems.Pages.Search;

public partial class RaceSearchPage : ContentPage, INotifyPropertyChanged
{
    private readonly List<RaceCardVm> _allRaces = new();
    private readonly Dictionary<string, PeopleRecord> _recordsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly CollectionViewScrollPreserver _scrollPreserver;
    private bool _isLoading = true;
    private string _searchText = string.Empty;
    private HashSet<string> _selectedFilters = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _pendingFilters = new(StringComparer.OrdinalIgnoreCase);
    private bool _isFilterModalOpen;
    private bool _loaded;

    public ObservableCollection<RaceCardVm> FilteredRaces { get; } = new();
    public ObservableCollection<SearchFilterChipVm> CategoryChips { get; } = new();
    public ObservableCollection<SearchFilterChipVm> FilterChips { get; } = new();

    public ICommand ToggleFilterChipCommand { get; }
    public ICommand OpenFiltersCommand { get; }
    public ICommand CancelFiltersCommand { get; }
    public ICommand ApplyFiltersCommand { get; }
    public bool IsFilterModalOpen { get => _isFilterModalOpen; private set { if (_isFilterModalOpen == value) return; _isFilterModalOpen = value; OnPropertyChanged(); } }

    public bool IsLoading
    {
        get => _isLoading;
        private set { _isLoading = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasNoResults)); }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value ?? string.Empty;
            OnPropertyChanged();
            Refilter();
        }
    }

    public bool HasNoResults => !IsLoading && FilteredRaces.Count == 0;

    public RaceSearchPage()
    {
        ToggleFilterChipCommand = new Command<SearchFilterChipVm>(OnToggleFilterChip);
        OpenFiltersCommand = new Command(OpenFilters);
        CancelFiltersCommand = new Command(() => IsFilterModalOpen = false);
        ApplyFiltersCommand = new Command(() =>
        {
            _selectedFilters = new HashSet<string>(_pendingFilters, StringComparer.OrdinalIgnoreCase);
            IsFilterModalOpen = false;
            Refilter();
        });

        InitializeComponent();
        BindingContext = this;

        CategoryChips.Add(new SearchFilterChipVm("Races", true));
        _scrollPreserver = new CollectionViewScrollPreserver(RacesCollection, this);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded)
        {
            _scrollPreserver.RestoreScrollPosition();
            return;
        }

        _loaded = true;
        await LoadRacesAsync();
        _scrollPreserver.RestoreScrollPosition();
    }

    private void OnToggleFilterChip(SearchFilterChipVm? chip)
    {
        if (chip == null) return;
        if (!_pendingFilters.Add(chip.Label))
            _pendingFilters.Remove(chip.Label);
        chip.IsSelected = _pendingFilters.Contains(chip.Label);
    }

    private void OpenFilters()
    {
        _pendingFilters = new HashSet<string>(_selectedFilters, StringComparer.OrdinalIgnoreCase);
        BuildFilterChips();
        IsFilterModalOpen = true;
    }

    private void BuildFilterChips()
    {
        FilterChips.Clear();
        var options = _allRaces
            .SelectMany(item => item.PeopleTypes ?? [])
            .Where(type => !string.IsNullOrWhiteSpace(type))
            .Select(type => type.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(type => type, StringComparer.OrdinalIgnoreCase);
        foreach (var option in options)
            FilterChips.Add(new SearchFilterChipVm(option, _pendingFilters.Contains(option)));
    }

    private async Task LoadRacesAsync()
    {
        IsLoading = true;
        try
        {
            _allRaces.Clear();
            _recordsByName.Clear();
            var all = await PeopleService.GetAllAsync();
            var ordered = all.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase).ToList();
            for (var idx = 0; idx < ordered.Count; idx++)
            {
                var name = ordered[idx].Key;
                var record = ordered[idx].Value;
                if (record == null) continue;
                _recordsByName[name] = record;

                var peopleTypes = PeopleTypeFormatting.NormalizePeopleTypes(record.PeopleType);
                var displayPeopleType = PeopleTypeFormatting.FormatPeopleTypes(peopleTypes);
                var primaryPeopleType = PeopleTypeFormatting.SelectPrimaryPeopleType(peopleTypes);

                var vm = new RaceCardVm
                {
                    Id = idx + 1,
                    Name = name,
                    PeopleTypes = peopleTypes,
                    PeopleType = displayPeopleType,
                    IsNonStandard = record.NonStandard,
                    Description = record.Description ?? string.Empty,
                    BuyAsRaw = record.BuyAs ?? string.Empty,
                    Icon = PeopleTypeFormatting.IconForPeopleType(primaryPeopleType),
                    IsSelected = false
                };

                vm.SearchText = BuildRaceSearchText(name, record);
                vm.BuildRowsAndChips(record.LevelledAbilities ?? new Dictionary<string, List<AbilityDefinition>>(), record.BuyAs);
                _allRaces.Add(vm);
            }

            BuildFilterChips();
            Refilter();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Races", $"Failed to load races: {ex.Message}", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static string BuildRaceSearchText(string name, PeopleRecord record)
    {
        var parts = new List<string> { name, record.Description ?? string.Empty };
        if (record.PeopleType != null)
            parts.AddRange(record.PeopleType);
        if (record.LevelledAbilities != null)
        {
            foreach (var list in record.LevelledAbilities.Values)
            {
                if (list != null)
                    parts.AddRange(list.Select(a => a.Name ?? string.Empty));
            }
        }
        return string.Join(" ", parts.Where(p => p.Length > 0)).ToLowerInvariant();
    }

    private void Refilter()
    {
        var q = (SearchText ?? string.Empty).Trim().ToLowerInvariant();
        var filters = _selectedFilters;

        var matches = _allRaces.Where(r =>
        {
            if (filters.Count > 0)
            {
                if (r.PeopleTypes == null || !r.PeopleTypes.Any(filters.Contains))
                    return false;
            }

            if (q.Length == 0) return true;

            return r.Name.ToLowerInvariant().Contains(q) ||
                   r.PeopleType.ToLowerInvariant().Contains(q) ||
                   (r.SearchText?.ToLowerInvariant().Contains(q) ?? false) ||
                   (r.Description?.ToLowerInvariant().Contains(q) ?? false);
        });

        FilteredRaces.Clear();
        foreach (var m in matches.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
            FilteredRaces.Add(m);

        OnPropertyChanged(nameof(HasNoResults));
    }

    private async void OnRaceInfoClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: RaceCardVm item }
            || !_recordsByName.TryGetValue(item.Name, out var record))
            return;

        _scrollPreserver.RecordItem(item);
        await Navigation.PushAsync(new RaceCardPage(item.Name, record));
    }
}
