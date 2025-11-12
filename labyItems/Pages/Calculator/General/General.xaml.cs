using System.ComponentModel;
using labyItems.Services;
using static labyItems.Services.GeneralService;

namespace labyItems.Pages.Calculator;

public partial class General : ContentPage
{
    public sealed record Result {
        public string Index { get; init; } = string.Empty;
        public  string Description {get;init;} = string.Empty;
        public int Cost { get; init; } = 0;
        public int Table { get; init; } = 0;
        public bool? IsImmunity { get; init; } = null;
    }
    private readonly List<Result> _rows = new();
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
        
        _rows.AddRange(list);
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
