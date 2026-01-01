using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using labyItems.Controls;
using labyItems.Helpers;
using labyItems.Models;
using labyItems.Models.Enums;
using labyItems.Pages.Configs;
using Microsoft.Maui.Controls;

namespace labyItems.Pages.Calculator;

public partial class GeneralConfigPage : ConfigPageBase<GeneralConfig>
{
    public GeneralConfigPage()
    {
        InitializeComponent();
        // BindingContext = new GeneralConfig();
        // ReturnFromConfigCommand = new Command(async () =>
        // {
        //     if (BindingContext is not GeneralConfig cfg)
        //         return;
        //     // cfg.LtmType = PowerbaseEnum.Physical;
        //     var result = new CalcResult { TotalIsp = cfg.Total, Summary = BuildSummary(cfg) };

        //     _tcs.TrySetResult(result);
        //     await StickyFooterControl.DefaultNavigateAsync(this);
        // });
    }

    private string BuildNotes(GeneralConfig c)
    {
        var s = new List<string>();

    AddResistanceLevels(s, c);
        s.AddToSummaryIf(
            c.CastingLevelsCount,
            $"+{c.CastingLevelsCount} Casting levels {(c.CastingLevelsColour?.ToString() ?? "None")}"
        );

        s.AddToSummaryIf(c.StrengthEnchantCost, c.StrengthEnchantDescription);
        s.AddToSummaryIf(c.ColdRage25PerDayCount, $"25% Cold Rage ({c.ColdRage25PerDayCount}/day)");
        s.AddToSummaryIf(
            c.BerserkRage50PerDayCount,
            $"50% Berserk Rage ({c.BerserkRage50PerDayCount}/day)"
        );
        s.AddToSummaryIf(
            c.RageCategoriesCount,
            $"Perm 25% rage vs {ListSummaryHelper.JoinWithAnd(c.PermRageCategoryItems)}"
        );
        s.AddToSummaryIf(c.RepelAttractTypeCount, c.RepelAttractGroupLabel);
        s.AddToSummaryIf(c.RepelAttractGroupCount, c.RepelAttractTypeLabel);
        s.AddToSummaryIf(c.RepelLifeCount, $"Repel Life ({c.RepelLifeCount}/day)");
        s.AddToSummaryIf(c.DisciplinePerDayCount, $"Discipline ({c.DisciplinePerDayCount}/day)");
        s.AddToSummaryIf(
            c.WardPact8LevelsCount,
            $"Ward Pact (8 levels) vs {ListSummaryHelper.JoinWithAnd(c.WardPacts)}"
        );
        s.AddToSummaryIf(
            c.KiOrPrimalStrikePerDayCount,
            $"{c.KiOrPrimalStrike} Strike ({c.KiOrPrimalStrikePerDayCount}/day)"
        );
        s.AddToSummaryIf(
            c.EmpowerWeaponMagicCount,
            ListSummaryHelper.BuildEmpowerMagicSummary(
                c.EmpowerWeaponMagicCount,
                c.ExtraColours
            )
        );

        s.AddToSummaryIf(
            c.EmpowerWeaponSpiritCount,
            ListSummaryHelper.BuildEmpowerSpiritSummary(
                c.EmpowerWeaponSpiritCount,
                c.ExtraAlignments
            )
        );

        s.AddToSummaryIf(
            c.EmpowerWeaponManticCount,
            ListSummaryHelper.BuildEmpowerManticSummary(
                c.EmpowerWeaponManticCount,
                c.ExtraAlignments,
                c.ExtraColours
            )
        );

        s.AddToSummaryIf(c.ScholarlyInterestPerDayCount, "Scholarly Interest (1/day)");
        s.AddToSummaryIf(c.KnowledgeOfArcanePerDayCount, $"Knowledge of the Arcane ({c.KnowledgeOfArcanePerDayCount}/day)");
        s.AddToSummaryIf(c.PrayerTimesPerDay, $"{c.PrayerSize} Prayer ({c.PrayerPowerbase}){(string.IsNullOrEmpty(c.PrayerSubject) ? string.Empty : $" {c.PrayerSubject} ")}({c.PrayerTimesPerDay}/day)");

        if (c.LtmValue > 0)
        {
            var pts = c.LtmValue * 6;
            s.Add(
                $"Additional life to minus: +{pts} (5 per 6pts){(string.IsNullOrEmpty(c.LtmType) ? ", is " + c.LtmType + " ×2 cost" : string.Empty)}"
            );
        }

        s.AddToSummaryIf(
            (c.ElfInnateColour is not null),
            $"Lvl: {c.ElvenInnateLevel} {c.ElfInnateColour} Elven Innates"
        );
        // Utilities
        if (c.ReadLanguages)
            s.Add("Read languages");
        if (c.DisarmTrapsAsScout)
            s.Add("Disarm traps (as scout)");
        s.AddToSummaryIf(c.PotionRecipesKnownCount, "Potion recipes known");
        if (c.Regeneration)
            s.Add("Regeneration (non-stacking)");
        if (c.ForearmParry)
            s.Add("Forearm Parry");

        return string.Join("\n", s);
    }

