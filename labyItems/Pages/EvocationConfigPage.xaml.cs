using labyItems.Models;
using labyItems.Pages.Configs;
using labyItems.Services;
using static labyItems.Pages.Evocation;

namespace labyItems.Pages;

public partial class EvocationConfigPage : ContentPage
{
    private readonly TaskCompletionSource<CalcResult?> _tcs = new();
    public Task<CalcResult?> Completion => _tcs.Task;

    public EvocationConfigPage()
    {
        InitializeComponent();
        BindingContext = new EvocationConfig();
    }

    private string BuildSummary(EvocationConfig cfg)
    {
        // One-line summary for contribution list:
        var basic = cfg.BasicPerDay > 0 ? $"Basic x{cfg.BasicPerDay}" : null;
        var adv = cfg.AdvancedPerDay > 0 ? $"Advanced x{cfg.AdvancedPerDay}" : null;
        var tags = new List<string?>(new[]{
            basic, adv,
            cfg.AddBasic ? "+Basic" : null,
            cfg.AddAdvanced ? "+Advanced" : null,
            cfg.AddPrep ? "+30s prep" : null
        }).Where(s => !string.IsNullOrWhiteSpace(s));

        var tagText = string.Join(", ", tags);
        var name = string.IsNullOrWhiteSpace(cfg.EvocationName) ? "Evocation" : cfg.EvocationName;
        return $"{name}: {tagText} → {cfg.Total} ISP";
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
        var cfg = (EvocationConfig)BindingContext;
        var res = new CalcResult
        {
            TotalIsp = cfg.Total,
            Summary = BuildSummary(cfg)
        };
        // Complete the task then pop
        _tcs.TrySetResult(res);
        await Navigation.PopAsync();
    }
}
