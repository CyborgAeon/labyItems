using labyItems.Models;
using labyItems.Pages.Configs;
using labyItems.Controls;
using labyItems.Pages.Configs;

namespace labyItems.Pages.Calculator;

public partial class WeaponConfigPage : ConfigPageBase<WeaponConfig>
{
    public WeaponConfigPage()
    {
        InitializeComponent();
    }

    protected override string BuildSummary(WeaponConfig cfg)
    {
        var tags = new List<string>();

        tags.Add(cfg.Base.ToString());
        if (cfg.IsMagicBase && cfg.MagicalColoursCount > 0) tags.Add($"+{3 * cfg.MagicalColoursCount} colours");
        if (cfg.IsSpiritBase && cfg.SpiritualNonOpposite)    tags.Add("+5 non-opposite");

        if (cfg.MagicTurnsPureDaily)      tags.Add("Magic/Spirit→Pure 1/day");
        if (cfg.ManticTurnsPureDaily)     tags.Add("Mantic→Pure 1/day");
        if (cfg.AdventurePermDamageDaily) tags.Add("Adventure perm dmg 1/day");
        if (cfg.ThruPacAlways)            tags.Add("Thru PAC always");
        if (cfg.BladeSharpenTooGreat)     tags.Add("Blade sharpen (too great)");
        if (cfg.SupernaturalBladeSharpen) tags.Add("Supernatural sharpen");
        if (cfg.CutThroughAuraDaily)      tags.Add("Cut through Aura 1/day");

        var text = string.Join(", ", tags.Where(t => !string.IsNullOrWhiteSpace(t)));
        return $"{text} → {cfg.Total} ISP";
    }
}
