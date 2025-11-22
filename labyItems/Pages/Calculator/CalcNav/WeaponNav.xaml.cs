using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using labyItems.Models;
using labyItems.Pages.Calculator;
using labyItems.Pages.Configs;


namespace labyItems.Pages.Calculator.CalcNav;
    public partial class WeaponNav : ContentPage
    {
        public event Action<CalcContribution>? ContributionAdded;

        private readonly List<CalcContribution> _contributions = new();
        private bool _categoryLocked;
 public static readonly BindableProperty TotalProperty =
        BindableProperty.Create(
            nameof(Total),
            typeof(int),
            typeof(WeaponNav),
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
            cfgPage.CalculatorContext = BindingContext as IspCalculator;
            cfgPage.ApplyBaseTotal(GetBaseIsp());
            await Navigation.PushAsync(cfgPage);

            var cfg = await cfgPage.Completion;
            _categoryLocked = false;
            if (cfg is null) return;

            var added = new CalcContribution(
                Id: Guid.NewGuid().ToString(),
                Source: "Weapon",
                Result: cfg,
                OnRemove: cfgPage.ResetConfig);

            _contributions.Add(added);
            ContributionAdded?.Invoke(added);

            WeaponPickedLabel.IsVisible = true;
            WeaponPickedLabel.Text = string.Join("\n",
                _contributions
                    .Where(c => c.Source.Equals("Weapon", StringComparison.OrdinalIgnoreCase))
                    .Select(c => c.Result.Summary));

            UpdateTotal();
        }

        public static readonly BindableProperty ReturnToFormCommandProperty =
    BindableProperty.Create(
        nameof(ReturnToFormCommand),
        typeof(ICommand),
        typeof(WeaponNav),
        null);

public ICommand? ReturnToFormCommand
{
    get => (ICommand?)GetValue(ReturnToFormCommandProperty);
    set => SetValue(ReturnToFormCommandProperty, value);
}

        private int UpdateTotal() => ComputeTotal();
        private int ComputeTotal()
        {
            var sum = _contributions.Sum(c => c.Result.TotalIsp);
            Total = sum <= 0 ? 0 : sum;
            return Total;
        }

        private string BuildSummary()
        {
            var lines = _contributions.Select(c => c.Result.Summary).ToList();
            var total = ComputeTotal();
            if (total > 0) lines.Add($"Total ISP: {total}");
            return string.Join("\n", lines);
        }

        private int GetBaseIsp() =>
            (BindingContext as IspCalculator)?.BaseTotal ?? 0;
    }
