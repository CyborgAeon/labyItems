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
    private readonly HashSet<string> _selectedFilterKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _pendingFilterKeys = new(StringComparer.OrdinalIgnoreCase);
    private bool _loaded;
    private bool _isFilterModalOpen;

    public ObservableCollection<ClassCardVm> FilteredMultiRaces { get; } = new();
    public ObservableCollection<MultiPathFilterSectionVm> FilterSections { get; } = new();

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
        ToggleFilterChipCommand = new Command<MultiPathFilterChipVm>(OnToggleFilterChip);
        OpenFiltersCommand = new Command(OpenFilters);
        CancelFiltersCommand = new Command(CancelFilters);
        ApplyFiltersCommand = new Command(ApplyFilters);
        OnPropertyChanged(nameof(OpenFiltersCommand));
        OnPropertyChanged(nameof(CancelFiltersCommand));
        OnPropertyChanged(nameof(ApplyFiltersCommand));

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

    private void OnToggleFilterChip(MultiPathFilterChipVm? chip)
    {
        if (chip == null) return;
        chip.IsSelected = !chip.IsSelected;
        if (chip.IsSelected) _pendingFilterKeys.Add(chip.Key); else _pendingFilterKeys.Remove(chip.Key);
    }

    private void OpenFilters() { _pendingFilterKeys.Clear(); _pendingFilterKeys.UnionWith(_selectedFilterKeys); SyncFilterChips(_pendingFilterKeys); IsFilterModalOpen = true; }
    private void CancelFilters() { SyncFilterChips(_selectedFilterKeys); IsFilterModalOpen = false; }
    private void ApplyFilters() { _selectedFilterKeys.Clear(); _selectedFilterKeys.UnionWith(_pendingFilterKeys); IsFilterModalOpen = false; Refilter(); }
    private void SyncFilterChips(IReadOnlySet<string> keys) { foreach (var chip in FilterSections.SelectMany(x => x.Chips)) chip.IsSelected = keys.Contains(chip.Key); }

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
                    IconGlyph = string.IsNullOrWhiteSpace(def.IconGlyph) ? FontAwesomeGlyphs.Shield : def.IconGlyph,
                    Summary = $"Multi-race up to level {maxLevel}",
                    PowerBase = powerBase,
                    LevelRows = levelRows,
                    BracketTags = bracketTags.Count > 0 ? bracketTags : (string.IsNullOrWhiteSpace(powerBase) ? Array.Empty<string>() : new[] { powerBase })
                };

                _allMultiRaces.Add(card);
            }

            FilterSections.Clear();
            foreach (var section in MultiPathSearchFilters.Build(catalog.MultiRaces.Values.SelectMany(x => x.AvailabilityOptions ?? []).SelectMany(x => x.Rules ?? []))) FilterSections.Add(section);

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
        var matches = _allMultiRaces.Where(c =>
        {
            if (_selectedFilterKeys.Count > 0 && (!_definitions.TryGetValue(c.Key, out var definition) || !(definition.AvailabilityOptions ?? []).Any(x => MultiPathSearchFilters.Matches(x.Rules, _selectedFilterKeys)))) return false;

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
