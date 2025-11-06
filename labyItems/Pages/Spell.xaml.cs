using labyItems.Services;

namespace labyItems.Pages;

public partial class Spell : ContentPage
{
    public sealed record Result
    {
        public string Name { get; init; } = string.Empty;
        public int Power { get; init; } = 0;
        public string Colour { get; init; }
        public bool IsAdvanced { get; init; }
    }

    private readonly List<Result> _rows = new();
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
        _rows.AddRange(list.Select(e => new Result
        {
            Name = e.name,
            Colour = e.colour,
            Power = e.level,
            IsAdvanced = e.isAdvanced ?? false,
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
        if (e.CurrentSelection.FirstOrDefault() is Result row)
        {
            _tcs?.TrySetResult(row);
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
