using labyItems.Models;
using labyItems.Categories;

namespace labyItems.Pages.Calculator;

public partial class IspCalculator : TabbedPage
{

    public event Action<int>? TotalChanged;
    private readonly List<CalcContribution> _contributions = new();
    private TaskCompletionSource<CalcResult?>? _tcs;

    public IspCalculator(ItemTypeEnum category)
    {
        InitializeComponent();

        // Subscribe to tab events
        ArmourCategoryPage.ContributionAdded += c => { _contributions.Add(c); UpdateTotal(); };
        WeaponCategoryPage.ContributionAdded += c => { _contributions.Add(c); UpdateTotal(); };
        CharmCategoryPage.ContributionAdded  += c => { _contributions.Add(c); UpdateTotal(); };
        ConsumableCategoryPage.ContributionAdded += c => { _contributions.Add(c); UpdateTotal(); };
        LifeCategoryPage.ContributionAdded   += c => { _contributions.Add(c); UpdateTotal(); };
    }

    // private void OnSelectArmour(object s, CheckedChangedEventArgs e) { if (e.Value) SetActive(_armour); }
    // private void OnSelectWeapon(object s, CheckedChangedEventArgs e) { if (e.Value) SetActive(_weapon); }
    // private void OnSelectCharm(object s, CheckedChangedEventArgs e)  { if (e.Value) SetActive(_charm); }
    // private void OnSelectConsum(object s, CheckedChangedEventArgs e) { if (e.Value) SetActive(_consumable); }
    // private void OnSelectLife(object s, CheckedChangedEventArgs e)   { if (e.Value) SetActive(_life); }

    // private void SetActive(ICalculatorCategory cat)
    // {
    //     if (_categoryLocked) return;
    //     // _active = cat;
    //     UpdateTotal();
    // }

    private async void OnReturn(object sender, EventArgs e)
    {
        var result = new CalcResult
        {
            TotalIsp = ComputeTotal(),
            Summary  = BuildSummary()
        };
        // _categoryLocked = false;
        _tcs?.TrySetResult(result);
        await Navigation.PopAsync();
    }

    public async Task<CalcResult?> GetResultAsync(INavigation nav)
    {
        _tcs = new TaskCompletionSource<CalcResult?>();
        await nav.PushAsync(this);
        return await _tcs.Task;
    }

    private void UpdateTotal()
    {
        var total = ComputeTotal();
        TotalChanged?.Invoke(total);
    }

    private int ComputeTotal()
    {
        var sum = _contributions.Sum(c => c.Isp);
        return sum <= 0 ? 0 : sum;
    }

    private string BuildSummary()
    {
        var lines = _contributions.Select(c => c.Label).ToList();
        var total = ComputeTotal();
        if (total > 0) lines.Add($"Total ISP: {total}");
        return string.Join("\n", lines);
    }
}
