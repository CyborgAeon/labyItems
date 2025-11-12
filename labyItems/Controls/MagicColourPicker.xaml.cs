using System;
using System.Collections.Generic;
using System.Linq;
using labyItems.Models.Enums;
namespace labyItems.Controls;

public partial class MagicColourPicker : ContentView
{
    public MagicColourPicker()
    {
        Colours = Enum.GetValues(typeof(MagicColours)).Cast<MagicColours>().ToList();
        InitializeComponent();
    }

    // ----- Label text -----
    public static readonly BindableProperty LabelTextProperty =
        BindableProperty.Create(
            nameof(LabelText),
            typeof(string),
            typeof(MagicColourPicker),
            "Magic Colour");

    public string LabelText
    {
        get => (string)GetValue(LabelTextProperty);
        set => SetValue(LabelTextProperty, value);
    }

    // ----- Selected Colour -----
    public static readonly BindableProperty SelectedColourProperty =
        BindableProperty.Create(
            nameof(SelectedColour),
            typeof(MagicColours?),
            typeof(MagicColourPicker),
            null,
            BindingMode.TwoWay);

    public MagicColours? SelectedColour
    {
        get => (MagicColours?)GetValue(SelectedColourProperty);
        set => SetValue(SelectedColourProperty, value);
    }

    // ----- Colour list -----
    public List<MagicColours> Colours { get; }
}
