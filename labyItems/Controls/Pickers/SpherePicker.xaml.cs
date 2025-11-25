using System;
using System.Collections.Generic;
using System.Linq;
using labyItems.Models.Enums;

namespace labyItems.Controls;

public partial class SpherePicker : EnumPicker<SpiritualSpheres>
{
    public SpherePicker()
    {
        InitializeComponent();
        RegisterInnerPicker(InnerPicker);
        if (string.IsNullOrEmpty(PlaceholderText))
            PlaceholderText = "Spiritual spheres";
    }

    public SpiritualSpheres? SelectedSphere
    {
        get => SelectedValue;
        set => SelectedValue = value;
    }
}
