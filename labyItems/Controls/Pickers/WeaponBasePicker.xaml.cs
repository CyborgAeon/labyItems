using System;
using labyItems.Models.Enums;
using Microsoft.Maui.Controls;

namespace labyItems.Controls;

public partial class WeaponBasePicker : EnumPicker<WeaponBaseOption>
{
    public WeaponBasePicker()
    {
        Resources ??= new ResourceDictionary();
        Resources[nameof(DisplayConverter)] = DisplayConverter;

        InitializeComponent();

        // Custom display text for this enum, via a lambda:
        DisplayFormatter = opt =>
            opt switch
            {
                WeaponBaseOption.PhysicalPlus1 => "+1 Physical",
                WeaponBaseOption.Magic0 => "+0 Magic",
                WeaponBaseOption.PureMagic0 => "+0 Pure Magic",
                WeaponBaseOption.Magic0Plus1VsType => "+0 Magic, +1 vs one type",
                WeaponBaseOption.Magic0Plus1VsGroup => "+0 Magic, +1 vs one group",
                WeaponBaseOption.MagicPlus1 => "+1 Magic",
                WeaponBaseOption.MagicPlus2 => "+2 Magic",
                WeaponBaseOption.Spirit0 => "+0 Spirit",
                WeaponBaseOption.PureSpirit0 => "+0 Pure Spirit",
                WeaponBaseOption.Spirit0Plus1VsType => "+0 Spirit, +1 vs one type",
                WeaponBaseOption.Spirit0Plus1VsGroup => "+0 Spirit, +1 vs one group",
                WeaponBaseOption.SpiritPlus1 => "+1 Spirit",
                WeaponBaseOption.SpiritPlus2 => "+2 Spirit",
                WeaponBaseOption.Mantic0 => "+0 Mantic",
                WeaponBaseOption.PureMantic0 => "+0 Pure Mantic",
                WeaponBaseOption.ManticPlus1 => "+1 Mantic",
                _ => opt.ToString(),
            };

        RegisterSearchEntry(SearchEntry, SuggestionsView);

        if (string.IsNullOrEmpty(PlaceholderText))
            PlaceholderText = "Select weapon base";
        if (string.IsNullOrEmpty(LabelText))
            LabelText = "Base power profile";
    }

    public WeaponBaseOption? SelectedWeaponBase
    {
        get => SelectedValue;
        set => SelectedValue = value;
    }

    private void OnToggleTapped(object? sender, TappedEventArgs e)
    {
        SearchEntry?.Focus();
    }
}
