using labyItems.Models.Enums;
using Microsoft.Maui.Controls;

namespace labyItems.Controls;

public partial class MagicColourPicker : EnumPicker<MagicColours>
{
    public MagicColourPicker()
    {
        Options.Remove(MagicColours.Grey);
        Resources ??= new ResourceDictionary();
        Resources[nameof(DisplayConverter)] = DisplayConverter;
        InitializeComponent();
        RegisterSearchEntry(SearchEntry, SuggestionsView);
        if (string.IsNullOrEmpty(PlaceholderText))
            PlaceholderText = "Select a colour";
        if (string.IsNullOrEmpty(LabelText))
            LabelText = "Select a colour";
    }

    public MagicColours? SelectedColour
    {
        get => SelectedValue;
        set => SelectedValue = value;
    }

    private void OnToggleTapped(object? sender, TappedEventArgs e)
    {
        SearchEntry?.Focus();
    }

    private void OnOverlayTapped(object? sender, TappedEventArgs e)
    {
        HideSuggestions();
    }
}
