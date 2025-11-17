using System;
using System.Collections.Generic;
using System.Linq;
using labyItems.Models.Enums;

namespace labyItems.Controls;

public partial class ElfColourPicker : EnumPicker<ElfColours>
{
    public ElfColourPicker()
    {
        InitializeComponent();
        RegisterInnerPicker(InnerPicker);
        if (string.IsNullOrEmpty(PlaceholderText))
            PlaceholderText = "Select a color";
    }

    public ElfColours? SelectedColour
    {
        get => SelectedValue;
        set => SelectedValue = value;
    }
}
