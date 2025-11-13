using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using labyItems.Models;
using labyItems.Pages.Configs;

namespace labyItems.Pages.Calculator.CalcNav;
    public partial class CharmNav : ContentPage
    {
        // Let the parent know when a contribution is added.
        public event Action<CalcContribution>? ContributionAdded;

        private readonly List<CalcContribution> _contributions = new();
        private bool _categoryLocked;

        private TaskCompletionSource<CalcResult?>? _tcs;

        public CharmNav()
        {
            InitializeComponent();
        }

        // ---------- UI event handlers for Charm actions ----------

        private async void OnCharmEarthPower(object sender, EventArgs e)
        {
            if (_categoryLocked) return;
            _categoryLocked = true;

            var cfgPage = new EvocationConfigPage();
            await Navigation.PushAsync(cfgPage);

            var cfg = await cfgPage.Completion;
            _categoryLocked = false;
            if (cfg is null) return;

            var added = new CalcContribution(
                Source: "Charm/EarthPower",
                Label: cfg.Summary,
                Isp: cfg.TotalIsp);

            _contributions.Add(added);
            ContributionAdded?.Invoke(added);

            EarthPowerCharmPickedLabel.IsVisible = true;
            EarthPowerCharmPickedLabel.Text = string.Join("\n", _contributions
                .Where(c => c.Source.StartsWith("Charm/", StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Label));

            UpdateTotal();
        }

        private async void OnCharmGeneral(object sender, EventArgs e)
        {
            if (_categoryLocked) return;
            _categoryLocked = true;

            var cfgPage = new GeneralConfigPage();
            await Navigation.PushAsync(cfgPage);

            var cfg = await cfgPage.Completion;
            _categoryLocked = false;
            if (cfg is null) return;

            var added = new CalcContribution(
                Source: "Charm/General",
                Label: cfg.Summary,
                Isp: cfg.TotalIsp);

            _contributions.Add(added);
            ContributionAdded?.Invoke(added);

            GeneralCharmPickedLabel.IsVisible = true;
            GeneralCharmPickedLabel.Text = string.Join("\n", _contributions
                .Where(c => c.Source.StartsWith("Charm/", StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Label));

            UpdateTotal();
        }

        private async void OnCharmMagic(object sender, EventArgs e)
        {
            if (_categoryLocked) return;
            _categoryLocked = true;

            var cfgPage = new SpellConfigPage();
            await Navigation.PushAsync(cfgPage);

            var cfg = await cfgPage.Completion;
            _categoryLocked = false;
            if (cfg is null) return;

            var added = new CalcContribution(
                Source: "Charm/Magic",
                Label: cfg.Summary,
                Isp: cfg.TotalIsp);

            _contributions.Add(added);
            ContributionAdded?.Invoke(added);

            MagicCharmPickedLabel.IsVisible = true;
            MagicCharmPickedLabel.Text = string.Join("\n", _contributions
                .Where(c => c.Source.StartsWith("Charm/", StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Label));

            UpdateTotal();
        }

        private async void OnCharmSpirit(object sender, EventArgs e)
        {
            if (_categoryLocked) return;
            _categoryLocked = true;

            var cfgPage = new MiracleConfigPage();
            await Navigation.PushAsync(cfgPage);

            var cfg = await cfgPage.Completion;
            _categoryLocked = false;
            if (cfg is null) return;

            var added = new CalcContribution(
                Source: "Charm/Spirit",
                Label: cfg.Summary,
                Isp: cfg.TotalIsp);

            _contributions.Add(added);
            ContributionAdded?.Invoke(added);

            SpiritCharmPickedLabel.IsVisible = true;
            SpiritCharmPickedLabel.Text = string.Join("\n", _contributions
                .Where(c => c.Source.StartsWith("Charm/", StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Label));

            UpdateTotal();
        }

        // ---------- Optional return pattern if you ever push this page standalone ----------
        // private async void OnReturn(object sender, EventArgs e)
        // {
        //     if (_tcs is null)
        //     {
        //         await Navigation.PopAsync();
        //         return;
        //     }

        //     var result = new CalcResult
        //     {
        //         TotalIsp = ComputeTotal(),
        //         Summary  = BuildSummary()
        //     };

        //     _tcs.TrySetResult(result);
        //     await Navigation.PopAsync();
        // }

        public async Task<CalcResult?> GetResultAsync(INavigation nav)
        {
            _tcs = new TaskCompletionSource<CalcResult?>();
            await nav.PushAsync(this);
            return await _tcs.Task;
        }

        // ---------- helpers ----------
        private void UpdateTotal()
        {
            TotalLabel.Text = $"Total: {ComputeTotal()} ISP";
        }

        private int ComputeTotal()
        {
            var sum = _contributions.Sum(c => c.Isp);
            return sum <= 0 ? 0 : sum;
        }

        private string BuildSummary()
        {
            var lines = new List<string>();
            foreach (var c in _contributions)
                lines.Add(c.Label);

            var total = ComputeTotal();
            if (total > 0)
                lines.Add($"Total ISP: {total}");

            return string.Join("\n", lines);
        }
    }