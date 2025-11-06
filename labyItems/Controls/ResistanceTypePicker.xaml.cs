using System;
using System.Collections.Generic;
using System.Linq;
using labyItems.Models.Enums; // <-- adjust if your enum lives elsewhere

namespace labyItems.Controls;

public partial class ResistanceTypePicker : ContentView
{
    public ResistanceTypePicker()
    {
        Types = Enum.GetValues(typeof(GeneralResistanceTypes))
                    .Cast<GeneralResistanceTypes>()
                    .ToList();

        InitializeComponent();
        BindingContext = this;
    }

    // ----- Label -----
    public static readonly BindableProperty LabelTextProperty =
        BindableProperty.Create(
            nameof(LabelText),
            typeof(string),
            typeof(ResistanceTypePicker),
            "Resistance Type");

    public string LabelText
    {
        get => (string)GetValue(LabelTextProperty);
        set => SetValue(LabelTextProperty, value);
    }

    // ----- Selected Type -----
    public static readonly BindableProperty SelectedTypeProperty =
        BindableProperty.Create(
            nameof(SelectedType),
            typeof(GeneralResistanceTypes?),
            typeof(ResistanceTypePicker),
            null,
            BindingMode.TwoWay);

    public GeneralResistanceTypes? SelectedType
    {
        get => (GeneralResistanceTypes?)GetValue(SelectedTypeProperty);
        set => SetValue(SelectedTypeProperty, value);
    }

    // ----- Enum list -----
    public List<GeneralResistanceTypes> Types { get; }
}
