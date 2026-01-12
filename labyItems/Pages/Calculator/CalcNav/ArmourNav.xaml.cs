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

public partial class ArmourNav : ContentPage
{
    private readonly List<CalcContribution> _contributions = new();
    private bool _categoryLocked;
    private ShieldConfigPage? _shieldConfigPage;
    private ArmourConfigPage? _armourConfigPage;
    private const string ShieldContributionId = "Shield";
    private const string ArmourContributionId = "Armour";
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

        var cfgPage = _shieldConfigPage ??= new ShieldConfigPage();
        cfgPage.CalculatorContext = BindingContext as IspCalculator;
        cfgPage.ApplyBaseTotal(GetBaseIsp());
        var completion = cfgPage.Completion;
        await Navigation.PushAsync(cfgPage);

        var cfg = await completion;
        _categoryLocked = false;
        if (cfg is null) return;

        var added = new CalcContribution(
            Id: ShieldContributionId,
            Source: ShieldContributionId,
            Result: cfg,
            OnRemove: cfgPage.ResetConfig);

        AddOrReplaceContribution(added);
    }

    private async void OnArmour(object sender, EventArgs e)
    {
        if (_categoryLocked) return;
        _categoryLocked = true;

        var cfgPage = _armourConfigPage ??= new ArmourConfigPage();
        cfgPage.CalculatorContext = BindingContext as IspCalculator;
        cfgPage.ApplyBaseTotal(GetBaseIsp());
        var completion = cfgPage.Completion;
        await Navigation.PushAsync(cfgPage);

        var cfg = await completion;
        _categoryLocked = false;
        if (cfg is null) return;

        var added = new CalcContribution(
            Id: ArmourContributionId,
            Source: ArmourContributionId,
            Result: cfg,
            OnRemove: cfgPage.ResetConfig);

        AddOrReplaceContribution(added);
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
        var sum = _contributions.Sum(c => c.Result.TotalIsp);
        Total = sum <= 0 ? 0 : sum;
        return Total;
    }

    private string BuildSummary()
    {
        var lines = _contributions.Select(c => c.Result.Summary).ToList();
        if (Total > 0) lines.Add($"Total ISP: {Total}");
        return string.Join("\n", lines);
    }

    private void AddOrReplaceContribution(CalcContribution contribution)
    {
        var existing = _contributions.FirstOrDefault(c => c.Id == contribution.Id);
        if (existing != null)
            _contributions.Remove(existing);

        _contributions.Add(contribution);
        ContributionAdded?.Invoke(contribution);
        UpdateTotal();
    }

    private int GetBaseIsp() =>
        (BindingContext as IspCalculator)?.BaseTotal ?? 0;
}
