using System;
using System.Collections.Generic;
using System.Linq;
using labyItems.Models.Enums; 
namespace labyItems.Controls;

public partial class ElfInnateLevelPicker : ContentView
{
    public ElfInnateLevelPicker()
    {
        Levels = Enum.GetValues(typeof(ElfInnateLevel))
                     .Cast<ElfInnateLevel>()
                     .ToList();

        InitializeComponent();
    }

    // ----- Label -----
    public static readonly BindableProperty LabelTextProperty =
        BindableProperty.Create(
            nameof(LabelText),
            typeof(string),
            typeof(ElfInnateLevelPicker),
            "Elf Innate Level");

    public string LabelText
    {
        get => (string)GetValue(LabelTextProperty);
        set => SetValue(LabelTextProperty, value);
    }

    // ----- Selected Level -----
    public static readonly BindableProperty SelectedLevelProperty =
        BindableProperty.Create(
            nameof(SelectedLevel),
            typeof(ElfInnateLevel?),
            typeof(ElfInnateLevelPicker),
            null,
            BindingMode.TwoWay);

    public ElfInnateLevel? SelectedLevel
    {
        get => (ElfInnateLevel?)GetValue(SelectedLevelProperty);
        set => SetValue(SelectedLevelProperty, value);
    }

    // ----- Enum list -----
    public List<ElfInnateLevel> Levels { get; }
}
