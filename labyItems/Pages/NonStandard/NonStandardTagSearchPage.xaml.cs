using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace labyItems.Pages.NonStandard;

public sealed record NonStandardTagSearchOption(string Title, string Value);

public partial class NonStandardTagSearchPage : ContentPage
{
    private readonly TaskCompletionSource<IReadOnlyList<string>> _completion = new();
    private readonly int _maxSelectionCount;
    private readonly bool _presentedModally;
    private readonly HashSet<string> _selected = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<NonStandardTagSearchOption> _allOptions;

    public ObservableCollection<NonStandardTagSearchOptionVm> FilteredOptions { get; } = new();
    public ObservableCollection<NonStandardTagSearchOptionVm> SelectedTags { get; } = new();

    public bool HasSelection => SelectedTags.Count > 0;
    public string SelectionHint => _maxSelectionCount > 0
        ? $"Select up to {_maxSelectionCount} tags."
        : "Select tags.";

    private NonStandardTagSearchPage(
        string title,
        IReadOnlyList<NonStandardTagSearchOption> options,
        IReadOnlyCollection<string>? initialSelected,
        int maxSelectionCount,
        bool presentedModally)
    {
        InitializeComponent();
        BindingContext = this;

        Title = string.IsNullOrWhiteSpace(title) ? "Select tags" : title;
        _allOptions = options.ToList();
        _maxSelectionCount = Math.Max(0, maxSelectionCount);
        _presentedModally = presentedModally;

        foreach (var value in initialSelected ?? Array.Empty<string>())
        {
            var normalized = (value ?? string.Empty).Trim();
            if (normalized.Length > 0)
                _selected.Add(normalized);
        }

        ApplyFilter(string.Empty);
        RefreshSelectedTags();
    }

    public static async Task<IReadOnlyList<string>> PickAsync(
        INavigation navigation,
        string title,
        IReadOnlyList<NonStandardTagSearchOption> options,
        IReadOnlyCollection<string>? initialSelected,
        int maxSelectionCount)
    {
        if (navigation == null)
            return Array.Empty<string>();

        var presentedModally = navigation.ModalStack.Count > 0 || navigation.NavigationStack.Count == 0;
        var page = new NonStandardTagSearchPage(title, options, initialSelected, maxSelectionCount, presentedModally);

        if (presentedModally)
            await navigation.PushModalAsync(page);
        else
            await navigation.PushAsync(page);

        return await page._completion.Task;
    }

    protected override bool OnBackButtonPressed()
    {
        _completion.TrySetResult(Array.Empty<string>());
        return base.OnBackButtonPressed();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _completion.TrySetResult(Array.Empty<string>());
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
        => ApplyFilter(e.NewTextValue ?? string.Empty);

    private void OnOptionTapped(object? sender, TappedEventArgs e)
    {
        var option = e.Parameter as NonStandardTagSearchOptionVm
            ?? (sender as BindableObject)?.BindingContext as NonStandardTagSearchOptionVm;
        if (option == null)
            return;

        var value = (option.Value ?? string.Empty).Trim();
        if (value.Length == 0)
            return;

        if (_selected.Contains(value))
            _selected.Remove(value);
        else
        {
            if (_maxSelectionCount > 0 && _selected.Count >= _maxSelectionCount)
                return;

            _selected.Add(value);
        }

        ApplyFilter(string.Empty);
        RefreshSelectedTags();
        OnPropertyChanged(nameof(HasSelection));
    }

    private void OnRemoveSelectedTagClicked(object? sender, EventArgs e)
    {
        var option = (sender as Button)?.CommandParameter as NonStandardTagSearchOptionVm
            ?? (sender as BindableObject)?.BindingContext as NonStandardTagSearchOptionVm;
        if (option == null)
            return;

        _selected.Remove(option.Value);
        ApplyFilter(string.Empty);
        RefreshSelectedTags();
        OnPropertyChanged(nameof(HasSelection));
    }

    private async void OnConfirmClicked(object? sender, EventArgs e)
    {
        _completion.TrySetResult(_selected.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList());
        await CloseSelfAsync();
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        _completion.TrySetResult(Array.Empty<string>());
        await CloseSelfAsync();
    }

    private void ApplyFilter(string query)
    {
        var normalized = (query ?? string.Empty).Trim();
        var items = _allOptions
            .Where(option => normalized.Length == 0 || option.Title.Contains(normalized, StringComparison.OrdinalIgnoreCase))
            .OrderBy(option => option.Title, StringComparer.OrdinalIgnoreCase)
            .Select(option => new NonStandardTagSearchOptionVm(option, _selected.Contains(option.Value)))
            .ToList();

        FilteredOptions.Clear();
        foreach (var item in items)
            FilteredOptions.Add(item);
    }

    private void RefreshSelectedTags()
    {
        SelectedTags.Clear();
        foreach (var item in _selected.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            SelectedTags.Add(new NonStandardTagSearchOptionVm(new NonStandardTagSearchOption(item, item), true));
    }

    private async Task CloseSelfAsync()
    {
        if (_presentedModally && Navigation.ModalStack.LastOrDefault() == this)
            await Navigation.PopModalAsync();
        else if (Navigation.NavigationStack.LastOrDefault() == this)
            await Navigation.PopAsync();
    }
}

public sealed class NonStandardTagSearchOptionVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public NonStandardTagSearchOptionVm(NonStandardTagSearchOption option, bool isSelected)
    {
        Option = option;
        _isSelected = isSelected;
    }

    private bool _isSelected;

    public NonStandardTagSearchOption Option { get; }
    public string Title => Option.Title;
    public string Value => Option.Value;
    public IReadOnlyList<string> DisplayChips => new[] { Title };
    public string SelectionGlyph => _isSelected ? "\uf14a" : "\uf0c8";

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;

            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectionGlyph)));
        }
    }
}
