using System.Collections.ObjectModel;
using System.Text;
using labyItems.Models.Enums;

namespace labyItems.Pages.Configs;

public class WeaponConfig : ConfigBase
{
    private WeaponBaseOption _weaponBase;
    public WeaponBaseOption WeaponBase
    {
        get => _weaponBase;
        set
        {
            // affectsTotal = true; no extra property names here –
            // we explicitly raise them below like your working example.
            if (SetProperty(ref _weaponBase, value, true))
            {
                // Notify WeaponBase itself
                OnPropertyChanged(); // nameof(WeaponBase) via CallerMemberName

                // All dependent computed properties that the UI binds to:
                OnPropertyChanged(nameof(IsMagicBase));
                OnPropertyChanged(nameof(IsSpiritBase));
                OnPropertyChanged(nameof(IsManticBase));

                OnPropertyChanged(nameof(ShowExtraColours));
                OnPropertyChanged(nameof(ShowExtraAlignments));

                OnPropertyChanged(nameof(ShowMagicVsType));
                OnPropertyChanged(nameof(ShowMagicVsGroup));
                OnPropertyChanged(nameof(ShowSpiritVsType));
                OnPropertyChanged(nameof(ShowSpiritVsGroup));

                // And the breakdown text
                OnPropertyChanged(nameof(Breakdown));
            }
        }
    }

    private int _magicalColoursCount;
    public int MagicalColoursCount
    {
        get => _magicalColoursCount;
        set
        {
            if (SetProperty(ref _magicalColoursCount, Math.Max(0, value), true))
            {
                // This influences the total & breakdown only
                OnPropertyChanged();              // MagicalColoursCount
                OnPropertyChanged(nameof(Breakdown));
            }
        }
    }

    private bool _spiritualNonOpposite;
    public bool SpiritualNonOpposite
    {
        get => _spiritualNonOpposite;
        set
        {
            if (SetProperty(ref _spiritualNonOpposite, value, true))
            {
                OnPropertyChanged();              // SpiritualNonOpposite
                OnPropertyChanged(nameof(Breakdown));
            }
        }
    }

    private bool _magicTurnsPureDaily;
    public bool MagicTurnsPureDaily
    {
        get => _magicTurnsPureDaily;
        set
        {
            if (SetProperty(ref _magicTurnsPureDaily, value, true))
            {
                OnPropertyChanged();              // MagicTurnsPureDaily
                OnPropertyChanged(nameof(Breakdown));
            }
        }
    }

    private bool _manticTurnsPureDaily;
    public bool ManticTurnsPureDaily
    {
        get => _manticTurnsPureDaily;
        set
        {
            if (SetProperty(ref _manticTurnsPureDaily, value, true))
            {
                OnPropertyChanged();              // ManticTurnsPureDaily
                OnPropertyChanged(nameof(Breakdown));
            }
        }
    }

    private bool _adventurePermDamageDaily;
    public bool AdventurePermDamageDaily
    {
        get => _adventurePermDamageDaily;
        set
        {
            if (SetProperty(ref _adventurePermDamageDaily, value, true))
            {
                OnPropertyChanged();              // AdventurePermDamageDaily
                OnPropertyChanged(nameof(Breakdown));
            }
        }
    }

    private bool _thruPacAlways;
    public bool ThruPacAlways
    {
        get => _thruPacAlways;
        set
        {
            if (SetProperty(ref _thruPacAlways, value, true))
            {
                OnPropertyChanged();              // ThruPacAlways
                OnPropertyChanged(nameof(Breakdown));
            }
        }
    }

    private bool _bladeSharpenTooGreat;
    public bool BladeSharpenTooGreat
    {
        get => _bladeSharpenTooGreat;
        set
        {
            if (SetProperty(ref _bladeSharpenTooGreat, value, true))
            {
                OnPropertyChanged();              // BladeSharpenTooGreat
                OnPropertyChanged(nameof(Breakdown));
            }
        }
    }

    private bool _supernaturalBladeSharpen;
    public bool SupernaturalBladeSharpen
    {
        get => _supernaturalBladeSharpen;
        set
        {
            if (SetProperty(ref _supernaturalBladeSharpen, value, true))
            {
                OnPropertyChanged();              // SupernaturalBladeSharpen
                OnPropertyChanged(nameof(Breakdown));
            }
        }
    }

