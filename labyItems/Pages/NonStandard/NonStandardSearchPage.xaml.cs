using System.Collections.ObjectModel;

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
    private bool _presentedModally;

    public ObservableCollection<NonStandardSearchOption> FilteredOptions { get; } = new();

    public NonStandardSearchPage(string title, IReadOnlyList<NonStandardSearchOption> options)
    {
        InitializeComponent();
        BindingContext = this;

        Title = string.IsNullOrWhiteSpace(title) ? "Search" : title;
        _allOptions = options?.ToList() ?? new List<NonStandardSearchOption>();

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
        _completion.TrySetResult(null);
        return base.OnBackButtonPressed();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _completion.TrySetResult(null);
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyFilter(e.NewTextValue ?? string.Empty);
    }

    private async void OnOptionTapped(object? sender, TappedEventArgs e)
    {
        var selected = e.Parameter as NonStandardSearchOption
            ?? (sender as BindableObject)?.BindingContext as NonStandardSearchOption;
        if (selected == null)
            return;

        _completion.TrySetResult(selected);
        if (_presentedModally && Navigation.ModalStack.LastOrDefault() == this)
            await Navigation.PopModalAsync();
        else if (Navigation.NavigationStack.LastOrDefault() == this)
            await Navigation.PopAsync();
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

        FilteredOptions.Clear();
        foreach (var option in ordered)
            FilteredOptions.Add(option);
    }
}