    private async void OnSearchGeneral(object sender, EventArgs e)
    {
        var picked = await new General().PickAsync(Navigation);
        if (picked == null)
            return;

        if (BindingContext is GeneralConfig cfg)
            cfg.ApplyGeneral(picked);
    }

    // private readonly TaskCompletionSource<CalcResult?> _tcs = new();
    // public Task<CalcResult?> Completion => _tcs.Task;
    // public Command ReturnFromConfigCommand { get; }


    private async void OnFooterReturnClicked(object sender, EventArgs e)
    {
        if (BindingContext is not GeneralConfig cfg)
            return;

        var result = BuildResult(cfg);

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

    public static async Task<CalcResult?> PickAsync(INavigation nav)
    {
        var page = new GeneralConfigPage();
        await nav.PushAsync(page);
        var res = await page._tcs.Task;
        return res;
    }

    private void OnReturn(object sender, EventArgs e)
    {
        if (BindingContext is not GeneralConfig cfg)
        {
            _tcs.TrySetResult(null);
            Navigation.PopAsync();
            return;
        }

        var result = BuildResult(cfg);

        _tcs.TrySetResult(result);
        Navigation.PopAsync();
    }

    protected override CalcResult BuildResult(GeneralConfig cfg)
    {
        var details = new Dictionary<string, object?>();
        var notes = BuildNotes(cfg);
        if (!string.IsNullOrWhiteSpace(notes))
            details["notes"] = notes;

        if (cfg.EmpowerWeaponSpiritCount > 0) details["empowerWeaponSpirit"] = cfg.EmpowerWeaponSpiritCount;
        if (cfg.EmpowerWeaponManticCount > 0) details["empowerWeaponMantic"] = cfg.EmpowerWeaponManticCount;
        if (cfg.UndeadTouchEffect.HasValue && cfg.UndeadTouchEffectCount > 0)
            details["undeadTouchEffect"] = new { effect = cfg.UndeadTouchEffect.ToString(), count = cfg.UndeadTouchEffectCount };
        if (cfg.GaseousFormPerDayCount > 0) details["gaseousForm"] = cfg.GaseousFormPerDayCount;
        if (cfg.WalkThroughWallsPerDayCount > 0) details["walkThroughWalls"] = cfg.WalkThroughWallsPerDayCount;
        if (cfg.PlaneShiftPerDayCount > 0) details["planeShift"] = cfg.PlaneShiftPerDayCount;
        if (cfg.PrayerTimesPerDay > 0 && cfg.PrayerPowerbase.HasValue)
        {
            details["prayerPowerbase"] = cfg.PrayerPowerbase.ToString();
            details["prayerTimesPerDay"] = cfg.PrayerTimesPerDay;
        }

        return new CalcResult
        {
            AbilityType = "General",
            AbilityName = string.IsNullOrWhiteSpace(cfg.Name) ? "General Charm" : cfg.Name,
            TotalIsp = cfg.Total,
            Details = details
        };
    }

    private static void AddResistanceLevels(List<string> summary, GeneralConfig c)
    {
        summary.AddToSummaryIf(
            c.ResistanceLevels,
            $"{c.ResistanceLevels} Levels of Resistance vs {c.ResistanceType}"
        );
    }
}
