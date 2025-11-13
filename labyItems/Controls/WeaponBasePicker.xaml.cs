using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using labyItems.Models.Enums;

namespace labyItems.Controls;

public partial class WeaponBasePicker : ContentView
{
    public WeaponBasePicker()
    {
        InitializeComponent();
        PopulatePicker();
    }

    private void PopulatePicker()
    {
        var mapping = new Dictionary<string, WeaponBaseOption>
        {
            { "+0 Magic (any one colour/hue incl. grey) — 20", WeaponBaseOption.Magic0 },
            { "+0 Magic, +1 vs one type — 25", WeaponBaseOption.Magic0Plus1VsType },
            { "+0 Magic, +1 vs one group — 30", WeaponBaseOption.Magic0Plus1VsGroup },
            { "+1 Magic — 40", WeaponBaseOption.MagicPlus1 },
            { "+2 Magic — 60", WeaponBaseOption.MagicPlus2 },
            { "+0 Pure Magic — 40", WeaponBaseOption.PureMagic0 },

            { "+0 Spirit (any one alignment) — 25", WeaponBaseOption.Spirit0 },
            { "+0 Spirit, +1 vs one type — 30", WeaponBaseOption.Spirit0Plus1VsType },
            { "+0 Spirit, +1 vs one group — 35", WeaponBaseOption.Spirit0Plus1VsGroup },
            { "+1 Spirit — 50", WeaponBaseOption.SpiritPlus1 },
            { "+2 Spirit — 75", WeaponBaseOption.SpiritPlus2 },
            { "+0 Pure Spirit — 45", WeaponBaseOption.PureSpirit0 },

            { "+0 Mantic (any one colour and any one alignment) — 50", WeaponBaseOption.Mantic0 },
            { "+1 Mantic — 100", WeaponBaseOption.ManticPlus1 },
            { "+0 Pure Mantic — 90", WeaponBaseOption.PureMantic0 },

            { "+1 Physical — 20", WeaponBaseOption.PhysicalPlus1 }
        };

        _mapping = mapping;

        WeaponBasePickerControl.ItemsSource = mapping.Keys.ToList();
    }

    private Dictionary<string, WeaponBaseOption> _mapping = new();

    // Label
    public static readonly BindableProperty LabelTextProperty =
        BindableProperty.Create(
            nameof(LabelText),
            typeof(string),
            typeof(WeaponBasePicker),
            "Weapon base");

    public string LabelText
    {
        get => (string)GetValue(LabelTextProperty);
        set => SetValue(LabelTextProperty, value);
    }

    // SelectedBase (the enum)
    public static readonly BindableProperty SelectedBaseProperty =
        BindableProperty.Create(
            nameof(SelectedBase),
            typeof(WeaponBaseOption),
            typeof(WeaponBasePicker),
            default(WeaponBaseOption),
            BindingMode.TwoWay);

    public WeaponBaseOption SelectedBase
    {
        get => (WeaponBaseOption)GetValue(SelectedBaseProperty);
        set => SetValue(SelectedBaseProperty, value);
    }

    private void OnPickerChanged(object sender, EventArgs e)
    {
        if (WeaponBasePickerControl.SelectedItem is string key &&
            _mapping.TryGetValue(key, out var opt))
        {
            SelectedBase = opt;
        }
    }
}
