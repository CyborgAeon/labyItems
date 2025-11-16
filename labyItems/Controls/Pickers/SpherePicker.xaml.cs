using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using labyItems.Models.Enums;

namespace labyItems.Controls
{
    public partial class SpherePicker : ContentView
    {
        public SpherePicker()
        {
            // build enum list before InitializeComponent is fine
            SpiritualSpheres = Enum.GetValues(typeof(SpiritualSpheres))
                                   .Cast<SpiritualSpheres>()
                                   .ToList();

            InitializeComponent();
        }

        // ----- Label -----
        public static readonly BindableProperty LabelTextProperty =
            BindableProperty.Create(
                propertyName: nameof(LabelText),
                returnType: typeof(string),
                declaringType: typeof(SpherePicker),     // FIX: owner type
                defaultValue: "Spiritual Sphere");

        public string LabelText
        {
            get => (string)GetValue(LabelTextProperty);
            set => SetValue(LabelTextProperty, value);
        }

        // ----- Selected Sphere (two-way) -----
        public static readonly BindableProperty SpiritualSphereProperty =
            BindableProperty.Create(
                propertyName: nameof(SpiritualSphere),
                returnType: typeof(SpiritualSpheres?),
                declaringType: typeof(SpherePicker),     // FIX: owner type
                defaultValue: null,
                defaultBindingMode: BindingMode.TwoWay);

        public SpiritualSpheres? SpiritualSphere
        {
            get => (SpiritualSpheres?)GetValue(SpiritualSphereProperty);
            set => SetValue(SpiritualSphereProperty, value);
        }

        // ----- Enum list for the Picker -----
        public List<SpiritualSpheres> SpiritualSpheres { get; }
    }
}
