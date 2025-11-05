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
        var basic = cfg.BasicPerDay > 0 ? $"Cast x {cfg.BasicPerDay}/day" : null;
        var adv = cfg.AdvancedPerDay > 0 ? $"Cast x {cfg.AdvancedPerDay}/day" : null;
        var tags = new List<string?>(new[]{
            basic, adv,
            cfg.AddBasic ? $"add {cfg.EvocationName} to base list (B)" : null,
            cfg.AddAdvanced ? $"add {cfg.EvocationName} to base list (A)" : null,
            cfg.AddPrep ? $"add {cfg.EvocationName} to base list with 30s prep" : null
        }).Where(s => !string.IsNullOrWhiteSpace(s));

        var tagText = string.Join(",\n", tags);
        var name = string.IsNullOrWhiteSpace(cfg.EvocationName) ? "Evocation" : cfg.EvocationName;
        return $"{name}: {tagText} → {cfg.Total} ISP";
    }

    private async void OnSearchEvocation(object sender, EventArgs e)
    {
        var picked = await new Evocation().PickAsync(Navigation);
        if (picked == null) return;

        var cfg = (EvocationConfig)BindingContext;
        cfg.ApplyEvocation(picked);

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
