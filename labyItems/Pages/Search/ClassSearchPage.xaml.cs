using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using labyItems.Helpers;
using labyItems.Models.Characters;
using labyItems.Models.ViewModels;
using labyItems.Pages.Characters;
using labyItems.Pages.Characters.ViewModels;
using labyItems.Services;

namespace labyItems.Pages.Search;

public partial class ClassSearchPage : ContentPage, INotifyPropertyChanged
{
    private readonly List<ClassCardVm> _allClasses = new();
    private readonly CollectionViewScrollPreserver _scrollPreserver;
    private bool _isLoading = true;
    private string _searchText = string.Empty;
    private string _selectedFilter = "All";
    private bool _loaded;

    public ObservableCollection<ClassCardVm> FilteredClasses { get; } = new();
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

    public bool HasNoResults => !IsLoading && FilteredClasses.Count == 0;

    public ClassSearchPage()
    {
        InitializeComponent();
        BindingContext = this;

        ToggleExpandedCommand = new Command<ClassCardVm>(OnToggleExpanded);
        ToggleFilterChipCommand = new Command<SearchFilterChipVm>(OnToggleFilterChip);

        BuildFilterChips();
        _scrollPreserver = new CollectionViewScrollPreserver(ClassesCollection, this);
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
        await LoadClassesAsync();
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

        foreach (var c in _allClasses)
        {
            if (!ReferenceEquals(c, item) && c.IsExpanded)
                c.IsExpanded = false;
        }

        item.IsExpanded = !item.IsExpanded;
    }

    private async Task LoadClassesAsync()
    {
        IsLoading = true;
        try
        {
            _allClasses.Clear();
            var dict = await ClassService.GetAllAsync();
            var id = 1;

            foreach (var kvp in dict)
            {
                var key = kvp.Key;
                var rec = kvp.Value;
                if (rec == null) continue;

                var (icon, category, bracketTags) = ClassCardVm.ParseBrackets(rec.Brackets);

                var maxAc = rec.MaxAC.ValueKind switch
                {
                    JsonValueKind.Number => rec.MaxAC.GetInt32(),
                    JsonValueKind.String => int.TryParse(rec.MaxAC.GetString(), out var n) ? n : 0,
                    _ => 0
                };

                var tblp = rec.PowerPerLevel.ValueKind switch
                {
                    JsonValueKind.Number => rec.PowerPerLevel.GetInt32(),
                    JsonValueKind.String => int.TryParse(rec.PowerPerLevel.GetString(), out var n) ? n : 0,
                    _ => 0
                };

                var levelRows = new ObservableCollection<LevelAbilityRowVm>(
                    Enumerable.Range(1, 8).Select(lvl =>
                    {
                        rec.Levels.TryGetValue(lvl.ToString(), out var arr);
                        arr ??= new List<AbilityDefinition>();
                        return LevelAbilityRowBuilder.Build(lvl, arr);
                    })
                );

                var vm = new ClassCardVm
                {
                    Id = id++,
                    Key = key,
                    Name = key,
                    Category = category,
                    Icon = icon,
                    Summary = ClassCardVm.BuildSummaryFromLevels(rec.Levels),
                    MaxAc = maxAc,
                    TBLP = tblp,
                    PowerBase = ClassCardVm.ExtractPowerBase(rec),
                    LevelRows = levelRows,
                    BracketTags = rec.Brackets ?? bracketTags
                };

                _allClasses.Add(vm);
            }

            Refilter();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Classes", $"Failed to load classes: {ex.Message}", "OK");
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

        var matches = _allClasses.Where(c =>
        {
            if (filter != "All")
            {
                var cat = c.Category ?? string.Empty;
                var tags = c.BracketTags ?? Array.Empty<string>();
                if (!cat.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                    !tags.Any(t => t.Contains(filter, StringComparison.OrdinalIgnoreCase)) &&
                    !c.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            if (q.Length == 0) return true;

            var bracketString = c.BracketTags != null ? string.Join(" ", c.BracketTags) : string.Empty;

            return c.Name.ToLowerInvariant().Contains(q) ||
                   (c.Category?.ToLowerInvariant().Contains(q) ?? false) ||
                   bracketString.ToLowerInvariant().Contains(q) ||
                   (c.Summary?.ToLowerInvariant().Contains(q) ?? false);
        });

        FilteredClasses.Clear();
        foreach (var m in matches)
            FilteredClasses.Add(m);

        OnPropertyChanged(nameof(HasNoResults));
    }
}

public sealed class SearchFilterChipVm : INotifyPropertyChanged
{
    private bool _isSelected;
    public string Label { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public SearchFilterChipVm(string label, bool isSelected = false)
    {
        Label = label;
        _isSelected = isSelected;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
