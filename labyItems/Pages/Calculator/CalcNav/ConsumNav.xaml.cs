using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using labyItems.Models;
using labyItems.Pages.Configs;

using System.Windows.Input;

namespace labyItems.Pages.Calculator.CalcNav;
    public partial class ConsumNav : ContentPage
    {
        // Let the parent know when a contribution is added.
        public event Action<CalcContribution>? ContributionAdded;

        private readonly List<CalcContribution> _contributions = new();
        private bool _categoryLocked;

public static readonly BindableProperty TotalProperty =
        BindableProperty.Create(
            nameof(Total),
            typeof(int),
            typeof(ConsumNav),
            0);
            
        public static readonly BindableProperty ReturnToFormCommandProperty =
    BindableProperty.Create(
        nameof(ReturnToFormCommand),
        typeof(ICommand),
        typeof(ConsumNav),
        null);

public ICommand? ReturnToFormCommand
{
    get => (ICommand?)GetValue(ReturnToFormCommandProperty);
    set => SetValue(ReturnToFormCommandProperty, value);
}
    public int Total
    {
        get => (int)GetValue(TotalProperty);
        set => SetValue(TotalProperty, value);
    }
        public ConsumNav()
        {
            InitializeComponent();
            ComputeTotal();
        }
private async Task OnReturnCommand(){
            var result = new CalcResult
            {
                TotalIsp = ComputeTotal(),
                Summary  = BuildSummary()
            };

            await Navigation.PopAsync();
}
private async void OnReturn(object sender, EventArgs e) => OnReturnCommand();

        private async void OnConsumableMagicScroll(object sender, EventArgs e)
        {
            if (_categoryLocked) return;
            _categoryLocked = true;

            var cfgPage = new ConsumableConfigPage(ConsumableType.MagicalScroll);
            await Navigation.PushAsync(cfgPage);

            var cfg = await cfgPage.Completion;
            _categoryLocked = false;
            if (cfg is null) return;

            var added = new CalcContribution(
                Source: "Consumable/Magic Scroll",
                Label: cfg.Summary,
                Isp:   cfg.TotalIsp);

            _contributions.Add(added);
            ContributionAdded?.Invoke(added);

            ConsumableMagicPickedLabel.IsVisible = true;
            ConsumableMagicPickedLabel.Text = string.Join("\n", _contributions
                .Where(c => c.Source.Equals("Consumable/Magic Scroll", StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Label));

            UpdateTotal();
        }

        private async void OnConsumableSpiritScroll(object sender, EventArgs e)
        {
            if (_categoryLocked) return;
            _categoryLocked = true;

            var cfgPage = new ConsumableConfigPage(ConsumableType.SpiritualScroll);
            await Navigation.PushAsync(cfgPage);

            var cfg = await cfgPage.Completion;
            _categoryLocked = false;
            if (cfg is null) return;

            var added = new CalcContribution(
                Source: "Consumable/Spirit Scroll",
                Label: cfg.Summary,
                Isp:   cfg.TotalIsp);

            _contributions.Add(added);
            ContributionAdded?.Invoke(added);

            ConsumableSpiritPickedLabel.IsVisible = true;
            ConsumableSpiritPickedLabel.Text = string.Join("\n", _contributions
                .Where(c => c.Source.Equals("Consumable/Spirit Scroll", StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Label));

            UpdateTotal();
        }

        private async void OnConsumableNeuroCrystal(object sender, EventArgs e)
        {
            if (_categoryLocked) return;
            _categoryLocked = true;

            // Preset with focussing crystals if you want, here set to 0 as in your snippet.
            var cfgPage = new ConsumableConfigPage(ConsumableType.NeuronicShard, initialFocussingCrystals: 0);
            await Navigation.PushAsync(cfgPage);

            var cfg = await cfgPage.Completion;
            _categoryLocked = false;
            if (cfg is null) return;

            var added = new CalcContribution(
                Source: "Consumable/Neuro Crystal",
                Label: cfg.Summary,
                Isp:   cfg.TotalIsp);

            _contributions.Add(added);
            ContributionAdded?.Invoke(added);

            ConsumableNeuroPickedLabel.IsVisible = true;
            ConsumableNeuroPickedLabel.Text = string.Join("\n", _contributions
                .Where(c => c.Source.Equals("Consumable/Neuro Crystal", StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Label));

            UpdateTotal();
        }

        private async void OnConsumableEPTalisman(object sender, EventArgs e)
        {
            if (_categoryLocked) return;
            _categoryLocked = true;

            var cfgPage = new ConsumableConfigPage(ConsumableType.DruidicTalisman);
            await Navigation.PushAsync(cfgPage);

            var cfg = await cfgPage.Completion;
            _categoryLocked = false;
            if (cfg is null) return;

            var added = new CalcContribution(
                Source: "Consumable/EP Talisman",
                Label: cfg.Summary,
                Isp:   cfg.TotalIsp);

            _contributions.Add(added);
            ContributionAdded?.Invoke(added);

            ConsumableEPPickedLabel.IsVisible = true;
            ConsumableEPPickedLabel.Text = string.Join("\n", _contributions
                .Where(c => c.Source.Equals("Consumable/EP Talisman", StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Label));

            UpdateTotal();
        }

        private async void OnConsumableGeneric(object sender, EventArgs e)
        {
            if (_categoryLocked) return;
            _categoryLocked = true;

            var cfgPage = new ConsumableConfigPage();
            await Navigation.PushAsync(cfgPage);

            var cfg = await cfgPage.Completion;
            _categoryLocked = false;
            if (cfg is null) return;

            var added = new CalcContribution(
                Source: "Consumable",
                Label: cfg.Summary,
                Isp:   cfg.TotalIsp);

            _contributions.Add(added);
            ContributionAdded?.Invoke(added);

            // If you want an aggregated per-consumable summary label, put it here.
            UpdateTotal();
        }

        private void UpdateTotal()
        {
            TotalLabel.Text = $"Total: {ComputeTotal()} ISP";
        }

        private int ComputeTotal()
        {
            var sum = _contributions.Sum(c => c.Isp);
            Total = sum <= 0 ? 0 : sum;
            return Total;
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
