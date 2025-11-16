using System;
using System.Collections.Generic;
using System.Linq;
using labyItems.Models.Enums;

namespace labyItems.Controls;

public partial class UndeadTouchEffectPicker : EnumPicker<UndeadTouchEffects>
{
    public UndeadTouchEffectPicker()
    {
        InitializeComponent();
        RegisterInnerPicker(InnerPicker);
        if (string.IsNullOrEmpty(LabelText))
            LabelText = "Can use selected undead touch fx x times per day";
        if (string.IsNullOrEmpty(PlaceholderText))
            PlaceholderText = "Select an undead touch effect";

    }

    public UndeadTouchEffects? SelectedType
    {
        get => SelectedValue;
        set => SelectedValue = value;
    }
}