    private bool _cutThroughAuraDaily;
    public bool CutThroughAuraDaily
    {
        get => _cutThroughAuraDaily;
        set
        {
            if (SetProperty(ref _cutThroughAuraDaily, value, true))
            {
                OnPropertyChanged();              // CutThroughAuraDaily
                OnPropertyChanged(nameof(Breakdown));
            }
        }
    }

    public bool IsMagicBase =>
        WeaponBase
            is WeaponBaseOption.Magic0
                or WeaponBaseOption.Magic0Plus1VsType
                or WeaponBaseOption.Magic0Plus1VsGroup
                or WeaponBaseOption.MagicPlus1
                or WeaponBaseOption.MagicPlus2
                or WeaponBaseOption.PureMagic0;

    public bool IsSpiritBase =>
        WeaponBase
            is WeaponBaseOption.Spirit0
                or WeaponBaseOption.Spirit0Plus1VsType
                or WeaponBaseOption.Spirit0Plus1VsGroup
                or WeaponBaseOption.SpiritPlus1
                or WeaponBaseOption.SpiritPlus2
                or WeaponBaseOption.PureSpirit0;

    public bool IsManticBase =>
        WeaponBase
            is WeaponBaseOption.Mantic0
                or WeaponBaseOption.ManticPlus1
                or WeaponBaseOption.PureMantic0;

    public bool ShowExtraColours => IsMagicBase || IsManticBase;
    public bool ShowExtraAlignments => IsSpiritBase || IsManticBase;

    public bool ShowMagicVsType => WeaponBase is WeaponBaseOption.Magic0Plus1VsType;
    public bool ShowMagicVsGroup => WeaponBase is WeaponBaseOption.Magic0Plus1VsGroup;
    public bool ShowSpiritVsType => WeaponBase is WeaponBaseOption.Spirit0Plus1VsType;
    public bool ShowSpiritVsGroup => WeaponBase is WeaponBaseOption.Spirit0Plus1VsGroup;

    private string _magicVsType = string.Empty;
    public string MagicVsType
    {
        get => _magicVsType;
        set => SetProperty(ref _magicVsType, value, false);
    }

    private string _magicVsGroup = string.Empty;
    public string MagicVsGroup
    {
        get => _magicVsGroup;
        set => SetProperty(ref _magicVsGroup, value, false);
    }

    private string _spiritVsType = string.Empty;
    public string SpiritVsType
    {
        get => _spiritVsType;
        set => SetProperty(ref _spiritVsType, value, false);
    }

    private string _spiritVsGroup = string.Empty;
    public string SpiritVsGroup
    {
        get => _spiritVsGroup;
        set => SetProperty(ref _spiritVsGroup, value, false);
    }

    public ObservableCollection<MagicColours?> ExtraColours { get; } = new() { MagicColours.Grey };

    private string _extraColoursSummary = string.Empty;
    public string ExtraColoursSummary
    {
        get => _extraColoursSummary;
        set => SetProperty(ref _extraColoursSummary, value, true);
    }

    private int _extraColoursCount;
    public int ExtraColoursCount
    {
        get => _extraColoursCount;
        set
        {
            if (SetProperty(ref _extraColoursCount, value, true))
            {
                OnPropertyChanged();              // ExtraColoursCount
                OnPropertyChanged(nameof(Breakdown));
            }
        }
    }

    private string _extraColoursCountLabel = string.Empty;
    public string ExtraColoursCountLabel
    {
        get => _extraColoursCountLabel;
        set => SetProperty(ref _extraColoursCountLabel, value, false);
    }

    public ObservableCollection<Alignments?> ExtraAlignments { get; } =
        new() { Alignments.Neutral };

    private string _extraAlignmentsSummary = string.Empty;
    public string ExtraAlignmentsSummary
    {
        get => _extraAlignmentsSummary;
        set => SetProperty(ref _extraAlignmentsSummary, value, true);
    }

