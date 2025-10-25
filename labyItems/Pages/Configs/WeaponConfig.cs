using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace labyItems.Pages.Configs;

public enum WeaponBaseOption
{
    None,

    // Magic
    Magic0,
    Magic0Plus1VsType,
    Magic0Plus1VsGroup,
    MagicPlus1,
    MagicPlus2,
    PureMagic0,

    // Spirit
    Spirit0,
    Spirit0Plus1VsType,
    Spirit0Plus1VsGroup,
    SpiritPlus1,
    SpiritPlus2,
    PureSpirit0,

    // Mantic
    Mantic0,
    ManticPlus1,
    PureMantic0,

    // Physical
    PhysicalPlus1
}

public class WeaponConfig : INotifyPropertyChanged
{
    // === Inputs ===
    private WeaponBaseOption _base;
    public WeaponBaseOption Base
    {
        get => _base;
        set { if (_base != value) { _base = value; OnPropertyChanged(); Recalculate(); } }
    }

    // Magic-only modifier
    private int _magicalColoursCount; // +3 each if magic base
    public int MagicalColoursCount
    {
        get => _magicalColoursCount;
        set { var v = Math.Max(0, value); if (_magicalColoursCount != v) { _magicalColoursCount = v; OnPropertyChanged(); Recalculate(); } }
    }

    // Spirit-only modifier
    private bool _spiritualNonOpposite; // +5 if spirit base
    public bool SpiritualNonOpposite
    {
        get => _spiritualNonOpposite;
        set { if (_spiritualNonOpposite != value) { _spiritualNonOpposite = value; OnPropertyChanged(); Recalculate(); } }
    }

    // General add-ons
    private bool _magicTurnsPureDaily;
    public bool MagicTurnsPureDaily { get => _magicTurnsPureDaily; set { if (_magicTurnsPureDaily != value) { _magicTurnsPureDaily = value; OnPropertyChanged(); Recalculate(); } } }

    private bool _manticTurnsPureDaily;
    public bool ManticTurnsPureDaily { get => _manticTurnsPureDaily; set { if (_manticTurnsPureDaily != value) { _manticTurnsPureDaily = value; OnPropertyChanged(); Recalculate(); } } }

    private bool _adventurePermDamageDaily;
    public bool AdventurePermDamageDaily { get => _adventurePermDamageDaily; set { if (_adventurePermDamageDaily != value) { _adventurePermDamageDaily = value; OnPropertyChanged(); Recalculate(); } } }

    private bool _thruPacAlways;
    public bool ThruPacAlways { get => _thruPacAlways; set { if (_thruPacAlways != value) { _thruPacAlways = value; OnPropertyChanged(); Recalculate(); } } }

    private bool _bladeSharpenTooGreat;
    public bool BladeSharpenTooGreat { get => _bladeSharpenTooGreat; set { if (_bladeSharpenTooGreat != value) { _bladeSharpenTooGreat = value; OnPropertyChanged(); Recalculate(); } } }

    private bool _supernaturalBladeSharpen;
    public bool SupernaturalBladeSharpen { get => _supernaturalBladeSharpen; set { if (_supernaturalBladeSharpen != value) { _supernaturalBladeSharpen = value; OnPropertyChanged(); Recalculate(); } } }

    private bool _cutThroughAuraDaily;
    public bool CutThroughAuraDaily { get => _cutThroughAuraDaily; set { if (_cutThroughAuraDaily != value) { _cutThroughAuraDaily = value; OnPropertyChanged(); Recalculate(); } } }

    // === Outputs ===
    private int _total;
    public int Total { get => _total; private set { if (_total != value) { _total = value; OnPropertyChanged(); } } }

    private string _breakdown = "";
    public string Breakdown { get => _breakdown; private set { if (_breakdown != value) { _breakdown = value; OnPropertyChanged(); } } }

    // Convenience flags (used by UI and summary)
    public bool IsMagicBase  => Base is WeaponBaseOption.Magic0 or WeaponBaseOption.Magic0Plus1VsType or WeaponBaseOption.Magic0Plus1VsGroup or WeaponBaseOption.MagicPlus1 or WeaponBaseOption.MagicPlus2 or WeaponBaseOption.PureMagic0;
    public bool IsSpiritBase => Base is WeaponBaseOption.Spirit0 or WeaponBaseOption.Spirit0Plus1VsType or WeaponBaseOption.Spirit0Plus1VsGroup or WeaponBaseOption.SpiritPlus1 or WeaponBaseOption.SpiritPlus2 or WeaponBaseOption.PureSpirit0;

    // === Calculation ===
    public void Recalculate()
    {
        int total = 0;
        var sb = new StringBuilder();

        // Base cost
        int baseCost = Base switch
        {
            // Magic
            WeaponBaseOption.Magic0              => 20,
            WeaponBaseOption.Magic0Plus1VsType   => 25,
            WeaponBaseOption.Magic0Plus1VsGroup  => 30,
            WeaponBaseOption.MagicPlus1          => 40,
            WeaponBaseOption.MagicPlus2          => 60,
            WeaponBaseOption.PureMagic0          => 40,

            // Spirit
            WeaponBaseOption.Spirit0             => 25,
            WeaponBaseOption.Spirit0Plus1VsType  => 30,
            WeaponBaseOption.Spirit0Plus1VsGroup => 35,
            WeaponBaseOption.SpiritPlus1         => 50,
            WeaponBaseOption.SpiritPlus2         => 75,
            WeaponBaseOption.PureSpirit0         => 45,

            // Mantic
            WeaponBaseOption.Mantic0             => 50,
            WeaponBaseOption.ManticPlus1         => 100,
            WeaponBaseOption.PureMantic0         => 90,

            // Physical
            WeaponBaseOption.PhysicalPlus1       => 20,

            _                                    => 0
        };
        if (baseCost > 0)
        {
            total += baseCost;
            sb.AppendLine($"Base: {Base} = {baseCost}");
        }

        // Magic modifier: +3 per colour (only if magic base)
        if (IsMagicBase && MagicalColoursCount > 0)
        {
            int c = 3 * MagicalColoursCount;
            total += c;
            sb.AppendLine($"+ Magical colours: 3 × {MagicalColoursCount} = {c}");
        }

        // Spirit modifier: +5 non-opposite (only if spirit base)
        if (IsSpiritBase && SpiritualNonOpposite)
        {
            total += 5;
            sb.AppendLine("+ Spiritual non-opposite: 5");
        }

        // General add-ons
        if (MagicTurnsPureDaily)        { total += 5;  sb.AppendLine("+ Magic/Spirit turns Pure 1/day (5)"); }
        if (ManticTurnsPureDaily)       { total += 10; sb.AppendLine("+ Mantic turns Pure 1/day (10)"); }
        if (AdventurePermDamageDaily)   { total += 25; sb.AppendLine("+ Adventure perm dmg 1/day (25)"); }
        if (ThruPacAlways)              { total += 30; sb.AppendLine("+ Thru PAC at all times (30)"); }
        if (BladeSharpenTooGreat)       { total += 5;  sb.AppendLine("+ Blade sharpen (too great) (5)"); }
        if (SupernaturalBladeSharpen)   { total += 10; sb.AppendLine("+ Supernatural blade sharpen (10)"); }
        if (CutThroughAuraDaily)        { total += 25; sb.AppendLine("+ Cut through Aura of Defence 1/day (25)"); }

        Total = total;
        Breakdown = sb.ToString().TrimEnd();
    }

    // INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
