using labyItems.Models;
using labyItems.Pages.Configs;

namespace labyItems.Pages.Calculator.CalcNav;

public partial class ArmourNav : ContentPage
{
    private readonly List<CalcContribution> _contributions = new();
    private bool _categoryLocked = false;
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
    private TaskCompletionSource<CalcResult?>? _tcs;

    public ArmourNav()
    {
        InitializeComponent();
        ComputeTotal();
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
    }

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
