using System.Text;
using labyItems.Models.Enums;

namespace labyItems.Pages.Configs;

public class WeaponConfig : ConfigBase
{
    private WeaponBaseOption _base;
    public WeaponBaseOption Base
    {
        get => _base;
        set { if (SetProperty(ref _base, value, affectsTotal: true)) OnPropertyChanged(nameof(Breakdown)); }
    }

    private int _magicalColoursCount;
    public int MagicalColoursCount
    {
        get => _magicalColoursCount;
        set { if (SetProperty(ref _magicalColoursCount, Math.Max(0, value), affectsTotal: true)) OnPropertyChanged(nameof(Breakdown)); }
    }

    private bool _spiritualNonOpposite;
    public bool SpiritualNonOpposite
    {
        get => _spiritualNonOpposite;
        set { if (SetProperty(ref _spiritualNonOpposite, value, affectsTotal: true)) OnPropertyChanged(nameof(Breakdown)); }
    }

    private bool _magicTurnsPureDaily;
    public bool MagicTurnsPureDaily
    {
        get => _magicTurnsPureDaily;
        set { if (SetProperty(ref _magicTurnsPureDaily, value, affectsTotal: true)) OnPropertyChanged(nameof(Breakdown)); }
    }

    private bool _manticTurnsPureDaily;
    public bool ManticTurnsPureDaily
    {
        get => _manticTurnsPureDaily;
        set { if (SetProperty(ref _manticTurnsPureDaily, value, affectsTotal: true)) OnPropertyChanged(nameof(Breakdown)); }
    }

    private bool _adventurePermDamageDaily;
    public bool AdventurePermDamageDaily
    {
        get => _adventurePermDamageDaily;
        set { if (SetProperty(ref _adventurePermDamageDaily, value, affectsTotal: true)) OnPropertyChanged(nameof(Breakdown)); }
    }

    private bool _thruPacAlways;
    public bool ThruPacAlways
    {
        get => _thruPacAlways;
        set { if (SetProperty(ref _thruPacAlways, value, affectsTotal: true)) OnPropertyChanged(nameof(Breakdown)); }
    }

    private bool _bladeSharpenTooGreat;
    public bool BladeSharpenTooGreat
    {
        get => _bladeSharpenTooGreat;
        set { if (SetProperty(ref _bladeSharpenTooGreat, value, affectsTotal: true)) OnPropertyChanged(nameof(Breakdown)); }
    }

    private bool _supernaturalBladeSharpen;
    public bool SupernaturalBladeSharpen
    {
        get => _supernaturalBladeSharpen;
        set { if (SetProperty(ref _supernaturalBladeSharpen, value, affectsTotal: true)) OnPropertyChanged(nameof(Breakdown)); }
    }

    private bool _cutThroughAuraDaily;
    public bool CutThroughAuraDaily
    {
        get => _cutThroughAuraDaily;
        set { if (SetProperty(ref _cutThroughAuraDaily, value, affectsTotal: true)) OnPropertyChanged(nameof(Breakdown)); }
    }

    public bool IsMagicBase =>
        Base is WeaponBaseOption.Magic0
            or WeaponBaseOption.Magic0Plus1VsType
            or WeaponBaseOption.Magic0Plus1VsGroup
            or WeaponBaseOption.MagicPlus1
            or WeaponBaseOption.MagicPlus2
            or WeaponBaseOption.PureMagic0;

    public bool IsSpiritBase =>
        Base is WeaponBaseOption.Spirit0
            or WeaponBaseOption.Spirit0Plus1VsType
            or WeaponBaseOption.Spirit0Plus1VsGroup
            or WeaponBaseOption.SpiritPlus1
            or WeaponBaseOption.SpiritPlus2
            or WeaponBaseOption.PureSpirit0;

    private string _breakdown = "";
    public string Breakdown
    {
        get => _breakdown;
        private set { if (_breakdown != value) { _breakdown = value; OnPropertyChanged(); } }
    }

    protected override int ApplyMultipliers(int total) => total;
    
    protected override int ExtraTotal()
    {
        int total = 0;
        var sb = new StringBuilder();

        int baseCost = Base switch
        {
            WeaponBaseOption.Magic0              => 20,
            WeaponBaseOption.Magic0Plus1VsType   => 25,
            WeaponBaseOption.Magic0Plus1VsGroup  => 30,
            WeaponBaseOption.MagicPlus1          => 40,
            WeaponBaseOption.MagicPlus2          => 60,
            WeaponBaseOption.PureMagic0          => 40,

            WeaponBaseOption.Spirit0             => 25,
            WeaponBaseOption.Spirit0Plus1VsType  => 30,
            WeaponBaseOption.Spirit0Plus1VsGroup => 35,
            WeaponBaseOption.SpiritPlus1         => 50,
            WeaponBaseOption.SpiritPlus2         => 75,
            WeaponBaseOption.PureSpirit0         => 45,

            WeaponBaseOption.Mantic0             => 50,
            WeaponBaseOption.ManticPlus1         => 100,
            WeaponBaseOption.PureMantic0         => 90,

            WeaponBaseOption.PhysicalPlus1       => 20,
            _                                    => 0
        };
        if (baseCost > 0) { total += baseCost; sb.AppendLine($"Base: {Base} = {baseCost}"); }

        if (IsMagicBase && MagicalColoursCount > 0)
        {
            int c = 3 * MagicalColoursCount; total += c;
            sb.AppendLine($"+ Magical colours: 3 × {MagicalColoursCount} = {c}");
        }

        if (IsSpiritBase && SpiritualNonOpposite)
        {
            total += 5; sb.AppendLine("+ Spiritual non-opposite: 5");
        }

        if (MagicTurnsPureDaily)      { total += 5;  sb.AppendLine("+ Magic/Spirit turns Pure 1/day (5)"); }
        if (ManticTurnsPureDaily)     { total += 10; sb.AppendLine("+ Mantic turns Pure 1/day (10)"); }
        if (AdventurePermDamageDaily) { total += 25; sb.AppendLine("+ Adventure perm dmg 1/day (25)"); }
        if (ThruPacAlways)            { total += 30; sb.AppendLine("+ Thru PAC at all times (30)"); }
        if (BladeSharpenTooGreat)     { total += 5;  sb.AppendLine("+ Blade sharpen (too great) (5)"); }
        if (SupernaturalBladeSharpen) { total += 10; sb.AppendLine("+ Supernatural blade sharpen (10)"); }
        if (CutThroughAuraDaily)      { total += 25; sb.AppendLine("+ Cut through Aura of Defence 1/day (25)"); }

        Breakdown = sb.ToString().TrimEnd();
        return total;
    }
}
