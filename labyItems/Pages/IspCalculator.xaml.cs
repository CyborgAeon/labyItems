using labyItems.Models;
using labyItems.Categories;
namespace labyItems.Pages;

public partial class IspCalculator : ContentPage
{
    public sealed record CalcContribution(string Source, string Label, int Isp);
    private readonly ArmourShieldCategory _armour = new();
    private readonly WeaponCategory _weapon = new();
    private readonly CharmCategory _charm = new();
    private readonly ConsumableCategory _consumable = new();

    private ICalculatorCategory _active;
    private bool _categoryLocked = false;

    public IspCalculator(ItemTypeEnum category)
    {
        InitializeComponent();
        RbWeapon.CheckedChanged += (_, e) =>
        {
            WeaponCategoryPage.IsVisible = RbWeapon.IsChecked;
            CharmCategoryPage.IsVisible = false;
            ArmourShieldCategoryPage.IsVisible = false;
            // ConsumableCategoryPage.IsVisible = false;
        };
        RbArmour.CheckedChanged += (_, e) =>
        {
            ArmourShieldCategoryPage.IsVisible = RbArmour.IsChecked;
            WeaponCategoryPage.IsVisible = false;
            CharmCategoryPage.IsVisible = false;
            // ConsumableCategoryPage.IsVisible = false;
        };
        RbCharm.CheckedChanged += (_, e) =>
        {
            ArmourShieldCategoryPage.IsVisible = false;
            WeaponCategoryPage.IsVisible = false;
            CharmCategoryPage.IsVisible = RbCharm.IsChecked;
            // ConsumableCategoryPage.IsVisible = false;
        };
        RbConsumable.CheckedChanged += (_, e) =>
        {
            ArmourShieldCategoryPage.IsVisible = false;
            WeaponCategoryPage.IsVisible = false;
            CharmCategoryPage.IsVisible = false;
            // ConsumableCategoryPage.IsVisible = false;
        };
    }

    private void OnSelectArmour(object s, CheckedChangedEventArgs e) { if (e.Value) SetActive(_armour); }
    private void OnSelectWeapon(object s, CheckedChangedEventArgs e) { if (e.Value) SetActive(_weapon); }
    private void OnSelectCharm(object s, CheckedChangedEventArgs e) { if (e.Value) RbCharm.IsChecked = true; SetActive(_charm); }
    private void OnSelectConsum(object s, CheckedChangedEventArgs e) { if (e.Value) SetActive(_consumable); }

    private void SetActive(ICalculatorCategory cat)
    {
        if (_categoryLocked) return;
        _active = cat;
        UpdateTotal();
    }

    // Return result to parent page
    private async void OnReturn(object sender, EventArgs e)
    {
        var result = new CalcResult
        {
            TotalIsp = ComputeTotal(),
            Summary = BuildSummary()
        };
        _categoryLocked = false;
        _tcs?.TrySetResult(result);
        await Navigation.PopAsync();
    }

    private readonly List<CalcContribution> _contributions = new();
private async void OnWeapon(object sender, EventArgs e)
{
    _categoryLocked = true;

    var cfgPage = new WeaponConfigPage();
    await Navigation.PushAsync(cfgPage);

    var cfg = await cfgPage.Completion;
    _categoryLocked = false;
    if (cfg is null) return;

    _contributions.Add(new CalcContribution(
        Source: "Weapon",
        Label: cfg.Summary,
        Isp: cfg.TotalIsp));

    // Optional category summary label:
    WeaponPickedLabel.IsVisible = true;
    WeaponPickedLabel.Text = string.Join("\n", _contributions
        .Where(c => c.Source.Equals("Weapon", StringComparison.OrdinalIgnoreCase))
        .Select(c => c.Label));

    UpdateTotal();
}

