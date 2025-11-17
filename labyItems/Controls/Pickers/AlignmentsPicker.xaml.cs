using System;
using System.Collections.Generic;
using System.Linq;
using labyItems.Models.Enums;

namespace labyItems.Controls;

public partial class AlignmentsPicker : EnumPicker<Alignments>
{
    public AlignmentsPicker()
    {
        InitializeComponent();
        RegisterInnerPicker(InnerPicker);
        if (string.IsNullOrEmpty(PlaceholderText))
            PlaceholderText = "Select alignment";
    }

    public Alignments? SelectedType
    {
        get => SelectedValue;
        set => SelectedValue = value;
    }
}
