using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using labyItems.Helpers;
using labyItems.Models.Characters;
using labyItems.Models.ViewModels;
using labyItems.Pages.Characters;
using labyItems.Pages.Characters.ViewModels;
using labyItems.Services;

namespace labyItems.Pages.Search;

public partial class MultiRaceSearchPage : ContentPage, INotifyPropertyChanged
{
    private readonly Dictionary<string, MultiRaceDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ClassCardVm> _allMultiRaces = new();
    private readonly CollectionViewScrollPreserver _scrollPreserver;
    private bool _isLoading = true;
    private string _searchText = string.Empty;
    private string _selectedFilter = "All";
    private bool _loaded;
    private bool _isFilterModalOpen;

    public ObservableCollection<ClassCardVm> FilteredMultiRaces { get; } = new();
    public ObservableCollection<SearchFilterChipVm> FilterChips { get; } = new();

    public ICommand ToggleExpandedCommand { get; }
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

    public bool HasNoResults => !IsLoading && FilteredMultiRaces.Count == 0;

    public MultiRaceSearchPage()
    {
        InitializeComponent();
        BindingContext = this;

        ToggleExpandedCommand = new Command<ClassCardVm>(OnToggleExpanded);
        ToggleFilterChipCommand = new Command<SearchFilterChipVm>(OnToggleFilterChip);
        OpenFiltersCommand = new Command(() => IsFilterModalOpen = true);
        CancelFiltersCommand = new Command(() => IsFilterModalOpen = false);
        ApplyFiltersCommand = new Command(() => { IsFilterModalOpen = false; Refilter(); });

        FilterChips.Add(new SearchFilterChipVm("All", true));
        _scrollPreserver = new CollectionViewScrollPreserver(MultiRacesCollection, this);
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
        await LoadMultiRacesAsync();
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

    private void OnToggleExpanded(ClassCardVm? item)
    {
        if (item == null) return;
        _scrollPreserver.RecordItem(item);

        foreach (var c in _allMultiRaces)
        {
            if (!ReferenceEquals(c, item) && c.IsExpanded)
                c.IsExpanded = false;
        }

        item.IsExpanded = !item.IsExpanded;
    }

    private async Task LoadMultiRacesAsync()
    {
        IsLoading = true;
        try
        {
            _allMultiRaces.Clear();
            _definitions.Clear();
            var catalog = await MultiRaceService.GetCatalogAsync();
            var id = 1;
            var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in catalog.MultiRaces.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
            {
                var def = pair.Value;
                _definitions[pair.Key] = def;
                var displayName = !string.IsNullOrWhiteSpace(def.DisplayName) ? def.DisplayName.Trim() : pair.Key;
                var powerBase = MultiPathWizardHelpers.ResolvePowerBase<
                    MultiRaceDefinition,
                    MultiRaceAvailabilityOption,
                    MultiRaceLevelAbility,
                    MultiRaceSystemEffect>(def);

                var maxLevel = MultiPathWizardHelpers.ResolveMaxLevel<
                    MultiRaceDefinition,
                    MultiRaceAvailabilityOption,
                    MultiRaceLevelAbility,
                    MultiRaceSystemEffect>(def);

                var levelRows = new ObservableCollection<LevelAbilityRowVm>();
                for (var level = 1; level <= maxLevel; level++)
                {
                    var abilityNames = new List<string>();
                    var abilityKeys = new List<string>();
                    if (def.Levels.TryGetValue(level.ToString(), out var levelAbilities) && levelAbilities != null)
                    {
                        foreach (var a in levelAbilities)
                        {
                            var aname = (a.Type ?? string.Empty).Trim();
                            if (aname.Length > 0)
                            {
                                abilityNames.Add(aname);
                                abilityKeys.Add(aname);
                            }
                        }
                    }

                    levelRows.Add(new LevelAbilityRowVm
                    {
                        Level = level,
                        Body = string.Empty,
                        Loc = string.Empty,
                        AbilityNames = abilityNames,
                        AbilityDetailKeys = abilityKeys
                    });
                }

                var powerBaseBrackets = string.IsNullOrWhiteSpace(powerBase) ? Array.Empty<string>() : new[] { powerBase };
                var (icon, category, bracketTags) = ClassCardVm.ParseBrackets(powerBaseBrackets);
                if (category.Length > 0)
                    categories.Add(category);

                var card = new ClassCardVm
                {
                    Id = id++,
                    Key = pair.Key,
                    Name = displayName,
                    Category = category.Length > 0 ? category : (powerBase.Length > 0 ? powerBase : "Multi-Race"),
                    Icon = icon.Length > 0 ? icon : "🧬",
                    Summary = $"Multi-race up to level {maxLevel}",
                    PowerBase = powerBase,
                    LevelRows = levelRows,
                    BracketTags = bracketTags.Count > 0 ? bracketTags : (string.IsNullOrWhiteSpace(powerBase) ? Array.Empty<string>() : new[] { powerBase })
                };

                _allMultiRaces.Add(card);
            }

            FilterChips.Clear();
            FilterChips.Add(new SearchFilterChipVm("All", _selectedFilter == "All"));
            foreach (var c in categories.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                FilterChips.Add(new SearchFilterChipVm(c, c.Equals(_selectedFilter, StringComparison.OrdinalIgnoreCase)));
            }

            Refilter();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Multi-Races", $"Failed to load multi-races: {ex.Message}", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void Refilter()
    {
        var q = (SearchText ?? string.Empty).Trim().ToLowerInvariant();
        var filter = _selectedFilter;

        var matches = _allMultiRaces.Where(c =>
        {
            if (filter != "All")
            {
                var cat = c.Category ?? string.Empty;
                var tags = c.BracketTags ?? Array.Empty<string>();
                var power = c.PowerBase ?? string.Empty;
                if (!cat.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                    !tags.Any(t => t.Contains(filter, StringComparison.OrdinalIgnoreCase)) &&
                    !power.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                    !c.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            if (q.Length == 0) return true;

            var bracketString = c.BracketTags != null ? string.Join(" ", c.BracketTags) : string.Empty;

            return c.Name.ToLowerInvariant().Contains(q) ||
                   (c.Category?.ToLowerInvariant().Contains(q) ?? false) ||
                   (c.PowerBase?.ToLowerInvariant().Contains(q) ?? false) ||
                   bracketString.ToLowerInvariant().Contains(q) ||
                   (c.Summary?.ToLowerInvariant().Contains(q) ?? false);
        });

        FilteredMultiRaces.Clear();
        foreach (var m in matches)
            FilteredMultiRaces.Add(m);

        OnPropertyChanged(nameof(HasNoResults));
    }

    private async void OnInfoClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: ClassCardVm item } || !_definitions.TryGetValue(item.Key, out var definition)) return;
        _scrollPreserver.RecordItem(item);
        await Navigation.PushAsync(new Pages.MultiRaceCard.MultiRaceCard(definition, item.Key));
    }
}
