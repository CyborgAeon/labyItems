using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using labyItems.Pages.Characters;
using labyItems.Services;

namespace labyItems.Pages.NonStandard;

public partial class NonStandardClassSearchPage : ContentPage
{
    private readonly TaskCompletionSource<string?> _completion = new();
    private readonly NonStandardClassSearchVm _vm;

    public ICommand SelectClassCommand { get; }
    public ICommand ToggleExpandedCommand { get; }
    public ICommand ToggleFilterChipCommand { get; }

    private NonStandardClassSearchPage(string? currentValue)
    {
        InitializeComponent();

        _vm = new NonStandardClassSearchVm(currentValue);
        BindingContext = _vm;

        SelectClassCommand = new Command<ClassCardVm>(OnSelectClass);
        ToggleExpandedCommand = new Command<ClassCardVm>(item => _ = OnToggleExpandedAsync(item));
        ToggleFilterChipCommand = new Command<NonStandardClassFilterChipVm>(_vm.ToggleFilterChip);
    }

    public static async Task<string?> PickAsync(INavigation navigation, string? currentValue)
    {
        if (navigation == null)
            return null;

        var page = new NonStandardClassSearchPage(currentValue);
        await page._vm.LoadAsync();
        await navigation.PushAsync(page);
        return await page._completion.Task;
    }

    protected override bool OnBackButtonPressed()
    {
        _completion.TrySetResult(null);
        return base.OnBackButtonPressed();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _completion.TrySetResult(null);
    }

    private async void OnSelectClass(ClassCardVm? card)
    {
        if (card == null)
            return;

        _vm.SetSelected(card.Key);
        _completion.TrySetResult(card.Key);

        if (Navigation.NavigationStack.LastOrDefault() == this)
            await Navigation.PopAsync();
    }

    private async Task OnToggleExpandedAsync(ClassCardVm? card)
    {
        if (card == null)
            return;

        if (!card.IsExpanded)
            await card.EnsureProgressionLoadedAsync();

        foreach (var item in _vm.FilteredClasses)
        {
            if (ReferenceEquals(item, card))
                continue;

            item.IsExpanded = false;
        }

        card.IsExpanded = !card.IsExpanded;
    }
}

internal sealed class NonStandardClassSearchVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly ObservableCollection<ClassCardVm> _allClasses = new();
    private readonly string _initialSelection;
    private string _searchText = string.Empty;

    public ObservableCollection<ClassCardVm> FilteredClasses { get; } = new();
    public ObservableCollection<NonStandardClassFilterChipVm> FilterChips { get; } = new();

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!Set(ref _searchText, value ?? string.Empty))
                return;

            Refilter();
        }
    }

    public NonStandardClassSearchVm(string? currentSelection)
    {
        _initialSelection = (currentSelection ?? string.Empty).Trim();
    }

    public async Task LoadAsync()
    {
        _allClasses.Clear();

        var classes = await ClassService.GetAllAsync();
        foreach (var entry in classes.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            _allClasses.Add(BuildClassCard(entry.Key, entry.Value));

        BuildFilterChips();
        Refilter();
        SetSelected(_initialSelection);
    }

    public void SetSelected(string? classKey)
    {
        var key = (classKey ?? string.Empty).Trim();
        foreach (var card in _allClasses)
            card.IsSelected = card.Key.Equals(key, StringComparison.OrdinalIgnoreCase);
    }

    public void ToggleFilterChip(NonStandardClassFilterChipVm? chip)
    {
        if (chip == null)
            return;

        if (chip.IsAll)
        {
            foreach (var item in FilterChips)
                item.IsSelected = item.IsAll;
            Refilter();
            return;
        }

        chip.IsSelected = !chip.IsSelected;
        var allChip = FilterChips.FirstOrDefault(item => item.IsAll);
        if (allChip != null)
            allChip.IsSelected = false;

        if (!FilterChips.Any(item => !item.IsAll && item.IsSelected) && allChip != null)
            allChip.IsSelected = true;

        Refilter();
    }

    private void BuildFilterChips()
    {
        FilterChips.Clear();
        FilterChips.Add(new NonStandardClassFilterChipVm("All", "All", true));

        var bracketLabels = _allClasses
            .SelectMany(card => card.BracketLabels)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(label => label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var label in bracketLabels)
            FilterChips.Add(new NonStandardClassFilterChipVm(label, label, false));
    }

    private void Refilter()
    {
        var query = (_searchText ?? string.Empty).Trim();
        var selectedFilters = FilterChips
            .Where(item => item.IsSelected && !item.IsAll)
            .Select(item => item.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var allSelected = selectedFilters.Count == 0
            || FilterChips.FirstOrDefault(item => item.IsAll)?.IsSelected == true;

        var filtered = _allClasses.Where(card =>
        {
            var matchesQuery = query.Length == 0
                || card.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || card.Category.Contains(query, StringComparison.OrdinalIgnoreCase)
                || card.BracketSubheading.Contains(query, StringComparison.OrdinalIgnoreCase);

            if (!matchesQuery)
                return false;

            if (allSelected)
                return true;

            return card.BracketLabels.Any(label => selectedFilters.Contains(label));
        });

        var selectedKey = _allClasses.FirstOrDefault(card => card.IsSelected)?.Key ?? string.Empty;

        FilteredClasses.Clear();
        foreach (var card in filtered.OrderBy(card => card.Name, StringComparer.OrdinalIgnoreCase))
            FilteredClasses.Add(card);

        if (selectedKey.Length > 0)
            SetSelected(selectedKey);
    }

    private static ClassCardVm BuildClassCard(string key, CharacterClassRecord record)
    {
        var (icon, category, bracketTags) = ClassCardVm.ParseBrackets(record.Brackets);

        var maxAc = record.MaxAC.ValueKind switch
        {
            JsonValueKind.Number => record.MaxAC.GetInt32(),
            JsonValueKind.String when int.TryParse(record.MaxAC.GetString(), out var parsed) => parsed,
            _ => 0
        };

        var tblp = record.PowerPerLevel.ValueKind switch
        {
            JsonValueKind.Number => record.PowerPerLevel.GetInt32(),
            JsonValueKind.String when int.TryParse(record.PowerPerLevel.GetString(), out var parsed) => parsed,
            _ => 0
        };

        return new ClassCardVm
        {
            Key = key,
            Name = key,
            Category = category,
            Icon = icon,
            Summary = ClassCardVm.BuildSummaryFromLevels(record.Levels),
            MaxAc = maxAc,
            TBLP = tblp,
            PowerBase = ClassCardVm.ExtractPowerBase(record),
            BracketTags = record.Brackets ?? bracketTags
        };
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

public sealed class NonStandardClassFilterChipVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _isSelected;

    public string Key { get; }
    public string Label { get; }

    public bool IsAll => Key.Equals("All", StringComparison.OrdinalIgnoreCase);

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;

            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public NonStandardClassFilterChipVm(string key, string label, bool isSelected)
    {
        Key = key ?? string.Empty;
        Label = label ?? string.Empty;
        _isSelected = isSelected;
    }
}
