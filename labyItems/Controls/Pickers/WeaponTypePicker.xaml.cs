using System;
using System.Collections.Generic;
using System.Linq;
using labyItems.Models.Enums;

namespace labyItems.Controls;

public partial class WeaponTypePicker : EnumPicker<WeaponType>
{
    public WeaponTypePicker()
    {
        InitializeComponent();
        RegisterInnerPicker(InnerPicker);
        if (string.IsNullOrEmpty(LabelText))
            LabelText = "Select a weapon type";
        if (string.IsNullOrEmpty(PlaceholderText))
            PlaceholderText = "Select a weapon type";

    }

    public WeaponType? SelectedType
    {
        get => SelectedValue;
        set => SelectedValue = value;
    }
}
