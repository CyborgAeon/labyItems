using labyItems.Models;
using labyItems.Pages.Configs;
using labyItems.Services;
using System.Collections.Generic;

namespace labyItems.Pages.Calculator;

public partial class EvocationConfigPage : ConfigPageBase<EvocationConfig>
{
    public EvocationConfigPage()
    {
        InitializeComponent();
    }

    protected override CalcResult BuildResult(EvocationConfig cfg)
    {
        var basic = cfg.BasicPerDay > 0 ? $"Cast x {cfg.BasicPerDay}/day" : null;
        var adv   = cfg.AdvancedPerDay > 0 ? $"Cast x {cfg.AdvancedPerDay}/day" : null;

        var details = new Dictionary<string, object?>();
        if (basic is not null) details["basicPerDay"] = cfg.BasicPerDay;
        if (adv is not null) details["advancedPerDay"] = cfg.AdvancedPerDay;
        if (cfg.AddBasic) details["addBasicToList"] = true;
        if (cfg.AddAdvanced) details["addAdvancedToList"] = true;
        if (cfg.AddPrep) details["addWithPrep30"] = true;
        if (cfg.DrawOnEpPerDay > 0) details["drawOnEpPerDay"] = cfg.DrawOnEpPerDay;

        var name = string.IsNullOrWhiteSpace(cfg.EvocationName) ? "Evocation" : cfg.EvocationName;
        return new CalcResult
        {
            AbilityType = "Evocation",
            AbilityName = name,
            TotalIsp = cfg.Total,
            Details = details
        };
    }

    private async void OnSearchEvocation(object sender, EventArgs e)
    {
        var picked = await new Evocation().PickAsync(Navigation);
        if (picked == null) return;

        if (BindingContext is EvocationConfig cfg)
            cfg.ApplyEvocation(picked);
    }
}
