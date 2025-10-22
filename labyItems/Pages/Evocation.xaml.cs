using labyItems.Services;

namespace labyItems.Pages;

public partial class Evocation : ContentPage
{
    public record Result(string Name, int Power, IReadOnlyList<string> Fields);
    public class Row
    {
        public string Name { get; init; } = "";
        public string FieldsText { get; init; } = "";
        public string PowerText { get; init; } = "";
        public Result AsResult { get; init; } = new("", 0, Array.Empty<string>());
    }

    private readonly List<Row> _rows = new();
    private TaskCompletionSource<Result?>? _tcs;

    public Evocation()
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
        var list = await EarthPowerService.SearchAsync(q);
        _rows.Clear();
        _rows.AddRange(list.Select(e => new Row
        {
            Name = e.Name,
            FieldsText = e.Fields.Count > 0 ? string.Join(", ", e.Fields) : "—",
            PowerText = $"Power: {e.Power}",
            AsResult = new Result(e.Name, e.Power, e.Fields)
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

    // Allow calling code to await a picked value
    public async Task<Result?> PickAsync(INavigation nav)
    {
        _tcs = new TaskCompletionSource<Result?>();
        await nav.PushAsync(this);
        return await _tcs.Task;
    }
}
