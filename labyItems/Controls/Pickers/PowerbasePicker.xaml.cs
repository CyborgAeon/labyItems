using labyItems.Models.Enums;

namespace labyItems.Controls;

public partial class PowerbaseEnumPicker : EnumPicker<PowerbaseEnum>
{
    public PowerbaseEnumPicker()
    {
        InitializeComponent();
        RegisterInnerPicker(InnerPicker);
        if (string.IsNullOrEmpty(PlaceholderText))
            PlaceholderText = "Select a powerbase";

    }

    public PowerbaseEnum? SelectedColour
    {
        get => SelectedValue;
        set => SelectedValue = value;
    }
    
}
