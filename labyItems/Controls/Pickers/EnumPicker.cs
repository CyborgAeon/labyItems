using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using labyItems.Helpers;
using Microsoft.Maui.Controls;

namespace labyItems.Controls;

public class EnumPicker<TEnum> : ContentView
    where TEnum : struct, Enum
{
    protected Picker? InnerPicker { get; private set; }

    public EnumPicker()
    {
        Options = Enum.GetValues(typeof(TEnum)).Cast<TEnum>().ToList();
    }

    // Expose the enum options to bind to the inner Picker's ItemsSource
    public List<TEnum> Options { get; }

    // NEW: Optional custom formatter for displaying enum values
    public Func<TEnum, string>? DisplayFormatter { get; set; }

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
            InnerPicker.Title = PlaceholderText;
        }
    }

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

    protected void RegisterInnerPicker(Picker picker)
    {
        InnerPicker = picker;
        UpdatePlaceholder();
        AndroidPickerHelper.PreventTypingOpeningPicker(picker);

        // Hook up a single generic converter that uses DisplayFormatter
        InnerPicker.ItemDisplayBinding = new Binding(".")
        {
            Converter = new EnumDisplayConverter<TEnum>(this),
        };
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

// Single generic converter for all EnumPicker<TEnum>
public class EnumDisplayConverter<TEnum> : IValueConverter
    where TEnum : struct, Enum
{
    private readonly EnumPicker<TEnum> _owner;

    public EnumDisplayConverter(EnumPicker<TEnum> owner)
    {
        _owner = owner;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TEnum enumValue)
        {
            // Use custom formatter if provided; fall back to ToString()
            var formatter = _owner.DisplayFormatter;
            return formatter != null ? formatter(enumValue) : enumValue.ToString();
        }

        return string.Empty;
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}
