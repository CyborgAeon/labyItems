using System.Collections.ObjectModel;
using System.Collections.Specialized;
using labyItems.Services;

namespace labyItems.Pages.NonStandard;

public sealed class NonStandardWalletEntryVm
{
    public NonStandardWalletEntryVm(NonStandardWalletEntry entry)
    {
        Entry = entry;
    }

    public NonStandardWalletEntry Entry { get; }
    public string Name => Entry.Name;
    public string Subtitle => (Entry.Subtitle ?? string.Empty).Replace("â€¢", "•");
    public string FilterKey => Entry.EntityType switch
    {
        NonStandardEntityType.CharacterClass => "Classes",
        NonStandardEntityType.CharacterRace => "Races",
        NonStandardEntityType.Ability => "Abilities",
        NonStandardEntityType.Spell => "Spells",
        NonStandardEntityType.Miracle => "Miracles",
        NonStandardEntityType.Evocation => "Evocations",
        _ => "Other"
    };

    public string UpdatedText => $"Updated {FormatRelativeTime(Entry.UpdatedAtUtc)}";

    private static string FormatRelativeTime(DateTimeOffset timestamp)
    {
        var span = DateTimeOffset.UtcNow - timestamp;
        if (span.TotalDays >= 2)
            return $"{Math.Floor(span.TotalDays)}d ago";
        if (span.TotalDays >= 1)
            return "1d ago";
        if (span.TotalHours >= 2)
            return $"{Math.Floor(span.TotalHours)}h ago";
        if (span.TotalHours >= 1)
            return "1h ago";
        if (span.TotalMinutes >= 2)
            return $"{Math.Floor(span.TotalMinutes)}m ago";
        return "just now";
    }
}

public partial class NonStandardWalletPage : ContentPage
{
    private readonly List<NonStandardWalletEntryVm> _allEntries = new();

    public ObservableCollection<string> AvailableFilters { get; } =
    [
        "Classes",
        "Races",
        "Abilities",
        "Spells",
        "Miracles",
        "Evocations"
    ];

    public ObservableCollection<string> SelectedFilters { get; } = new();
    public ObservableCollection<NonStandardWalletEntryVm> FilteredEntries { get; } = new();

    public string SummaryText
        => FilteredEntries.Count == 0
            ? "No creations yet."
            : $"{FilteredEntries.Count} creation{(FilteredEntries.Count == 1 ? string.Empty : "s")}";

    public NonStandardWalletPage()
    {
        InitializeComponent();
        BindingContext = this;
        SelectedFilters.CollectionChanged += OnSelectedFiltersChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        try
        {
            var entries = await NonStandardContentService.GetWalletEntriesAsync();
            _allEntries.Clear();
            _allEntries.AddRange(entries.Select(entry => new NonStandardWalletEntryVm(entry)));
            ApplyFilter();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Load failed", ex.Message, "OK");
        }
    }

    private void OnSelectedFiltersChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => ApplyFilter();

    private void ApplyFilter()
    {
        IEnumerable<NonStandardWalletEntryVm> query = _allEntries;
        if (SelectedFilters.Count > 0)
        {
            query = query.Where(entry => SelectedFilters.Any(filter =>
                string.Equals(filter, entry.FilterKey, StringComparison.OrdinalIgnoreCase)));
        }

        var ordered = query
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.FilterKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        FilteredEntries.Clear();
        foreach (var entry in ordered)
            FilteredEntries.Add(entry);

        OnPropertyChanged(nameof(SummaryText));
    }

    private async void OnCreateNewClicked(object sender, EventArgs e)
        => await Navigation.PushAsync(new NonStandardDashboardPage());

    private async void OnReviewClicked(object sender, EventArgs e)
    {
        var vm = ResolveVm(sender);
        if (vm == null)
            return;

        await OpenEntryAsync(vm.Entry);
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        var vm = ResolveVm(sender);
        if (vm == null)
            return;

        await OpenEntryAsync(vm.Entry);
    }

    private async void OnFilesClicked(object sender, EventArgs e)
    {
        var vm = ResolveVm(sender);
        if (vm == null)
            return;

        await Navigation.PushAsync(new NonStandardDocumentLinksPage(vm.Entry));
    }

    private static NonStandardWalletEntryVm? ResolveVm(object sender)
    {
        if (sender is not BindableObject bindable)
            return null;

        return (bindable as Button)?.CommandParameter as NonStandardWalletEntryVm
            ?? bindable.BindingContext as NonStandardWalletEntryVm;
    }

    private async Task OpenEntryAsync(NonStandardWalletEntry entry)
    {
        if (entry.EntityType == NonStandardEntityType.CharacterClass)
        {
            var page = new NonStandardClassCreatePage();
            await Navigation.PushAsync(page);
            await page.LoadFromWalletEntryAsync(entry);
            return;
        }

        var pageLegacy = new NonStandardLegacyCreatePage
        {
            FixedEntityTypeKey = entry.EntityType.ToString()
        };

        await Navigation.PushAsync(pageLegacy);
        await pageLegacy.LoadFromWalletEntryAsync(entry);
    }
}
