using labyItems.Models;
using labyItems.Pages.Configs;

namespace labyItems.Pages.Calculator.CalcNav;

public partial class ArmourNav : ContentPage
{
    private readonly List<CalcContribution> _contributions = new();
    private bool _categoryLocked = false;
    public event Action<CalcContribution>? ContributionAdded;

    private TaskCompletionSource<CalcResult?>? _tcs;

    public ArmourNav()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Called when the user taps the "Shield" button in the Armour tab.
    /// </summary>
    private async void OnShield(object sender, EventArgs e)
    {
        if (_categoryLocked) return;
        _categoryLocked = true;

        var cfgPage = new ShieldConfigPage();   // your shield-only config page
        await Navigation.PushAsync(cfgPage);

        var cfg = await cfgPage.Completion;     // CalcResult? with Summary + TotalIsp
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

    /// <summary>
    /// Called when the user taps the "Armour" button in the Armour tab.
    /// </summary>
    private async void OnArmour(object sender, EventArgs e)
    {
        if (_categoryLocked) return;
        _categoryLocked = true;

        var cfgPage = new ArmourConfigPage();   // your armour-only config page
        await Navigation.PushAsync(cfgPage);

        var cfg = await cfgPage.Completion;     // CalcResult? with Summary + TotalIsp
        _categoryLocked = false;
        if (cfg is null) return;

        _contributions.Add(new CalcContribution(
            Source: "Armour",
            Label: cfg.Summary,
            Isp: cfg.TotalIsp));

        // If you have a label in armour.xaml to show chosen armour, uncomment & rename as needed:
        // ArmourPickedLabel.IsVisible = true;
        // ArmourPickedLabel.Text = string.Join("\n", _contributions
        //     .Where(c => c.Source.Equals("Armour", StringComparison.OrdinalIgnoreCase))
        //     .Select(c => c.Label));

        UpdateTotal();
    }

    /// <summary>
    /// Optional: if this tab needs to return a combined result to its caller.
    /// </summary>
    private async void OnReturn(object sender, EventArgs e)
    {
        if (_tcs is null)
        {
            // This tab is probably being used directly in a TabbedPage and
            // not via GetResultAsync – nothing to return to.
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

    /// <summary>
    /// If the parent wants to navigate to this tab as a modal-like page
    /// and await a CalcResult.
    /// </summary>
    public async Task<CalcResult?> GetResultAsync(INavigation nav)
    {
        _tcs = new TaskCompletionSource<CalcResult?>();
        await nav.PushAsync(this);
        return await _tcs.Task;
    }

    // ------------ helpers for total / summary ------------

    private void UpdateTotal()
    {
        // Assuming armour.xaml has a Label named "TotalLabel".
        // If not, rename this to match your XAML name.
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
