using System.ComponentModel;
using labyItems.Services;

namespace labyItems.Pages;

public partial class General : ContentPage
{
    public record Result(string Index, string Description, int Cost, int table);
    public class Row
    {
        public string Index { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string TableText { get; init; } = string.Empty;
        public string CostText { get; init; } = string.Empty;
        public Result AsResult { get; init; } = new(string.Empty, string.Empty, 0, 0);
    }

    private readonly List<Row> _rows = new();
    private TaskCompletionSource<Result?>? _tcs;

    public General()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Load("");
    }

    private async Task Load(string q)
    {
        var list = await GeneralService.SearchByIndexAsync(q);
        _rows.Clear();
        _rows.AddRange(list.Select(e => new Row
        {
            Index = e.Index,
            TableText = $"Table: {e.Table.ToString()}",
            CostText = $"Cost: {e.Cost}",
Description = e.Description,
            AsResult = new Result(e.Index, e.Description, e.Cost, e.Table)
        }));
        Results.ItemsSource = null;
        Results.ItemsSource = _rows;
    }

    private async void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        await Load(e.NewTextValue ?? "");
    }

    private async void OnPick(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Row row)
        {
            _tcs?.TrySetResult(row.AsResult);
            ((CollectionView)sender!).SelectedItem = null;
            await Navigation.PopAsync();
        }
    }

    public async Task<Result?> PickAsync(INavigation nav)
    {
        _tcs = new TaskCompletionSource<Result?>();
        await nav.PushAsync(this);
        return await _tcs.Task;
    }
}
