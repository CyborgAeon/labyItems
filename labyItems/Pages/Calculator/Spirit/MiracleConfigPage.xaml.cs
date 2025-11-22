using labyItems.Models;
using labyItems.Pages.Configs;
using labyItems.Models.DTOs;
using Microsoft.Maui.Controls;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using labyItems.Controls;

namespace labyItems.Pages.Configs;
public partial class MiracleConfigPage : ConfigPageBase<MiracleConfig>
{
    public MiracleConfigPage()
    {
        InitializeComponent();
    }

    private async void OnFooterReturnClicked(object sender, EventArgs e)
    {
        if (BindingContext is not MiracleConfig cfg) return;

        var result = new CalcResult
        {
            TotalIsp = cfg.Total,
            AbilityType = "Miracle",
            AbilityName = string.IsNullOrWhiteSpace(cfg.Name) ? "Miracle" : cfg.Name,
            Details = BuildDetails(cfg)
        };

        if (Navigation?.NavigationStack?.Count > 1)
        {
            _tcs.TrySetResult(result);
            await Navigation.PopAsync();
            return;
        }

        if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        _tcs.TrySetResult(result);
        await Navigation.PopAsync();
    }


    protected override CalcResult BuildResult(MiracleConfig cfg) =>
        new()
        {
            AbilityType = "Miracle",
            AbilityName = string.IsNullOrWhiteSpace(cfg.Name) ? "Miracle" : cfg.Name,
            TotalIsp = cfg.Total,
            Details = BuildDetails(cfg)
        };
    
    private async void OnSearchMiracle(object sender, EventArgs e)
    {
        var picked = await new Miracle().PickAsync(Navigation);
        if (picked is null) return;

        if (BindingContext is MiracleConfig cfg)
            cfg.ApplyMiracle(picked);
    }

    private async void OnReturn(object sender, EventArgs e)
    {
        if (BindingContext is not MiracleConfig cfg) return;

        var res = BuildResult(cfg);
        _tcs.TrySetResult(res);
        await Navigation.PopAsync();
    }

    private Dictionary<string, object?> BuildDetails(MiracleConfig cfg)
    {
        var details = new Dictionary<string, object?>();

        if (cfg.BasicPerDay > 0) details["basicPerDay"] = cfg.BasicPerDay;
        if (cfg.AdvancedPerDay > 0) details["advancedPerDay"] = cfg.AdvancedPerDay;
        if (cfg.InnateIsMantic) details["innateIsMantic"] = true;
        if (cfg.GeneralSpiritStore > 0) details["generalSpiritStore"] = cfg.GeneralSpiritStore;
        if (cfg.SphereSpiritStore > 0) details["sphereSpiritStore"] = cfg.SphereSpiritStore;
        if (cfg.SpiritStoreRegenerates) details["spiritStoreRegenerates"] = cfg.SpiritStoreRegenerates;
        if (cfg.AddBasicToList) details["addBasicToList"] = true;
        if (cfg.AddAdvancedToList) details["addAdvancedToList"] = true;
        if (cfg.AddWithPrep30) details["addWithPrep30"] = true;
        if (cfg.TurnBasicUpTo5thMantic > 0) details["turnBasicUpTo5thMantic"] = cfg.TurnBasicUpTo5thMantic;
        if (cfg.TurnBasicMantic > 0) details["turnBasicMantic"] = cfg.TurnBasicMantic;
        if (cfg.TurnAdvancedUpTo6thMantic > 0) details["turnAdvancedUpTo6thMantic"] = cfg.TurnAdvancedUpTo6thMantic;
        if (cfg.TurnAdvancedAbove6thMantic > 0) details["turnAdvancedAbove6thMantic"] = cfg.TurnAdvancedAbove6thMantic;
        if (cfg.IsTeachingScroll) details["isTeachingScroll"] = true;
        if (cfg.TrueBeliever > 0) details["trueBeliever"] = cfg.TrueBeliever;

        return details;
    }
}
