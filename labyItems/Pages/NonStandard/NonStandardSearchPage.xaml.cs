using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace labyItems.Pages.NonStandard;

public sealed record NonStandardSearchOption(
    string Title,
    string Subtitle,
    string Value)
{
    public bool HasSubtitle => !string.IsNullOrWhiteSpace((Subtitle ?? string.Empty).Trim());
}

public partial class NonStandardSearchPage : ContentPage
{
    private readonly List<NonStandardSearchOption> _allOptions;
    private readonly TaskCompletionSource<NonStandardSearchOption?> _completion = new();
    private readonly TaskCompletionSource<IReadOnlyList<NonStandardSearchOption>> _multiCompletion = new();
    private bool _presentedModally;
    private readonly HashSet<string> _selectedValues = new(StringComparer.OrdinalIgnoreCase);
    private readonly bool _isMultiSelect;

    public ObservableCollection<NonStandardSearchOptionItemVm> FilteredOptions { get; } = new();

    public bool IsMultiSelect => _isMultiSelect;

    public bool HasAnySelection => _selectedValues.Count > 0;

    public NonStandardSearchPage(
        string title,
        IReadOnlyList<NonStandardSearchOption> options,
        bool isMultiSelect = false,
        IReadOnlyCollection<string>? initialSelectedValues = null)
    {
        InitializeComponent();
        BindingContext = this;

        Title = string.IsNullOrWhiteSpace(title) ? "Search" : title;
        _allOptions = options?.ToList() ?? new List<NonStandardSearchOption>();
        _isMultiSelect = isMultiSelect;

        foreach (var value in initialSelectedValues ?? Array.Empty<string>())
        {
            var token = (value ?? string.Empty).Trim();
            if (token.Length > 0)
                _selectedValues.Add(token);
        }

        ApplyFilter(string.Empty);
    }

    public static async Task<NonStandardSearchOption?> PickAsync(
        INavigation navigation,
        string title,
        IReadOnlyList<NonStandardSearchOption> options)
    {
        if (navigation == null)
            return null;

        var page = new NonStandardSearchPage(title, options);
        if (ShouldPresentModally(navigation))
        {
            page._presentedModally = true;
            await navigation.PushModalAsync(page);
        }
        else
        {
            await navigation.PushAsync(page);
        }

        return await page._completion.Task;
    }

    public static async Task<IReadOnlyList<NonStandardSearchOption>> PickManyAsync(
        INavigation navigation,
        string title,
        IReadOnlyList<NonStandardSearchOption> options,
        IReadOnlyCollection<string>? initialSelectedValues = null)
    {
        if (navigation == null)
            return Array.Empty<NonStandardSearchOption>();

        var page = new NonStandardSearchPage(
            title,
            options,
            isMultiSelect: true,
            initialSelectedValues: initialSelectedValues);

        if (ShouldPresentModally(navigation))
        {
            page._presentedModally = true;
            await navigation.PushModalAsync(page);
        }
        else
        {
            await navigation.PushAsync(page);
        }

        return await page._multiCompletion.Task;
    }

    private static bool ShouldPresentModally(INavigation navigation)
    {
        if (navigation.ModalStack.Count > 0)
            return true;

        if (navigation.NavigationStack.Count == 0)
            return true;

        return false;
    }

    protected override bool OnBackButtonPressed()
    {
        CompleteCancel();
        return base.OnBackButtonPressed();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        CompleteCancel();
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        CompleteCancel();
        await CloseSelfAsync();
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyFilter(e.NewTextValue ?? string.Empty);
    }

    private async void OnOptionTapped(object? sender, TappedEventArgs e)
    {
        var selected = e.Parameter as NonStandardSearchOptionItemVm
            ?? (sender as BindableObject)?.BindingContext as NonStandardSearchOptionItemVm;
        if (selected == null)
            return;

        if (_isMultiSelect)
        {
            ToggleSelection(selected);
            return;
        }

        _completion.TrySetResult(selected.Option);
        await CloseSelfAsync();
    }

    private async void OnConfirmClicked(object? sender, EventArgs e)
    {
        if (!_isMultiSelect)
            return;

        var selected = _allOptions
            .Where(option => _selectedValues.Contains((option.Value ?? string.Empty).Trim()))
            .ToList();

        _multiCompletion.TrySetResult(selected);
        await CloseSelfAsync();
    }

    private void ApplyFilter(string query)
    {
        var normalized = (query ?? string.Empty).Trim();
        IEnumerable<NonStandardSearchOption> filtered = _allOptions;
        if (normalized.Length > 0)
        {
            filtered = filtered.Where(option =>
                option.Title.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || option.Subtitle.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || option.Value.Contains(normalized, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = filtered
            .OrderBy(option => option.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.Subtitle, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var existing in FilteredOptions)
            existing.PropertyChanged -= OnOptionVmPropertyChanged;

        FilteredOptions.Clear();
        foreach (var option in ordered)
        {
            var vm = new NonStandardSearchOptionItemVm(option)
            {
                IsSelected = _selectedValues.Contains((option.Value ?? string.Empty).Trim())
            };
            vm.PropertyChanged += OnOptionVmPropertyChanged;
            FilteredOptions.Add(vm);
        }
    }

    private void ToggleSelection(NonStandardSearchOptionItemVm option)
    {
        var value = (option.Option.Value ?? string.Empty).Trim();
        if (value.Length == 0)
            return;

        if (_selectedValues.Contains(value))
        {
            _selectedValues.Remove(value);
            option.IsSelected = false;
        }
        else
        {
            _selectedValues.Add(value);
            option.IsSelected = true;
        }

        OnPropertyChanged(nameof(HasAnySelection));
    }

    private void OnOptionVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NonStandardSearchOptionItemVm.IsSelected))
            OnPropertyChanged(nameof(HasAnySelection));
    }

    private async Task CloseSelfAsync()
    {
        if (_presentedModally && Navigation.ModalStack.LastOrDefault() == this)
            await Navigation.PopModalAsync();
        else if (Navigation.NavigationStack.LastOrDefault() == this)
            await Navigation.PopAsync();
    }

    private void CompleteCancel()
    {
        _completion.TrySetResult(null);
        _multiCompletion.TrySetResult(Array.Empty<NonStandardSearchOption>());
    }
}

public sealed class NonStandardSearchOptionItemVm : INotifyPropertyChanged
{
    private bool _isSelected;

    public NonStandardSearchOptionItemVm(NonStandardSearchOption option)
    {
        Option = option;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public NonStandardSearchOption Option { get; }
    public string Title => Option.Title;
    public string Subtitle => Option.Subtitle;
    public bool HasSubtitle => Option.HasSubtitle;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!Set(ref _isSelected, value))
                return;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectionGlyph)));
        }
    }

    public string SelectionGlyph => IsSelected ? "\uf14a" : "\uf0c8";

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
