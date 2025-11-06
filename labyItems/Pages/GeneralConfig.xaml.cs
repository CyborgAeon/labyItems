using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using labyItems.Pages.Configs;
using labyItems.Models;

namespace labyItems.Pages
{
    public partial class GeneralConfigPage : ContentPage
    {
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

        private static string BuildSummary(GeneralConfig c)
        {
            var s = new List<string>();
            // Resistance
            AddIf(s, c.ResistanceAllLevels, "+1 Resistance (all)", "x");
            AddIf(s, c.ResistanceMagicOrSpiritLevels, "+1 Resistance (magic/spirit)", "x");
            AddIf(s, c.ResistanceEpOrNeuroLevels, "+1 Resistance (EP/neuronics)", "x");
            AddIf(s, c.ResistanceAllPlus2Count, "+2 Resistance (all) ×3 cost", "x");
            AddIf(s, c.ResistanceMagOrSpiritPlus2Count, "+2 Resistance (magic/spirit) ×3 cost", "x");
            AddIf(s, c.ResistanceEpOrNeuroPlus2Count, "+2 Resistance (EP/neuronics) ×3 cost", "x");

            // Casting
            AddIf(s, c.CastingLevelsAll, "Casting levels (all)", "+");
            AddIf(s, c.CastingLevelsOneColour, "Casting levels (one colour)", "+");

            // Strength
            AddIf(s, c.StrengthPlus1NonStackingCount, "+1 Strength (non-stacking)", "x");
            AddIf(s, c.StrengthPlus1StackingTo2Count, "+1 Strength (stacking to +2)", "x");
            AddIf(s, c.StrengthPlus2NonStackingCount, "+2 Strength (non-stacking)", "x");

            // Rages
            AddIf(s, c.ColdRage25PerDayCount, "25% Cold Rage (1/day)", "x");
            AddIf(s, c.BerserkRage50PerDayCount, "50% Berserk Rage (1/day)", "x");
            AddIf(s, c.ColdRage25VsOneGroupAlwaysCount, "25% Cold Rage vs one group (always)", "x");

            // Repel / Attract
            AddIf(s, c.RepelAttractOneTypePerDayCount, "Repel/Attract one Type (1/day)", "x");
            AddIf(s, c.RepelAttractOneGroupPerDayCount, "Repel/Attract one Group (1/day)", "x");
            AddIf(s, c.RepelLifePerDayCount, "Repel Life (1/day)", "x");

            // Misc
            AddIf(s, c.DisciplinePerDayCount, "Discipline (1/day)", "x");
            AddIf(s, c.WardPact8LevelsCount, "Ward Pact (8 levels)", "x");
            AddIf(s, c.KiOrPrimalStrikePerDayCount, "Ki/Primal Strike (1/day)", "x");

            // Weapon empowerments
            AddIf(s, c.EmpowerWeaponMagicCount, "Empower weapon: +0 magic (5 mins)", "x");
            AddIf(s, c.EmpowerWeaponSpiritCount, "Empower weapon: +0 spirit (5 mins)", "x");
            AddIf(s, c.EmpowerWeaponManticCount, "Empower weapon: +0 mantic (5 mins)", "x");
            AddIf(s, c.ExtraColoursForEmpowerments, "Extra colours for empowerment", "+");
            AddIf(s, c.ExtraAlignmentsForEmpowerments, "Extra alignments for empowerment", "+");

            // Knowledge / Prayers
            AddIf(s, c.ScholarlyInterestPerDayCount, "Scholarly Interest (1/day)", "x");
            AddIf(s, c.KnowledgeOfArcanePerDayCount, "Knowledge of the Arcane (1/day)", "x");
            AddIf(s, c.MajorPrayerPerDayPowerbaseCount, "Major Prayer (powerbase) (1/day)", "x");
            AddIf(s, c.MajorPrayerPerDayPowerbaseSubjectCount, "Major Prayer (powerbase+subject) (1/day)", "x");
            AddIf(s, c.MinorPrayerPerDayPowerbaseCount, "Minor Prayer (powerbase) (1/day)", "x");

            // Additional life
            if (c.AdditionalLTMBlocks > 0)
            {
                var pts = c.AdditionalLTMBlocks * 6;
                s.Add($"Additional life to minus: +{pts} (5 per 6pts){(c.LTMKickInIsMagicOrSpirit ? ", kick-in Magic/Spirit ×2 cost" : "")}");
            }

            // Elf/Drave innates
            AddIf(s, c.ElfDraveInnateLevel4Count, "Elf/Drave 4th level innates", "x");
            AddIf(s, c.ElfDraveInnateLevel6Count, "Elf/Drave 6th level innates", "x");
            AddIf(s, c.ElfDraveInnateLevel8Count, "Elf/Drave 8th level innates", "x");

            // Utilities
            if (c.ReadLanguages) s.Add("Read languages");
            if (c.DisarmTrapsAsScout) s.Add("Disarm traps (as scout)");
            AddIf(s, c.PotionRecipesKnownCount, "Potion recipes known", "x");
            if (c.Regeneration) s.Add("Regeneration (non-stacking)");
            if (c.ForearmParry) s.Add("Forearm Parry");

            return string.Join("\n", s);
        }

        private static void AddIf(List<string> s, int count, string label, string mode)
        {
            if (count <= 0) return;
            s.Add(mode == "x" ? $"{label}: x{count}" : $"{label}: +{count}");
        }
    }
}
