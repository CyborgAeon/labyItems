using labyItems.Models;
using labyItems.Models.Enums;
using labyItems.Pages.Configs;

namespace labyItems.Pages.Calculator;

public partial class ArmourConfigPage : ConfigPageBase<ArmourConfig>
{
    public ArmourConfigPage()
    {
        InitializeComponent();
    }

    private void OnArmourChanged(object sender, EventArgs e)
    {
        if (BindingContext is not ArmourConfig cfg) return;
        
        var picker = sender as Picker;
        var index = picker?.SelectedIndex ?? -1;

        cfg.SelectedArmour = index switch
        {
            1 => ArmourKind.MagicalMasterCrafted,
            2 => ArmourKind.SpiritualMasterCrafted,
            3 => ArmourKind.ManticMasterCrafted,
            _ => ArmourKind.None
        };
    }
    
    protected override string BuildSummary(ArmourConfig cfg)
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
}
