using System.Collections.ObjectModel;
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
    public string Subtitle => Entry.Subtitle;
}

public partial class NonStandardWalletPage : ContentPage
{
    private readonly List<NonStandardWalletEntryVm> _allEntries = new();
    private string _searchText = string.Empty;

    public event Action<NonStandardWalletEntry>? EditRequested;

    public ObservableCollection<NonStandardWalletEntryVm> FilteredEntries { get; } = new();

    public string SummaryText
        => FilteredEntries.Count == 0
            ? "No non-standard entries found."
            : $"{FilteredEntries.Count} entries";

    public NonStandardWalletPage()
    {
        InitializeComponent();
        BindingContext = this;
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

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        _searchText = (e.NewTextValue ?? string.Empty).Trim();
        ApplyFilter();
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        await ReloadAsync();
    }

    private void OnEditClicked(object sender, EventArgs e)
    {
        var vm = (sender as Button)?.CommandParameter as NonStandardWalletEntryVm
            ?? (sender as BindableObject)?.BindingContext as NonStandardWalletEntryVm;
        if (vm == null)
            return;

        EditRequested?.Invoke(vm.Entry);
    }

    private void ApplyFilter()
    {
        IEnumerable<NonStandardWalletEntryVm> query = _allEntries;
        if (_searchText.Length > 0)
        {
            query = query.Where(entry =>
                entry.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
                || entry.Subtitle.Contains(_searchText, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = query
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Subtitle, StringComparer.OrdinalIgnoreCase)
            .ToList();

        FilteredEntries.Clear();
        foreach (var entry in ordered)
            FilteredEntries.Add(entry);

        OnPropertyChanged(nameof(SummaryText));
    }
}
