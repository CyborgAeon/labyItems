using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using labyItems.Pages.Configs;
using labyItems.Models;
using labyItems.Helpers;
namespace labyItems.Pages
{
    public partial class GeneralConfigPage : ContentPage
    {
        public bool ShowResistanceSection { get; set; } = false;
        private void ToggleResistanceSection(object sender, EventArgs e)
        => ShowResistanceSection = !ShowResistanceSection;
        private async void OnSearchGeneral(object sender, EventArgs e)
        {
            var picked = await new General().PickAsync(Navigation);
            if (picked == null) return;

            if (BindingContext is GeneralConfig cfg)
                cfg.ApplyGeneral(picked);
        }
        private readonly TaskCompletionSource<CalcResult?> _tcs = new();
        public Task<CalcResult?> Completion => _tcs.Task;

        public GeneralConfigPage(GeneralConfig? model = null)
        {
            InitializeComponent();
            BindingContext = model ?? new GeneralConfig();
        }

        public static async Task<CalcResult?> PickAsync(INavigation nav, GeneralConfig? seed = null)
        {
            var page = new GeneralConfigPage(seed);
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

            var lines = BuildSummary(cfg);
            var result = new CalcResult
            {
                TotalIsp = cfg.Total,
                Summary = lines
            };

            _tcs.TrySetResult(result);
            Navigation.PopAsync();
        }
        private static void AddResistanceLevels(List<string> summary, GeneralConfig c){
            summary.AddToSummaryIf(c.ResistanceLevels, $"{c.ResistanceLevels} Levels of Resistance vs {c.ResistanceType}");
        }

        private static string BuildSummary(GeneralConfig c)
        {
            var s = new List<string>();

            AddResistanceLevels(s, c);
            s.AddToSummaryIf(c.CastingLevelsCount, $"+{c.CastingLevelsCount} Casting levels {c.CastingLevelsColour}");

            s.AddToSummaryIf(c.StrengthPlus1NonStackingCount, "+1 Strength (non-stacking)");
            s.AddToSummaryIf(c.StrengthPlus1StackingTo2Count, "+1 Strength (stacking to +2)");
            s.AddToSummaryIf(c.StrengthPlus2NonStackingCount, "+2 Strength (non-stacking)");
            s.AddToSummaryIf(c.ColdRage25PerDayCount, "25% Cold Rage (1/day)");
            s.AddToSummaryIf(c.BerserkRage50PerDayCount, "50% Berserk Rage (1/day)");
            s.AddToSummaryIf(c.ColdRage25VsOneGroupAlwaysCount, "25% Cold Rage vs one group (always)");
            s.AddToSummaryIf(c.RepelAttractOneTypePerDayCount, "Repel/Attract one Type (1/day)");
            s.AddToSummaryIf(c.RepelAttractOneGroupPerDayCount, "Repel/Attract one Group (1/day)");
            s.AddToSummaryIf(c.RepelLifePerDayCount, "Repel Life (1/day)");
            s.AddToSummaryIf(c.DisciplinePerDayCount, "Discipline (1/day)");
            s.AddToSummaryIf(c.WardPact8LevelsCount, "Ward Pact (8 levels)");
            s.AddToSummaryIf(c.KiOrPrimalStrikePerDayCount, "Ki/Primal Strike (1/day)");
            s.AddToSummaryIf(c.EmpowerWeaponMagicCount, "Empower weapon: +0 magic (5 mins)");
            s.AddToSummaryIf(c.EmpowerWeaponSpiritCount, "Empower weapon: +0 spirit (5 mins)");
            s.AddToSummaryIf(c.EmpowerWeaponManticCount, "Empower weapon: +0 mantic (5 mins)");
            s.AddToSummaryIf(c.ExtraColoursForEmpowerments, "Extra colours for empowerment");
            s.AddToSummaryIf(c.ExtraAlignmentsForEmpowerments, "Extra alignments for empowerment");
            s.AddToSummaryIf(c.ScholarlyInterestPerDayCount, "Scholarly Interest (1/day)");
            s.AddToSummaryIf(c.KnowledgeOfArcanePerDayCount, "Knowledge of the Arcane (1/day)");
            s.AddToSummaryIf(c.MajorPrayerPerDayPowerbaseCount, "Major Prayer (powerbase) (1/day)");
            s.AddToSummaryIf(c.MajorPrayerPerDayPowerbaseSubjectCount, "Major Prayer (powerbase+subject) (1/day)");
            s.AddToSummaryIf(c.MinorPrayerPerDayPowerbaseCount, "Minor Prayer (powerbase) (1/day)");

            if (c.AdditionalLTMBlocks > 0)
            {
                var pts = c.AdditionalLTMBlocks * 6;
                s.Add($"Additional life to minus: +{pts} (5 per 6pts){(c.LTMKickInIsMagicOrSpirit ? ", kick-in Magic/Spirit ×2 cost" : "")}");
            }

            s.AddToSummaryIf((c.ElfInnateColour is not null), $"Lvl: {c.ElvenInnateLevel} {c.ElfInnateColour} Elven Innates");
            // Utilities
            if (c.ReadLanguages) s.Add("Read languages");
            if (c.DisarmTrapsAsScout) s.Add("Disarm traps (as scout)");
            s.AddToSummaryIf(c.PotionRecipesKnownCount, "Potion recipes known");
            if (c.Regeneration) s.Add("Regeneration (non-stacking)");
            if (c.ForearmParry) s.Add("Forearm Parry");

            return string.Join("\n", s);
        }
    }
}
