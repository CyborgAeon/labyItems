using System.Collections.Generic;
using labyItems.Models;
using labyItems.Models.Enums;
using labyItems.Pages.Configs;

namespace labyItems.Pages.Calculator;

public partial class ArmourConfigPage : ConfigPageBase<ArmourConfig>
{
    public ArmourConfigPage()
    {
        InitializeComponent();
        PacSlider.ItemsSource = BuildTableDictionary(ArmourConfig.PacTable);
        DacSlider.ItemsSource = BuildTableDictionary(ArmourConfig.DacTable);
        MacSlider.ItemsSource = BuildTableDictionary(ArmourConfig.MacTable);
        SacSlider.ItemsSource = BuildTableDictionary(ArmourConfig.SacTable);
    }

    private void OnArmourChanged(object sender, EventArgs e)
    {
        if (BindingContext is not ArmourConfig cfg) return;
        
        var picker = sender as Picker;
        var index = picker?.SelectedIndex ?? -1;

        cfg.SelectedArmour = index switch
        {
            1 => ArmourKind.Magical,
            2 => ArmourKind.Spiritual,
            3 => ArmourKind.Mantic,
            _ => ArmourKind.None
        };
    }

    protected override CalcResult BuildResult(ArmourConfig cfg)
    {
        var kind = cfg.SelectedArmour switch
        {
            ArmourKind.Magical   => "Magical MC Armour",
            ArmourKind.Spiritual => "Spiritual MC Armour",
            ArmourKind.Mantic    => "Mantic MC Armour",
            _                    => "No Armour"
        };

        var details = new Dictionary<string, object?>();
        if (cfg.ACBase > 0) details["AC"] = cfg.ACBase;
        if (cfg.SelectedArmour == ArmourKind.Magical && cfg.MagicalColoursCount > 0)
            details["magicalColours"] = cfg.MagicalColoursCount;
        if (cfg.SelectedArmour == ArmourKind.Spiritual && cfg.SpiritualNonOpposite)
            details["spiritualNonOpposite"] = true;
        if (cfg.PAC > 0) details["PAC"] = cfg.PAC;
        if (cfg.DAC > 0) details["DAC"] = cfg.DAC;
        if (cfg.MAC > 0) details["MAC"] = cfg.MAC;
        if (cfg.SAC > 0) details["SAC"] = cfg.SAC;

        return new CalcResult
        {
            AbilityType = "Armour",
            AbilityName = kind,
            TotalIsp = cfg.Total,
            Details = details
        };
    }
    private static IDictionary<string, int> BuildTableDictionary(IReadOnlyList<int> table)
    {
        var dict = new Dictionary<string, int>();
        for (int i = 0; i < table.Count; i++)
        {
            dict[i.ToString()] = table[i];
        }
        return dict;
    }
}