    private int _extraAlignmentsCount;
    public int ExtraAlignmentsCount
    {
        get => _extraAlignmentsCount;
        set
        {
            if (SetProperty(ref _extraAlignmentsCount, value, true))
            {
                OnPropertyChanged();              // ExtraAlignmentsCount
                OnPropertyChanged(nameof(Breakdown));
            }
        }
    }

    private string _extraAlignmentsCountLabel = string.Empty;
    public string ExtraAlignmentsCountLabel
    {
        get => _extraAlignmentsCountLabel;
        set => SetProperty(ref _extraAlignmentsCountLabel, value, false);
    }

    private string _breakdown = "";
    public string Breakdown
    {
        get => _breakdown;
        private set
        {
            if (_breakdown != value)
            {
                _breakdown = value;
                OnPropertyChanged();
            }
        }
    }

    protected override int ExtraTotal()
    {
        int total = 0;
        var sb = new StringBuilder();

        int baseCost = WeaponBase switch
        {
            WeaponBaseOption.Magic0 => 20,
            WeaponBaseOption.Magic0Plus1VsType => 25,
            WeaponBaseOption.Magic0Plus1VsGroup => 30,
            WeaponBaseOption.MagicPlus1 => 40,
            WeaponBaseOption.MagicPlus2 => 60,
            WeaponBaseOption.PureMagic0 => 40,

            WeaponBaseOption.Spirit0 => 25,
            WeaponBaseOption.Spirit0Plus1VsType => 30,
            WeaponBaseOption.Spirit0Plus1VsGroup => 35,
            WeaponBaseOption.SpiritPlus1 => 50,
            WeaponBaseOption.SpiritPlus2 => 75,
            WeaponBaseOption.PureSpirit0 => 45,

            WeaponBaseOption.Mantic0 => 50,
            WeaponBaseOption.ManticPlus1 => 100,
            WeaponBaseOption.PureMantic0 => 90,

            WeaponBaseOption.PhysicalPlus1 => 20,
            _ => 0,
        };
        if (baseCost > 0)
        {
            total += baseCost;
            sb.AppendLine($"Base: {WeaponBase} = {baseCost}");
        }

        if (IsMagicBase && MagicalColoursCount > 0)
        {
            int c = 3 * MagicalColoursCount;
            total += c;
            sb.AppendLine($"+ Magical colours: 3 × {MagicalColoursCount} = {c}");
        }

        if (IsSpiritBase && SpiritualNonOpposite)
        {
            total += 5;
            sb.AppendLine("+ Spiritual non-opposite: 5");
        }

        if (MagicTurnsPureDaily)
        {
            total += 5;
            sb.AppendLine("+ Magic/Spirit turns Pure 1/day (5)");
        }
        if (ManticTurnsPureDaily)
        {
            total += 10;
            sb.AppendLine("+ Mantic turns Pure 1/day (10)");
        }
        if (AdventurePermDamageDaily)
        {
            total += 25;
            sb.AppendLine("+ Adventure perm dmg 1/day (25)");
        }
        if (ThruPacAlways)
        {
            total += 30;
            sb.AppendLine("+ Thru PAC at all times (30)");
        }
        if (BladeSharpenTooGreat)
        {
            total += 5;
            sb.AppendLine("+ Blade sharpen (too great) (5)");
        }
        if (SupernaturalBladeSharpen)
        {
            total += 10;
            sb.AppendLine("+ Supernatural blade sharpen (10)");
        }
        if (CutThroughAuraDaily)
        {
            total += 25;
            sb.AppendLine("+ Cut through Aura of Defence 1/day (25)");
        }

        if ((IsMagicBase || IsManticBase) && ExtraColoursCount > 1)
        {
            int extra = (ExtraColoursCount - 1) * 3;
            total += extra;
            sb.AppendLine($"+ Extra colours: {ExtraColoursCount} ({extra})");
        }

        if ((IsSpiritBase || IsManticBase) && ExtraAlignmentsCount > 1)
        {
            int extra = (ExtraAlignmentsCount - 1) * 3;
            total += extra;
            sb.AppendLine($"+ Extra alignments: {ExtraAlignmentsCount} ({extra})");
        }

        Breakdown = sb.ToString().TrimEnd();
        return total;
    }
}
