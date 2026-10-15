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

public partial class MultiClassSearchPage : ContentPage, INotifyPropertyChanged
{
    private readonly Dictionary<string, MultiClassDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ClassCardVm> _allMultiClasses = new();
    private readonly CollectionViewScrollPreserver _scrollPreserver;
    private bool _isLoading = true;
    private string _searchText = string.Empty;
    private readonly HashSet<string> _selectedFilterKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _pendingFilterKeys = new(StringComparer.OrdinalIgnoreCase);
    private bool _loaded;
    private bool _isFilterModalOpen;

    public ObservableCollection<ClassCardVm> FilteredMultiClasses { get; } = new();
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

    public bool HasNoResults => !IsLoading && FilteredMultiClasses.Count == 0;

    public MultiClassSearchPage()
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

        _scrollPreserver = new CollectionViewScrollPreserver(MultiClassesCollection, this);
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
        await LoadMultiClassesAsync();
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

        foreach (var c in _allMultiClasses)
        {
            if (!ReferenceEquals(c, item) && c.IsExpanded)
                c.IsExpanded = false;
        }

        item.IsExpanded = !item.IsExpanded;
    }

    private async Task LoadMultiClassesAsync()
    {
        IsLoading = true;
        try
        {
            _allMultiClasses.Clear();
            _definitions.Clear();
            var catalog = await MultiClassService.GetCatalogAsync();
            var id = 1;

            foreach (var pair in catalog.MultiClasses.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
            {
                var def = pair.Value;
                _definitions[pair.Key] = def;
                var displayName = !string.IsNullOrWhiteSpace(def.DisplayName) ? def.DisplayName.Trim() : pair.Key;
                var powerBase = MultiPathWizardHelpers.ResolvePowerBase<
                    MultiClassDefinition,
                    MultiClassAvailabilityOption,
                    MultiClassLevelAbility,
                    MultiClassSystemEffect>(def);

                var maxLevel = MultiPathWizardHelpers.ResolveMaxLevel<
                    MultiClassDefinition,
                    MultiClassAvailabilityOption,
                    MultiClassLevelAbility,
                    MultiClassSystemEffect>(def);

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

                var card = new ClassCardVm
                {
                    Id = id++,
                    Key = pair.Key,
                    Name = displayName,
                    Category = category.Length > 0 ? category : powerBase,
                    Icon = icon,
                    IconGlyph = string.IsNullOrWhiteSpace(def.IconGlyph) ? FontAwesomeGlyphs.Shield : def.IconGlyph,
                    Summary = $"Multi-class up to level {maxLevel}",
                    PowerBase = powerBase,
                    LevelRows = levelRows,
                    BracketTags = bracketTags.Count > 0 ? bracketTags : (string.IsNullOrWhiteSpace(powerBase) ? Array.Empty<string>() : new[] { powerBase })
                };

                _allMultiClasses.Add(card);
            }

            FilterSections.Clear();
            foreach (var section in MultiPathSearchFilters.Build(catalog.MultiClasses.Values.SelectMany(x => x.AvailabilityOptions ?? []).SelectMany(x => x.Rules ?? []))) FilterSections.Add(section);

            Refilter();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Multi-Classes", $"Failed to load multi-classes: {ex.Message}", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void Refilter()
    {
        var q = (SearchText ?? string.Empty).Trim().ToLowerInvariant();
        var matches = _allMultiClasses.Where(c =>
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

        FilteredMultiClasses.Clear();
        foreach (var m in matches)
            FilteredMultiClasses.Add(m);

        OnPropertyChanged(nameof(HasNoResults));
    }

    private async void OnInfoClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: ClassCardVm item } || !_definitions.TryGetValue(item.Key, out var definition)) return;
        _scrollPreserver.RecordItem(item);
        await Navigation.PushAsync(new Pages.MultiClassCard.MultiClassCard(definition, item.Key));
    }
}
