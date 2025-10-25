using labyItems.Models;
using labyItems.Pages.Configs;

namespace labyItems.Pages;

public partial class WeaponConfigPage : ContentPage
{
    private readonly TaskCompletionSource<CalcResult?> _tcs = new();
    public Task<CalcResult?> Completion => _tcs.Task;

    public WeaponConfigPage()
    {
        InitializeComponent();
        BindingContext = new WeaponConfig(); // auto-recalculates on property changes
    }

    // Map picker index -> enum (setters trigger Recalculate)
    private void OnBaseChanged(object sender, EventArgs e)
    {
        var cfg = (WeaponConfig)BindingContext;
        switch (BasePicker.SelectedIndex)
        {
            case 1:  cfg.Base = WeaponBaseOption.Magic0; break;
            case 2:  cfg.Base = WeaponBaseOption.Magic0Plus1VsType; break;
            case 3:  cfg.Base = WeaponBaseOption.Magic0Plus1VsGroup; break;
            case 4:  cfg.Base = WeaponBaseOption.MagicPlus1; break;
            case 5:  cfg.Base = WeaponBaseOption.MagicPlus2; break;
            case 6:  cfg.Base = WeaponBaseOption.PureMagic0; break;

            case 7:  cfg.Base = WeaponBaseOption.Spirit0; break;
            case 8:  cfg.Base = WeaponBaseOption.Spirit0Plus1VsType; break;
            case 9:  cfg.Base = WeaponBaseOption.Spirit0Plus1VsGroup; break;
            case 10: cfg.Base = WeaponBaseOption.SpiritPlus1; break;
            case 11: cfg.Base = WeaponBaseOption.SpiritPlus2; break;
            case 12: cfg.Base = WeaponBaseOption.PureSpirit0; break;

            case 13: cfg.Base = WeaponBaseOption.Mantic0; break;
            case 14: cfg.Base = WeaponBaseOption.ManticPlus1; break;
            case 15: cfg.Base = WeaponBaseOption.PureMantic0; break;

            case 16: cfg.Base = WeaponBaseOption.PhysicalPlus1; break;

            default: cfg.Base = WeaponBaseOption.None; break;
        }
    }

    private string BuildSummary(WeaponConfig cfg)
    {
        var baseLabel = cfg.Base switch
        {
            WeaponBaseOption.Magic0               => "+0 Magic",
            WeaponBaseOption.Magic0Plus1VsType    => "+0 Magic, +1 vs type",
            WeaponBaseOption.Magic0Plus1VsGroup   => "+0 Magic, +1 vs group",
            WeaponBaseOption.MagicPlus1           => "+1 Magic",
            WeaponBaseOption.MagicPlus2           => "+2 Magic",
            WeaponBaseOption.PureMagic0           => "+0 Pure Magic",

            WeaponBaseOption.Spirit0              => "+0 Spirit",
            WeaponBaseOption.Spirit0Plus1VsType   => "+0 Spirit, +1 vs type",
            WeaponBaseOption.Spirit0Plus1VsGroup  => "+0 Spirit, +1 vs group",
            WeaponBaseOption.SpiritPlus1          => "+1 Spirit",
            WeaponBaseOption.SpiritPlus2          => "+2 Spirit",
            WeaponBaseOption.PureSpirit0          => "+0 Pure Spirit",

            WeaponBaseOption.Mantic0              => "+0 Mantic",
            WeaponBaseOption.ManticPlus1          => "+1 Mantic",
            WeaponBaseOption.PureMantic0          => "+0 Pure Mantic",

            WeaponBaseOption.PhysicalPlus1        => "+1 Physical",
            _                                     => "No Base"
        };

        var tags = new List<string>();
        if (cfg.IsMagicBase && cfg.MagicalColoursCount > 0) tags.Add($"+{3 * cfg.MagicalColoursCount} (colours)");
        if (cfg.IsSpiritBase && cfg.SpiritualNonOpposite)   tags.add("+5 (non-opposite)".Replace("add","+").Replace("Add","+") ); // safeguard typo
        if (cfg.MagicTurnsPureDaily)                        tags.Add("+5 Pure/day");
        if (cfg.ManticTurnsPureDaily)                       tags.Add("+10 Mantic Pure/day");
        if (cfg.AdventurePermDamageDaily)                   tags.Add("+25 Adv perm/day");
        if (cfg.ThruPacAlways)                              tags.Add("+30 thru PAC");
        if (cfg.BladeSharpenTooGreat)                       tags.Add("+5 sharpen size");
        if (cfg.SupernaturalBladeSharpen)                   tags.Add("+10 sup. sharpen");
        if (cfg.CutThroughAuraDaily)                        tags.Add("+25 cut AoD/day");

        var tagText = tags.Count > 0 ? $" [{string.Join(", ", tags)}]" : "";
        return $"{baseLabel}{tagText} → {cfg.Total} ISP";
    }

    private async void OnReturn(object sender, EventArgs e)
    {
        var cfg = (WeaponConfig)BindingContext;
        var res = new CalcResult
        {
            TotalIsp = cfg.Total,
            Summary  = BuildSummary(cfg)
        };
        _tcs.TrySetResult(res);
        await Navigation.PopAsync();
    }
}
