using labyItems.Models;
using labyItems.Pages.Configs;
using labyItems.Models.DTOs;
using Microsoft.Maui.Controls;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace labyItems.Pages.Configs
{
    public partial class MiracleConfigPage : ContentPage
    {
        private readonly TaskCompletionSource<CalcResult?> _tcs = new();
        public Task<CalcResult?> Completion => _tcs.Task;

        public MiracleConfigPage()
        {
            InitializeComponent();
            BindingContext = new MiracleConfig();
        }

        private string BuildSummary(MiracleConfig cfg)
        {
            // Compact tags reflecting the new model
            var tags = new List<string>();

            if (cfg.BasicPerDay > 0)     tags.Add($"Basic x{cfg.BasicPerDay}");
            if (cfg.AdvancedPerDay > 0)  tags.Add($"Advanced x{cfg.AdvancedPerDay}");
            if (cfg.InnateIsMantic)      tags.Add("Innates mantic ×4");

            if (cfg.GeneralSpiritStore > 0) tags.Add($"General store {cfg.GeneralSpiritStore} (4×)");
            if (cfg.SphereSpiritStore  > 0) tags.Add($"Sphere store {cfg.SphereSpiritStore} (3×)");
            if (cfg.SpiritStoreRegenerates && (cfg.GeneralSpiritStore > 0 || cfg.SphereSpiritStore > 0))
                tags.Add("Store regenerates +25");

            // Base list flags (only one should apply based on IsAdvanced)
            if (cfg.IsAdvanced == false && cfg.AddBasicToList)    tags.Add("+Basic");
            if (cfg.IsAdvanced == true  && cfg.AddAdvancedToList) tags.Add("+Advanced");

            if (cfg.AddWithPrep30) tags.Add("30s prep (+Power/2)");

            // Mantic conversions
            if (cfg.TurnBasicUpTo5thMantic      > 0) tags.Add($"Basic→≤5th mantic ×{cfg.TurnBasicUpTo5thMantic} (40×)");
            if (cfg.TurnBasicMantic             > 0) tags.Add($"Basic→mantic ×{cfg.TurnBasicMantic} (50×)");
            if (cfg.TurnAdvancedUpTo6thMantic   > 0) tags.Add($"Adv→≤6th mantic ×{cfg.TurnAdvancedUpTo6thMantic} (60×)");
            if (cfg.TurnAdvancedAbove6thMantic  > 0) tags.Add($"Adv→>6th mantic ×{cfg.TurnAdvancedAbove6thMantic} (80×)");

            if (cfg.IsTeachingScroll) tags.Add($"Teaching scroll (3×Power={3 * cfg.Power})");
            if (cfg.TrueBeliever > 0) tags.Add($"True believer ×{cfg.TrueBeliever} (16×)");

            var name = string.IsNullOrWhiteSpace(cfg.MiracleName) ? "Miracle" : cfg.MiracleName;
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

            var res = new CalcResult
            {
                TotalIsp = cfg.Total,
                Summary  = BuildSummary(cfg)
            };

            _tcs.TrySetResult(res);
            await Navigation.PopAsync();
        }
    }
}
