using labyItems.Models.Enums;

namespace labyItems.Controls;

public partial class MagicColourPicker : EnumPicker<MagicColours>
{
    public MagicColourPicker()
    {
        InitializeComponent();
        RegisterInnerPicker(InnerPicker);
        if (string.IsNullOrEmpty(LabelText))
            LabelText = "Magic Colour";
        if (string.IsNullOrEmpty(PlaceholderText))
            PlaceholderText = "Select a colour";

    }

    public MagicColours? SelectedColour
    {
        get => SelectedValue;
        set => SelectedValue = value;
    }
    
}
