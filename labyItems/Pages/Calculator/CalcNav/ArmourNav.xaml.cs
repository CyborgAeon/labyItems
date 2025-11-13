using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using labyItems.Models;
using labyItems.Pages.Configs;

namespace labyItems.Pages.Calculator.CalcNav;

public partial class ArmourNav : ContentPage
{
    private readonly List<CalcContribution> _contributions = new();
    private bool _categoryLocked;
    public event Action<CalcContribution>? ContributionAdded;
public static readonly BindableProperty TotalProperty =
        BindableProperty.Create(
            nameof(Total),
            typeof(int),
            typeof(ArmourNav),
            0);

    public int Total
    {
        get => (int)GetValue(TotalProperty);
        set => SetValue(TotalProperty, value);
    }

        public static readonly BindableProperty ReturnToFormCommandProperty =
    BindableProperty.Create(
        nameof(ReturnToFormCommand),
        typeof(ICommand),
        typeof(ArmourNav),
        null);

public ICommand? ReturnToFormCommand
{
    get => (ICommand?)GetValue(ReturnToFormCommandProperty);
    set => SetValue(ReturnToFormCommandProperty, value);
}
    public ArmourNav()
    {
        InitializeComponent();
        UpdateTotal();
    }

    private async void OnShield(object sender, EventArgs e)
    {
        if (_categoryLocked) return;
        _categoryLocked = true;

        var cfgPage = new ShieldConfigPage();
        await Navigation.PushAsync(cfgPage);

        var cfg = await cfgPage.Completion;
        _categoryLocked = false;
        if (cfg is null) return;

        var added = new CalcContribution(
            Source: "Shield",
            Label: cfg.Summary,
            Isp: cfg.TotalIsp);

        _contributions.Add(added);
        ContributionAdded?.Invoke(added);

        UpdateTotal();
    }

    private async void OnArmour(object sender, EventArgs e)
    {
        if (_categoryLocked) return;
        _categoryLocked = true;

        var cfgPage = new ArmourConfigPage();
        await Navigation.PushAsync(cfgPage);

        var cfg = await cfgPage.Completion;
        _categoryLocked = false;
        if (cfg is null) return;

        var added = new CalcContribution(
            Source: "Armour",
            Label: cfg.Summary,
            Isp: cfg.TotalIsp);

        _contributions.Add(added);
        ContributionAdded?.Invoke(added);

        UpdateTotal();
    }
    private async void OnReturnWrapper(object sender, EventArgs e) => await OnReturn();

    private async Task OnReturn()
    {
        var result = new CalcResult
        {
            TotalIsp = UpdateTotal(),
            Summary  = BuildSummary()
        };

        await Navigation.PopAsync();
    }

    private int UpdateTotal() => ComputeTotal();
    private int ComputeTotal()
    {
        var sum = _contributions.Sum(c => c.Isp);
        Total = sum <= 0 ? 0 : sum;
        return Total;
    }

    private string BuildSummary()
    {
        var lines = _contributions.Select(c => c.Label).ToList();
        if (Total > 0) lines.Add($"Total ISP: {Total}");
        return string.Join("\n", lines);
    }
}
