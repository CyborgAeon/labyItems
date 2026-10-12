using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using labyItems.Helpers;
using labyItems.Models.Characters;
using labyItems.Pages.Characters;
using labyItems.Services;

namespace labyItems.Pages.Search;

public partial class RaceSearchPage : ContentPage, INotifyPropertyChanged
{
    private readonly List<RaceCardVm> _allRaces = new();
    private readonly CollectionViewScrollPreserver _scrollPreserver;
    private bool _isLoading = true;
    private string _searchText = string.Empty;
    private string _selectedFilter = "All";
    private bool _loaded;

    public ObservableCollection<RaceCardVm> FilteredRaces { get; } = new();
    public ObservableCollection<SearchFilterChipVm> FilterChips { get; } = new();

    public ICommand ToggleRaceExpandedCommand { get; }
    public ICommand ToggleFilterChipCommand { get; }

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
        InitializeComponent();
        BindingContext = this;

        ToggleRaceExpandedCommand = new Command<RaceCardVm>(OnToggleExpanded);
        ToggleFilterChipCommand = new Command<SearchFilterChipVm>(OnToggleFilterChip);

        FilterChips.Add(new SearchFilterChipVm("All", true));
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
        _selectedFilter = chip.Label;
        foreach (var c in FilterChips)
            c.IsSelected = c.Label.Equals(_selectedFilter, StringComparison.OrdinalIgnoreCase);

        Refilter();
    }

    private void OnToggleExpanded(RaceCardVm? item)
    {
        if (item == null) return;
        _scrollPreserver.RecordItem(item);

        foreach (var r in _allRaces)
        {
            if (!ReferenceEquals(r, item) && r.IsExpanded)
                r.IsExpanded = false;
        }

        item.IsExpanded = !item.IsExpanded;
    }

    private async Task LoadRacesAsync()
    {
        IsLoading = true;
        try
        {
            _allRaces.Clear();
            var all = await PeopleService.GetAllAsync();
            var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var ordered = all.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase).ToList();
            for (var idx = 0; idx < ordered.Count; idx++)
            {
                var name = ordered[idx].Key;
                var record = ordered[idx].Value;
                if (record == null) continue;

                var peopleTypes = PeopleTypeFormatting.NormalizePeopleTypes(record.PeopleType);
                types.UnionWith(peopleTypes);

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

            FilterChips.Clear();
            FilterChips.Add(new SearchFilterChipVm("All", _selectedFilter == "All"));
            foreach (var t in types.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                FilterChips.Add(new SearchFilterChipVm(t, t.Equals(_selectedFilter, StringComparison.OrdinalIgnoreCase)));
            }

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
        var filter = _selectedFilter;

        var matches = _allRaces.Where(r =>
        {
            if (filter != "All")
            {
                if (r.PeopleTypes == null || !r.PeopleTypes.Any(t => t.Equals(filter, StringComparison.OrdinalIgnoreCase)))
                {
                    if (!r.PeopleType.Contains(filter, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }

            if (q.Length == 0) return true;

            return r.Name.ToLowerInvariant().Contains(q) ||
                   r.PeopleType.ToLowerInvariant().Contains(q) ||
                   (r.SearchText?.ToLowerInvariant().Contains(q) ?? false) ||
                   (r.Description?.ToLowerInvariant().Contains(q) ?? false);
        });

        FilteredRaces.Clear();
        foreach (var m in matches)
            FilteredRaces.Add(m);

        OnPropertyChanged(nameof(HasNoResults));
    }
}
