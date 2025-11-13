using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using labyItems.Models;
using labyItems.Pages.Configs;

namespace labyItems.Pages.Calculator.CalcNav;
    public partial class WeaponNav : ContentPage
    {
        public event Action<CalcContribution>? ContributionAdded;

        private readonly List<CalcContribution> _contributions = new();
        private bool _categoryLocked;
        private TaskCompletionSource<CalcResult?>? _tcs;
 public static readonly BindableProperty TotalProperty =
        BindableProperty.Create(
            nameof(Total),
            typeof(int),
            typeof(CharmNav),
            0);

    public int Total
    {
        get => (int)GetValue(TotalProperty);
        set => SetValue(TotalProperty, value);
    }
        public WeaponNav()
        {
            InitializeComponent();
            ComputeTotal();
        }

        private async void OnWeapon(object sender, EventArgs e)
        {
            if (_categoryLocked) return;
            _categoryLocked = true;

            var cfgPage = new WeaponConfigPage();
            await Navigation.PushAsync(cfgPage);

            var cfg = await cfgPage.Completion;
            _categoryLocked = false;
            if (cfg is null) return;

            var added = new CalcContribution(
                Source: "Weapon",
                Label: cfg.Summary,
                Isp:   cfg.TotalIsp);

            _contributions.Add(added);
            ContributionAdded?.Invoke(added);

            WeaponPickedLabel.IsVisible = true;
            WeaponPickedLabel.Text = string.Join("\n",
                _contributions
                    .Where(c => c.Source.Equals("Weapon", StringComparison.OrdinalIgnoreCase))
                    .Select(c => c.Label));

            UpdateTotal();
        }

        // Optional: allow pushing this tab standalone and returning a result.
        private async void OnReturn(object sender, EventArgs e)
        {
            if (_tcs is null)
            {
                await Navigation.PopAsync();
                return;
            }

            var result = new CalcResult
            {
                TotalIsp = ComputeTotal(),
                Summary  = BuildSummary()
            };

            _tcs.TrySetResult(result);
            await Navigation.PopAsync();
        }

        public async Task<CalcResult?> GetResultAsync(INavigation nav)
        {
            _tcs = new TaskCompletionSource<CalcResult?>();
            await nav.PushAsync(this);
            return await _tcs.Task;
        }

        private void UpdateTotal() => ComputeTotal();
        private int ComputeTotal()
        {
            var sum = _contributions.Sum(c => c.Isp);
            Total = sum <= 0 ? 0 : sum;
            return Total;
        }

        private string BuildSummary()
        {
            var lines = _contributions.Select(c => c.Label).ToList();
            var total = ComputeTotal();
            if (total > 0) lines.Add($"Total ISP: {total}");
            return string.Join("\n", lines);
        }
    }