using labyItems.Models.Enums;

namespace labyItems.Controls;

public partial class MagicColourPicker : EnumPicker<MagicColours>
{
    public MagicColourPicker()
    {
        Options.Remove(MagicColours.Grey);
        InitializeComponent();
        RegisterInnerPicker(InnerPicker);
        if (string.IsNullOrEmpty(PlaceholderText))
            PlaceholderText = "Select a colour";
    }

    public MagicColours? SelectedColour
    {
        get => SelectedValue;
        set => SelectedValue = value;
    }
}
