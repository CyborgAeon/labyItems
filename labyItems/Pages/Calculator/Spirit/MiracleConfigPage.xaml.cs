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
            Summary  = BuildSummary(cfg)
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


    protected override string BuildSummary(MiracleConfig cfg)
    {
        var tags = new List<string>();

        if (cfg.BasicPerDay > 0)     tags.Add($"Basic x{cfg.BasicPerDay}");
        if (cfg.AdvancedPerDay > 0)  tags.Add($"Advanced x{cfg.AdvancedPerDay}");
        if (cfg.InnateIsMantic)      tags.Add("Innates mantic ×4");

        if (cfg.GeneralSpiritStore > 0) tags.Add($"{cfg.GeneralSpiritStore} additional spirits (4×{cfg.GeneralSpiritStore})");
        if (cfg.SphereSpiritStore  > 0) tags.Add($"{cfg.SphereSpiritStore} additional {SphereSel} spirits (3×{cfg.SphereSpiritStore})");
        if (cfg.SpiritStoreRegenerates && (cfg.GeneralSpiritStore > 0 || cfg.SphereSpiritStore > 0))
            tags.Add("Store regenerates +25");
        if (cfg.IsAdvanced == false && cfg.AddBasicToList)    tags.Add($"add {cfg.Name} to base list (B)");
        if (cfg.IsAdvanced == true  && cfg.AddAdvancedToList) tags.Add($"add {cfg.Name} to base list (A)");

        if (cfg.AddWithPrep30) tags.Add($"add {cfg.Name} to base list with 30s prep");
        if (cfg.TurnBasicUpTo5thMantic      > 0) tags.Add($"Turn handbook → ≤5th mantic ×{cfg.TurnBasicUpTo5thMantic} (40×)");
        if (cfg.TurnBasicMantic             > 0) tags.Add($"Turn any handbook mantic ×{cfg.TurnBasicMantic} (50×)");
        if (cfg.TurnAdvancedUpTo6thMantic   > 0) tags.Add($"Turn advanced → ≤6th mantic ×{cfg.TurnAdvancedUpTo6thMantic} (60×)");
        if (cfg.TurnAdvancedAbove6thMantic  > 0) tags.Add($"Turn any advanced mantic ×{cfg.TurnAdvancedAbove6thMantic} (80×)");

        if (cfg.IsTeachingScroll) tags.Add($"Teaching scroll (3×Power={3 * cfg.Power})");
        if (cfg.TrueBeliever > 0) tags.Add($"True believer ×{cfg.TrueBeliever} (16×)");

        var name = string.IsNullOrWhiteSpace(cfg.Name) ? "Miracle" : cfg.Name;
        var tagText = string.Join(", ", tags);
        return $"{name}: {tagText} → {cfg.Total} ISP";
    }
    
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

        var res = new CalcResult { TotalIsp = cfg.Total, Summary = BuildSummary(cfg) };
        _tcs.TrySetResult(res);
        await Navigation.PopAsync();
    }
}