using System.Collections.Generic;
using labyItems.Controls;
using labyItems.Models;
using labyItems.Pages.Configs;

using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace labyItems.Pages.Calculator;

public partial class WeaponConfigPage : ConfigPageBase<WeaponConfig>
{
    public WeaponConfigPage()
    {
        InitializeComponent();
        // BindingContext = new TConfig();
    }

    protected override CalcResult BuildResult(WeaponConfig cfg)
    {
        var details = new Dictionary<string, object?> { ["base"] = cfg.WeaponBase.ToString() };

        if (cfg.IsMagicBase && cfg.MagicalColoursCount > 0)
            details["magicalColours"] = cfg.MagicalColoursCount;
        if (cfg.IsSpiritBase && cfg.SpiritualNonOpposite)
            details["spiritualNonOpposite"] = true;

        if (cfg.MagicTurnsPureDaily)
            details["magicTurnsPureDaily"] = true;
        if (cfg.ManticTurnsPureDaily)
            details["manticTurnsPureDaily"] = true;
        if (cfg.AdventurePermDamageDaily)
            details["adventurePermDamageDaily"] = true;
        if (cfg.ThruPacAlways)
            details["thruPacAlways"] = true;
        if (cfg.BladeSharpenTooGreat)
            details["bladeSharpenTooGreat"] = true;
        if (cfg.SupernaturalBladeSharpen)
            details["supernaturalBladeSharpen"] = true;
        if (cfg.CutThroughAuraDaily)
            details["cutThroughAuraDaily"] = true;

        if (!string.IsNullOrWhiteSpace(cfg.MagicVsType)) details["magicVsType"] = cfg.MagicVsType;
        if (!string.IsNullOrWhiteSpace(cfg.MagicVsGroup)) details["magicVsGroup"] = cfg.MagicVsGroup;
        if (!string.IsNullOrWhiteSpace(cfg.SpiritVsType)) details["spiritVsType"] = cfg.SpiritVsType;
        if (!string.IsNullOrWhiteSpace(cfg.SpiritVsGroup)) details["spiritVsGroup"] = cfg.SpiritVsGroup;

        if ((cfg.IsMagicBase || cfg.IsManticBase) && cfg.ExtraColoursCount > 1)
            details["extraColours"] = cfg.ExtraColoursSummary;
        if ((cfg.IsSpiritBase || cfg.IsManticBase) && cfg.ExtraAlignmentsCount > 1)
            details["extraAlignments"] = cfg.ExtraAlignmentsSummary;

        return new CalcResult
        {
            AbilityType = "Weapon",
            AbilityName = cfg.WeaponBase.ToString(),
            TotalIsp = cfg.Total,
            Details = details,
        };
    }

    public static readonly BindableProperty ReturnToFormCommandProperty = BindableProperty.Create(
        nameof(ReturnToFormCommand),
        typeof(ICommand),
        typeof(WeaponConfigPage),
        null
    );

    public ICommand? ReturnToFormCommand
    {
        get => (ICommand?)GetValue(ReturnToFormCommandProperty);
        set => SetValue(ReturnToFormCommandProperty, value);
    }

}
