using labyItems.Pages.Configs;
using labyItems.Services;
namespace labyItems.Pages;

public partial class EvocationConfigPage : ContentPage
{
    private readonly TaskCompletionSource<EvocationConfig?> _tcs = new();

    public Task<EvocationConfig?> Completion => _tcs.Task;

    public EvocationConfigPage(EarthPowerService.EvocEntry baseEvoc)
    {
        InitializeComponent();
        BindingContext = new EvocationConfig(baseEvoc);
    }

    public EvocationConfigPage(Evocation.Result picked)
        : this(new EarthPowerService.EvocEntry(picked.Name, picked.Power, picked.Fields)){}

    private async void OnReturn(object sender, EventArgs e)
    {
        var cfg = (EvocationConfig)BindingContext;
        _tcs.TrySetResult(cfg);
        await Navigation.PopAsync();
    }
}
