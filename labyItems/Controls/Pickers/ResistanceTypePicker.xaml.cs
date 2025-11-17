using System;
using System.Collections.Generic;
using System.Linq;
using labyItems.Models.Enums;

namespace labyItems.Controls;

public partial class ResistanceTypePicker : EnumPicker<GeneralResistanceTypes>
{
    public ResistanceTypePicker()
    {
        InitializeComponent();
        RegisterInnerPicker(InnerPicker);
        if (string.IsNullOrEmpty(LabelText))
            LabelText = "Additional levels of resistance for...";
        if (string.IsNullOrEmpty(PlaceholderText))
            PlaceholderText = "Select a type";

    }

    public GeneralResistanceTypes? SelectedType
    {
        get => SelectedValue;
        set => SelectedValue = value;
    }
}
