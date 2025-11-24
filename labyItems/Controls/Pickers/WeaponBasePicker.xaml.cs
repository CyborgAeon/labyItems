using System;
using labyItems.Models.Enums;

namespace labyItems.Controls;

public partial class WeaponBasePicker : EnumPicker<WeaponBaseOption>
{
    public WeaponBasePicker()
    {
        InitializeComponent();

        // Custom display text for this enum, via a lambda:
        DisplayFormatter = opt => opt switch
        {
            WeaponBaseOption.Magic0 => "+0 Magic",
            WeaponBaseOption.Magic0Plus1VsType => "+0 Magic, +1 vs one type",
            WeaponBaseOption.Magic0Plus1VsGroup => "+0 Magic, +1 vs one group",
            WeaponBaseOption.MagicPlus1 => "+1 Magic",
            WeaponBaseOption.MagicPlus2 => "+2 Magic",
            WeaponBaseOption.PureMagic0 => "+0 Pure Magic",
            WeaponBaseOption.Spirit0 => "+0 Spirit",
            WeaponBaseOption.Spirit0Plus1VsType => "+0 Spirit, +1 vs one type",
            WeaponBaseOption.Spirit0Plus1VsGroup => "+0 Spirit, +1 vs one group",
            WeaponBaseOption.SpiritPlus1 => "+1 Spirit",
            WeaponBaseOption.SpiritPlus2 => "+2 Spirit",
            WeaponBaseOption.PureSpirit0 => "+0 Pure Spirit",
            WeaponBaseOption.Mantic0 => "+0 Mantic",
            WeaponBaseOption.ManticPlus1 => "+1 Mantic",
            WeaponBaseOption.PureMantic0 => "+0 Pure Mantic",
            WeaponBaseOption.PhysicalPlus1 => "+1 Physical",
            _ => opt.ToString(),
        };

        RegisterInnerPicker(InnerPicker);

        if (string.IsNullOrEmpty(PlaceholderText))
            PlaceholderText = "Select weapon base";
    }

    public WeaponBaseOption? SelectedWeaponBase
    {
        get => SelectedValue;
        set => SelectedValue = value;
    }
}

