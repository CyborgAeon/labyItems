using labyItems.Models;
using labyItems.Pages.Configs;
using labyItems.Services;

namespace labyItems.Pages.Calculator;

public partial class EvocationConfigPage : ConfigPageBase<EvocationConfig>
{
    public EvocationConfigPage()
    {
        InitializeComponent();
    }

    protected override string BuildSummary(EvocationConfig cfg)
    {
        var basic = cfg.BasicPerDay > 0 ? $"Cast x {cfg.BasicPerDay}/day" : null;
        var adv   = cfg.AdvancedPerDay > 0 ? $"Cast x {cfg.AdvancedPerDay}/day" : null;

        var tags = new List<string?>(new[]
        {
            basic,
            adv,
            cfg.AddBasic ? $"add {cfg.EvocationName} to base list (B)" : null,
            cfg.AddAdvanced ? $"add {cfg.EvocationName} to base list (A)" : null,
            cfg.AddPrep ? $"add {cfg.EvocationName} to base list with 30s prep" : null,
            cfg.DrawOnEpPerDay > 0 ? $"draw on EP {cfg.DrawOnEpPerDay}/day (16×)" : null
        }).Where(s => !string.IsNullOrWhiteSpace(s));

        var tagText = string.Join(",\n", tags);
        var name = string.IsNullOrWhiteSpace(cfg.EvocationName) ? "Evocation" : cfg.EvocationName;
        return $"{name}: {tagText} → {cfg.Total} ISP";
    }

    private async void OnSearchEvocation(object sender, EventArgs e)
    {
        var picked = await new Evocation().PickAsync(Navigation);
        if (picked == null) return;

        if (BindingContext is EvocationConfig cfg)
            cfg.ApplyEvocation(picked);
    }
}
