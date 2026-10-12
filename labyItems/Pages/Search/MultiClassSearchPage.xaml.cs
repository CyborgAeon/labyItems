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
    private readonly List<ClassCardVm> _allMultiClasses = new();
    private readonly CollectionViewScrollPreserver _scrollPreserver;
    private bool _isLoading = true;
    private string _searchText = string.Empty;
    private string _selectedFilter = "All";
    private bool _loaded;

    public ObservableCollection<ClassCardVm> FilteredMultiClasses { get; } = new();
    public ObservableCollection<SearchFilterChipVm> FilterChips { get; } = new();

    public ICommand ToggleExpandedCommand { get; }
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

    public bool HasNoResults => !IsLoading && FilteredMultiClasses.Count == 0;

    public MultiClassSearchPage()
    {
        InitializeComponent();
        BindingContext = this;

        ToggleExpandedCommand = new Command<ClassCardVm>(OnToggleExpanded);
        ToggleFilterChipCommand = new Command<SearchFilterChipVm>(OnToggleFilterChip);

        BuildFilterChips();
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

    private void BuildFilterChips()
    {
        FilterChips.Clear();
        var options = new[] { "All", "Warrior", "Priest", "Wizard", "Scout", "Druid", "Neuro" };
        foreach (var opt in options)
        {
            FilterChips.Add(new SearchFilterChipVm(opt, opt.Equals(_selectedFilter, StringComparison.OrdinalIgnoreCase)));
        }
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
            var catalog = await MultiClassService.GetCatalogAsync();
            var id = 1;

            foreach (var pair in catalog.MultiClasses.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
            {
                var def = pair.Value;
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
                    Summary = $"Multi-class up to level {maxLevel}",
                    PowerBase = powerBase,
                    LevelRows = levelRows,
                    BracketTags = bracketTags.Count > 0 ? bracketTags : (string.IsNullOrWhiteSpace(powerBase) ? Array.Empty<string>() : new[] { powerBase })
                };

                _allMultiClasses.Add(card);
            }

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
        var filter = _selectedFilter;

        var matches = _allMultiClasses.Where(c =>
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

        FilteredMultiClasses.Clear();
        foreach (var m in matches)
            FilteredMultiClasses.Add(m);

        OnPropertyChanged(nameof(HasNoResults));
    }
}
