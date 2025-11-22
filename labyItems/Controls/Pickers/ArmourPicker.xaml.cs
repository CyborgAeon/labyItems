using System;
using System.Collections.Generic;
using System.Linq;
using labyItems.Models.Enums;

namespace labyItems.Controls;

public partial class ArmourPicker : EnumPicker<ArmourKind>
{
    public ArmourPicker()
    {
        InitializeComponent();
        RegisterInnerPicker(InnerPicker);
        if (string.IsNullOrEmpty(PlaceholderText))
            PlaceholderText = "Armour is empowered?";
    }

    public ArmourKind? SelectedType
    {
        get => SelectedValue;
        set => SelectedValue = value;
    }
}
