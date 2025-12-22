using labyItems.Models.Enums;
using Microsoft.Maui.Controls;

namespace labyItems.Controls;

public partial class WeaponTypePicker : EnumPicker<WeaponType>
{
    public WeaponTypePicker()
    {
        Resources ??= new ResourceDictionary();
        Resources[nameof(DisplayConverter)] = DisplayConverter;

        InitializeComponent();
        RegisterSearchEntry(SearchEntry, SuggestionsView);
        if (string.IsNullOrEmpty(LabelText))
            LabelText = "Select a weapon type";
        if (string.IsNullOrEmpty(PlaceholderText))
            PlaceholderText = "Select a weapon type";

    }

    public WeaponType? SelectedType
    {
        get => SelectedValue;
        set => SelectedValue = value;
    }

    private void OnToggleTapped(object? sender, TappedEventArgs e)
    {
        SearchEntry?.Focus();
    }
}
