using labyItems.Models.Enums;

namespace labyItems.Controls;

public partial class ShieldTypePicker : EnumPicker<ShieldType>
{
    public ShieldTypePicker()
    {
        InitializeComponent();
        RegisterInnerPicker(InnerPicker);
        if (string.IsNullOrWhiteSpace(PlaceholderText))
            PlaceholderText = "Select shield type";

        DisplayFormatter = ShieldTypeDisplay;
    }

    private static string ShieldTypeDisplay(ShieldType type) => type switch
    {
        ShieldType.Magical => "Magical shield",
        ShieldType.Spiritual => "Spiritual shield",
        ShieldType.Mantic => "Mantic shield",
        _ => type.ToString()
    };
}
