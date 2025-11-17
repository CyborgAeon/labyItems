using System;
using System.Collections.Generic;
using System.Linq;
using labyItems.Models.Enums;

namespace labyItems.Controls;

public partial class ElfInnateLevelPicker : EnumPicker<ElfInnateLevel>
{
    public ElfInnateLevelPicker()
    {
        InitializeComponent();
        RegisterInnerPicker(InnerPicker);
        if (string.IsNullOrEmpty(PlaceholderText))
            PlaceholderText = "arbitrary text";
    }

    public ElfInnateLevel? SelectedType
    {
        get => SelectedValue;
        set => SelectedValue = value;
    }
}
