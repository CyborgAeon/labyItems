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
            1 => ArmourConfig.magicString,
            2 => ArmourConfig.spiritString,
            3 => ArmourConfig.manticString,
            _ => string.Empty
        };
    }

    protected override CalcResult BuildResult(ArmourConfig cfg)
    {
        var kind = cfg.SelectedArmour switch
        {
            ArmourConfig.magicString  => "Magical MC Armour",
            ArmourConfig.spiritString => "Spiritual MC Armour",
            ArmourConfig.manticString => "Mantic MC Armour",
            _                         => "No Armour"
        };

        var details = new Dictionary<string, object?>();
        details["armourType"] = cfg.SelectedArmour.ToString();
        if (cfg.ACBase > 0) details["AC"] = cfg.ACBase;
        if (!string.IsNullOrWhiteSpace(cfg.LayeredSummary))
            details["layeredSummary"] = cfg.LayeredSummary;
        if (cfg.MagicalColoursCount > 0)
            details["magicalColours"] = cfg.MagicalColoursCount;
        if (cfg.SpiritualNonOpposite)
            details["spiritualNonOpposite"] = true;

        var enhancements = new List<Dictionary<string, object>>();
        void AddEnh(string type, int value, int isp)
        {
            if (value <= 0 || isp <= 0)
                return;
            enhancements.Add(new Dictionary<string, object>
            {
                ["type"] = type,
                ["value"] = value,
                ["isp"] = isp
            });
        }

        AddEnh("PAC", cfg.PAC, ArmourConfig.GetTableCost(cfg.PAC, ArmourConfig.PacTable));
        AddEnh("DAC", cfg.DAC, ArmourConfig.GetTableCost(cfg.DAC, ArmourConfig.DacTable));
        AddEnh("MAC", cfg.MAC, ArmourConfig.GetTableCost(cfg.MAC, ArmourConfig.MacTable));
        AddEnh("SAC", cfg.SAC, ArmourConfig.GetTableCost(cfg.SAC, ArmourConfig.SacTable));

        if (enhancements.Count > 0)
            details["enhancementBonuses"] = enhancements;

        return new CalcResult
        {
            AbilityType = "Armour",
            AbilityName = kind,
            TotalIsp = cfg.Total,
            Summary = cfg.Breakdown,
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
