using System;
using System.Collections.Generic;
using System.Linq;
using labyItems.Helpers;
using Microsoft.Maui.Controls;

namespace labyItems.Controls;

// NOTE: name it something that does NOT conflict with MAUI's Picker
public class EnumPicker<TEnum> : ContentView
    where TEnum : struct, Enum
{
    protected Microsoft.Maui.Controls.Picker? InnerPicker { get; private set; }

    public EnumPicker()
    {
        Options = Enum.GetValues(typeof(TEnum)).Cast<TEnum>().ToList();
    }

    // Expose the enum options to bind to the inner Picker's ItemsSource
    public List<TEnum> Options { get; }
    public static readonly BindableProperty PlaceholderTextProperty = BindableProperty.Create(
        nameof(PlaceholderText),
        typeof(string),
        typeof(EnumPicker<TEnum>),
        defaultValue: string.Empty,
        propertyChanged: OnPlaceholderTextChanged
    );

    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    private static void OnPlaceholderTextChanged(
        BindableObject bindable,
        object oldValue,
        object newValue
    )
    {
        var control = (EnumPicker<TEnum>)bindable;
        control.UpdatePlaceholder();
    }

    private void UpdatePlaceholder()
    {
        if (InnerPicker != null)
        {
            // On MAUI Picker, Title acts as the placeholder text
            InnerPicker.Title = PlaceholderText;
        }
    }

    // Label text for the control ("Magic Colour", "Rage Type", etc.)
    public static readonly BindableProperty LabelTextProperty = BindableProperty.Create(
        nameof(LabelText),
        typeof(string),
        typeof(EnumPicker<TEnum>),
        defaultValue: string.Empty
    );

    public string LabelText
    {
        get => (string)GetValue(LabelTextProperty);
        set => SetValue(LabelTextProperty, value);
    }

    // Selected enum value (nullable)
    public static readonly BindableProperty SelectedValueProperty = BindableProperty.Create(
        nameof(SelectedValue),
        typeof(TEnum?),
        typeof(EnumPicker<TEnum>),
        default(TEnum?),
        BindingMode.TwoWay
    );

    public TEnum? SelectedValue
    {
        get => (TEnum?)GetValue(SelectedValueProperty);
        set => SetValue(SelectedValueProperty, value);
    }

    protected void RegisterInnerPicker(Microsoft.Maui.Controls.Picker picker)
    {
        InnerPicker = picker;
        UpdatePlaceholder();
        AndroidPickerHelper.PreventTypingOpeningPicker(picker);
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (InnerPicker != null)
        {
            AndroidPickerHelper.PreventTypingOpeningPicker(InnerPicker);
        }
    }
}