    private async void OnCharmEarthPower(object sender, EventArgs e)
    {
        _categoryLocked = true;
        var cfgPage = new EvocationConfigPage();
        await Navigation.PushAsync(cfgPage);
        var cfg = await cfgPage.Completion;
        if (cfg == null) { _categoryLocked = false; return; }

        _contributions.Add(new CalcContribution(
            Source: "Charm/EarthPower",
            Label: cfg.Summary,
            Isp: cfg.TotalIsp));

        EarthPowerCharmPickedLabel.IsVisible = true;
        EarthPowerCharmPickedLabel.Text = string.Join("\n", _contributions
            .Where(c => c.Source.StartsWith("Charm/", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Label));

        UpdateTotal();
    }
    private async void OnCharmGeneral(object sender, EventArgs e)
    {
        _categoryLocked = true;
        var cfgPage = new GeneralConfigPage();               // new stub page below
        await Navigation.PushAsync(cfgPage);
        var cfg = await cfgPage.Completion;                 // waits for Return
        if (cfg is null) return;

        // 2) Add to calculator contributions
        _contributions.Add(new CalcContribution(
            Source: "Charm/General",
            Label: cfg.Summary,
            Isp: cfg.TotalIsp));

        GeneralCharmPickedLabel.IsVisible = true;
        GeneralCharmPickedLabel.Text = string.Join("\n", _contributions
            .Where(c => c.Source.StartsWith("Charm/", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Label));

        UpdateTotal();
    }
    private async void OnShield(object sender, EventArgs e)
    {
        _categoryLocked = true;

        var cfgPage = new ShieldConfigPage();   // your shield-only page
        await Navigation.PushAsync(cfgPage);

        var cfg = await cfgPage.Completion;     // CalcResult? with Summary + TotalIsp
        _categoryLocked = false;
        if (cfg is null) return;

        _contributions.Add(new CalcContribution(
            Source: "Shield",
            Label: cfg.Summary,
            Isp: cfg.TotalIsp));

        // If you keep a per-category label, uncomment and point to it:
        // ShieldPickedLabel.IsVisible = true;
        // ShieldPickedLabel.Text = string.Join("\n", _contributions
        //     .Where(c => c.Source.Equals("Shield", StringComparison.OrdinalIgnoreCase))
        //     .Select(c => c.Label));

        UpdateTotal();
    }
    private async void OnArmour(object sender, EventArgs e)
    {
        _categoryLocked = true;

        var cfgPage = new ArmourConfigPage();   // your armour-only page
        await Navigation.PushAsync(cfgPage);

        var cfg = await cfgPage.Completion;     // CalcResult? with Summary + TotalIsp
        _categoryLocked = false;
        if (cfg is null) return;

        _contributions.Add(new CalcContribution(
            Source: "Armour",
            Label: cfg.Summary,
            Isp: cfg.TotalIsp));

        // If you keep a per-category label, uncomment and point to it:
        // ArmourPickedLabel.IsVisible = true;
        // ArmourPickedLabel.Text = string.Join("\n", _contributions
        //     .Where(c => c.Source.Equals("Armour", StringComparison.OrdinalIgnoreCase))
        //     .Select(c => c.Label));

        UpdateTotal();
    }
    private async void OnCharmMagic(object sender, EventArgs e)
    {
        _categoryLocked = true;
        var cfgPage = new SpellConfigPage();               // new stub page below
        await Navigation.PushAsync(cfgPage);
        var cfg = await cfgPage.Completion;                 // waits for Return
        if (cfg is null) return;

        // 2) Add to calculator contributions
        _contributions.Add(new CalcContribution(
            Source: "Charm/Magic",
            Label: cfg.Summary,
            Isp: cfg.TotalIsp));

        MagicCharmPickedLabel.IsVisible = true;
        MagicCharmPickedLabel.Text = string.Join("\n", _contributions
            .Where(c => c.Source.StartsWith("Charm/", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Label));

        UpdateTotal();
    }

    public async Task<CalcResult?> GetResultAsync(INavigation nav)
    {
        _tcs = new TaskCompletionSource<CalcResult?>();
        await nav.PushAsync(this);
        return await _tcs.Task;
    }

    private TaskCompletionSource<CalcResult?>? _tcs;
    private void UpdateTotal()
    {
        TotalLabel.Text = $"Total: {ComputeTotal()} ISP";
    }

    private int ComputeTotal()
    {
        var sum = _contributions.Sum(c => c.Isp);
        if (sum <= 0) return 0;
        return sum;
    }
    private string BuildSummary()
    {
        var lines = new List<string>();
        foreach (var c in _contributions)
            lines.Add(c.Label);

        var total = ComputeTotal();
        if (total > 0) lines.Add($"Total ISP: {total}");
        return string.Join("\n", lines);
    }
}