using labyItems.Models;
using labyItems.Models.Enums;
using labyItems.Pages.Configs;

namespace labyItems.Pages;

public partial class ArmourConfigPage : ContentPage
{
    private readonly TaskCompletionSource<CalcResult?> _tcs = new();
    public Task<CalcResult?> Completion => _tcs.Task;

    public ArmourConfigPage()
    {
        InitializeComponent();
        BindingContext = new ArmourConfig(); // auto-recalculates as properties change
    }

    // Map picker index -> enum (and trigger recalculation via setter)
    private void OnArmourChanged(object sender, EventArgs e)
    {
        var cfg = (ArmourConfig)BindingContext;
        switch (ArmourPicker.SelectedIndex)
        {
            case 1: cfg.SelectedArmour = ArmourKind.MagicalMasterCrafted; break;
            case 2: cfg.SelectedArmour = ArmourKind.SpiritualMasterCrafted; break;
            case 3: cfg.SelectedArmour = ArmourKind.ManticMasterCrafted; break;
            default: cfg.SelectedArmour = ArmourKind.None; break;
        }
        // cfg.Recalculate(); // not needed—setters recalc automatically (see config class below)
    }

    private string BuildSummary(ArmourConfig cfg)
    {
        var kind = cfg.SelectedArmour switch
        {
            ArmourKind.MagicalMasterCrafted   => "Magical MC Armour",
            ArmourKind.SpiritualMasterCrafted => "Spiritual MC Armour",
            ArmourKind.ManticMasterCrafted    => "Mantic MC Armour",
            _                                 => "No Armour"
        };

        var tags = new List<string>();
        if (cfg.ACBase > 0) tags.Add($"AC {cfg.ACBase}");
        if (cfg.SelectedArmour == ArmourKind.MagicalMasterCrafted && cfg.MagicalColoursCount > 0)
            tags.Add($"+{2 * cfg.MagicalColoursCount} colours");
        if (cfg.SelectedArmour == ArmourKind.SpiritualMasterCrafted && cfg.SpiritualNonOpposite)
            tags.Add("+3 non-opposite");
        if (cfg.PAC > 0) tags.Add($"PAC {cfg.PAC}");
        if (cfg.DAC > 0) tags.Add($"DAC {cfg.DAC}");
        if (cfg.MAC > 0) tags.Add($"MAC {cfg.MAC}");
        if (cfg.SAC > 0) tags.Add($"SAC {cfg.SAC}");

        var tagText = tags.Count > 0 ? $" [{string.Join(", ", tags)}]" : "";
        return $"{kind}{tagText} → {cfg.Total} ISP";
    }

    private async void OnReturn(object sender, EventArgs e)
    {
        var cfg = (ArmourConfig)BindingContext;
        var res = new CalcResult
        {
            TotalIsp = cfg.Total,
            Summary = BuildSummary(cfg)
        };
        _tcs.TrySetResult(res);
        await Navigation.PopAsync();
    }
}
