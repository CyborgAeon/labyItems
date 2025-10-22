using labyItems.Pages.Configs;
using labyItems.Services;

namespace labyItems.Pages;

public partial class EvocationConfigPage : ContentPage
{
    private readonly TaskCompletionSource<EvocationConfig?> _tcs = new();
    public Task<EvocationConfig?> Completion => _tcs.Task;

    public EvocationConfigPage()
    {
        InitializeComponent();
        BindingContext = new EvocationConfig(); // start empty; user can search or type power
    }

    // Optional convenience constructor if you *do* already have a pick
    public EvocationConfigPage(Evocation.Result picked) : this()
    {
        ((EvocationConfig)BindingContext).ApplyEvocation(picked);
    }

    private async void OnSearchEvocation(object sender, EventArgs e)
    {
            var picked = await new Evocation().PickAsync(Navigation); // your search/autocomplete page
            if (picked == null) return;

            var cfg = (EvocationConfig)BindingContext;
            cfg.ApplyEvocation(picked); // updates EvocationName and Power
        
    }

    private async void OnReturn(object sender, EventArgs e)
    {
        _tcs.TrySetResult((EvocationConfig)BindingContext);
        await Navigation.PopAsync();
    }
}
