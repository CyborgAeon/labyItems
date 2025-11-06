using System;
using System.Collections.Generic;
using System.Linq;
using labyItems.Models.Enums;

namespace labyItems.Controls;

public partial class ElfColourPicker : ContentView
{
    public ElfColourPicker()
    {
        Colours = Enum.GetValues(typeof(ElfColours))
                      .Cast<ElfColours>()
                      .ToList();

        InitializeComponent();
    }

    // ----- Label -----
    public static readonly BindableProperty LabelTextProperty =
        BindableProperty.Create(
            nameof(LabelText),
            typeof(string),
            typeof(ElfColourPicker),
            "Elf Colour");

    public string LabelText
    {
        get => (string)GetValue(LabelTextProperty);
        set => SetValue(LabelTextProperty, value);
    }

    // ----- Selected Colour -----
    public static readonly BindableProperty SelectedColourProperty =
        BindableProperty.Create(
            nameof(SelectedColour),
            typeof(ElfColours?),
            typeof(ElfColourPicker),
            null,
            BindingMode.TwoWay);

    public ElfColours? SelectedColour
    {
        get => (ElfColours?)GetValue(SelectedColourProperty);
        set => SetValue(SelectedColourProperty, value);
    }

    // ----- Enum List -----
    public List<ElfColours> Colours { get; }
}
