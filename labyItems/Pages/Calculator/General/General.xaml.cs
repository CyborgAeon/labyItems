using System.ComponentModel;
using labyItems.Services;

namespace labyItems.Pages.Calculator;

public partial class General : ContentPage
{
    private readonly List<EvolutionService.EvolutionResult> _rows = new();
    private TaskCompletionSource<EvolutionService.EvolutionResult?>? _tcs;

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
        var list = await EvolutionService.SearchByIndexAsync(q);
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
        if (e.CurrentSelection.FirstOrDefault() is EvolutionService.EvolutionResult row)
        {
            _tcs?.TrySetResult(row);
            ((CollectionView)sender!).SelectedItem = null;
            await Navigation.PopAsync();
        }
    }

    public async Task<EvolutionService.EvolutionResult?> PickAsync(INavigation nav)
    {
        _tcs = new TaskCompletionSource<EvolutionService.EvolutionResult?>();
        await nav.PushAsync(this);
        return await _tcs.Task;
    }
}
