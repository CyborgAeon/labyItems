using labyItems.Services;

namespace labyItems.Pages;

public partial class Spell : ContentPage
{
    public record Result(string Name, int Power, string colour);
    public class Row
    {
        public string Name { get; init; } = string.Empty;
        public string ColourText { get; init; } = string.Empty;
        public string PowerText { get; init; } = string.Empty;
        public Result AsResult { get; init; } = new(string.Empty, 0, string.Empty);
    }

    private readonly List<Row> _rows = new();
    private TaskCompletionSource<Result?>? _tcs;

    public Spell()
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
        var list = await SpellService.SearchAsync(q);
        _rows.Clear();
        _rows.AddRange(list.Select(e => new Row
        {
            Name = e.Name,
            ColourText = e.Colour,
            PowerText = $"Power: {e.Power}",
            AsResult = new Result(e.Name, e.Power, e.Colour)
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
