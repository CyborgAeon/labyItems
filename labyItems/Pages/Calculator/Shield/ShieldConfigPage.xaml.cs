using labyItems.Models;
using labyItems.Models.Enums;
using labyItems.Pages.Configs;

namespace labyItems.Pages.Calculator;

public partial class ShieldConfigPage : ConfigPageBase<ShieldConfig>
{
    public ShieldConfigPage()
    {
        InitializeComponent();
    }

    protected override CalcResult BuildResult(ShieldConfig cfg)
    {
        var kind = cfg.SelectedShield switch
        {
            ShieldType.Magical => "Magical Shield",
            ShieldType.Spiritual => "Spiritual Shield",
            ShieldType.Mantic => "Mantic Shield",
            _ => "No Shield",
        };

        var details = new Dictionary<string, object?>();
        if (cfg.SelectedShield == ShieldType.Magical && cfg.ShieldColourCount > 0)
            details["shieldColours"] = cfg.ShieldColourCount;
        if (cfg.SelectedShield == ShieldType.Spiritual)
            details["spiritualNonOpposite"] = true;
        if (cfg.ShowShieldAlignments && cfg.ShieldAlignmentCount > 0)
            details["shieldAlignments"] = cfg.ShieldAlignmentCount;
        if (cfg.PAC > 0)
            details["PAC"] = cfg.PAC;
        if (cfg.DAC > 0)
            details["DAC"] = cfg.DAC;
        if (cfg.MAC > 0)
        {
            details["MAC"] = cfg.MAC;
            if (cfg.MacColoursCount > 0)
                details["macColours"] = cfg.MacColoursCount;
        }
        if (cfg.SAC > 0)
        {
            details["SAC"] = cfg.SAC;
            if (cfg.SacAlignmentCount > 0)
                details["sacAlignment"] = cfg.SacAlignmentCount;
        }

        return new CalcResult
        {
            AbilityType = "Shield",
            AbilityName = kind,
            TotalIsp = cfg.Total,
            Summary = cfg.Breakdown,
            Details = details,
        };
    }

    // ---- Event handlers to wire from XAML ----

    private void OnCalculate(object sender, EventArgs e)
    {
        _ = Config.Total;
    }

    private void OnShieldChanged(object sender, EventArgs e)
    {
        var cfg = (ShieldConfig)BindingContext;
        _ = cfg.Total;
    }

    private void OnMagicalColoursChanged(object sender, TextChangedEventArgs e)
    {
        var cfg = (ShieldConfig)BindingContext;
        cfg.ShieldColourCount = Math.Max(0, TryParseInt(e.NewTextValue));
        _ = cfg.Total;
    }

    private static int TryParseInt(string? s) => int.TryParse(s, out var v) ? v : 0;
}
