using labyItems.Models.Enums;

namespace labyItems.Controls;

public partial class EvocationFieldPicker : EnumPicker<EvocationFields>
{
    public EvocationFieldPicker()
    {
        InitializeComponent();
        RegisterInnerPicker(InnerPicker);
        if (string.IsNullOrWhiteSpace(PlaceholderText))
            PlaceholderText = "Select field";
    }

    public EvocationFields? SelectedField
    {
        get => SelectedValue;
        set => SelectedValue = value;
    }
}
