using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using labyItems.Helpers;
using labyItems.Models.Characters;
using labyItems.Models.ViewModels;
using labyItems.Services;
using Microsoft.Maui.Graphics;
using ClassCardPage = labyItems.Pages.ClassCard.ClassCard;

namespace labyItems.Pages.Search;

public partial class ClassSearchPage : ContentPage, INotifyPropertyChanged
{
    private readonly List<ClassSearchResultVm> _allClasses = new();
    private readonly CollectionViewScrollPreserver _scrollPreserver;
    private bool _isLoading = true;
    private string _searchText = string.Empty;
    private HashSet<string> _selectedFilters = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _pendingFilters = new(StringComparer.OrdinalIgnoreCase);
    private bool _isFilterModalOpen;
    private bool _loaded;

    public ObservableCollection<ClassSearchResultVm> FilteredClasses { get; } = new();
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

    public bool HasNoResults => !IsLoading && FilteredClasses.Count == 0;

    public ClassSearchPage()
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

        CategoryChips.Add(new SearchFilterChipVm("Classes", true));
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
        var options = _allClasses
            .SelectMany(item => item.Record.Brackets ?? [])
            .Where(bracket => !string.IsNullOrWhiteSpace(bracket))
            .Select(bracket => bracket.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(bracket => bracket, StringComparer.OrdinalIgnoreCase);
        foreach (var option in options)
            FilterChips.Add(new SearchFilterChipVm(option, _pendingFilters.Contains(option)));
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

    private async Task LoadClassesAsync()
    {
        IsLoading = true;
        try
        {
            _allClasses.Clear();
            var dict = await ClassService.GetAllAsync();
            if (dict.Count == 0)
            {
                ClassService.InvalidateCache();
                dict = await ClassService.GetAllAsync();
            }
            foreach (var kvp in dict.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
            {
                var key = kvp.Key;
                var rec = kvp.Value;
                if (rec == null) continue;

                _allClasses.Add(new ClassSearchResultVm(key, rec));
            }

            BuildFilterChips();
            Refilter();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Classes", $"Failed to load classes: {ex.Message}", "OK");
        }
        finally
        {
            IsLoading = false;
            if (_allClasses.Count == 0)
                _loaded = false;
        }
    }

    private void Refilter()
    {
        var q = (SearchText ?? string.Empty).Trim().ToLowerInvariant();
        var filters = _selectedFilters;

        var matches = _allClasses.Where(c =>
        {
            if (filters.Count > 0)
            {
                var tags = c.Record.Brackets ?? [];
                if (!tags.Any(tag => filters.Contains(tag)))
                    return false;
            }

            if (q.Length == 0) return true;

            var bracketString = string.Join(" ", c.Record.Brackets);

            return c.Name.ToLowerInvariant().Contains(q) ||
                   c.BracketText.ToLowerInvariant().Contains(q) ||
                   bracketString.ToLowerInvariant().Contains(q) ||
                   c.Overview.ToLowerInvariant().Contains(q);
        });

        FilteredClasses.Clear();
        foreach (var m in matches.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
            FilteredClasses.Add(m);

        OnPropertyChanged(nameof(HasNoResults));
    }

    private async void OnClassInfoClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: ClassSearchResultVm item }) return;
        _scrollPreserver.RecordItem(item);
        await Navigation.PushAsync(new ClassCardPage(item.Name, item.Record));
    }
}

public sealed class ClassSearchResultVm
{
    public ClassSearchResultVm(string name, CharacterClassRecord record) { Name = name; Record = record; }
    public string Name { get; }
    public CharacterClassRecord Record { get; }
    public string BracketText => ClassBracketIconHelper.BuildBracketSubheading(Record.Brackets, "Unbracketed");
    public string Overview => string.Join(" · ", new[] { $"Max AC {Record.MaxAC}", string.Join(", ", Record.Armour?.Wearable ?? []), string.Join(", ", Record.BuyAs ?? []) }.Where(x => !string.IsNullOrWhiteSpace(x)));
    public bool HasSplitIcon => Record.Brackets.Count > 1;
    public string SingleGlyph => ClassBracketIconHelper.GetBracketGlyph(Record.Brackets.FirstOrDefault());
    public string SplitLeftGlyph => ClassBracketIconHelper.GetBracketGlyph(Record.Brackets.ElementAtOrDefault(0));
    public string SplitRightGlyph => ClassBracketIconHelper.GetBracketGlyph(Record.Brackets.ElementAtOrDefault(1));
    public Color SingleBackground => ClassBracketIconHelper.GetBracketColor(Record.Brackets.FirstOrDefault());
    public Color SplitLeftBackground => ClassBracketIconHelper.GetBracketColor(Record.Brackets.ElementAtOrDefault(0));
    public Color SplitRightBackground => ClassBracketIconHelper.GetBracketColor(Record.Brackets.ElementAtOrDefault(1));
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
