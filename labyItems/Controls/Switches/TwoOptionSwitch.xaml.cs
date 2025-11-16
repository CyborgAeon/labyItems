using Microsoft.Maui.Controls;

namespace labyItems.Controls;

public partial class TwoOptionSwitch : ContentView
{
    public TwoOptionSwitch()
    {
        InitializeComponent();
        UpdateSwitchFromSelectedValue();
    }

    #region Bindable Properties

    // Text shown to the left of the switch
    // public static readonly BindableProperty LeftTextProperty = BindableProperty.Create(
    //     nameof(LeftText),
    //     typeof(string),
    //     typeof(TwoOptionSwitch),
    //     defaultValue: "Left"
    // );

    // public string LeftText
    // {
    //     get => (string)GetValue(LeftTextProperty);
    //     set => SetValue(LeftTextProperty, value);
    // }

    // // Text shown to the right of the switch
    // public static readonly BindableProperty RightTextProperty =
    //     BindableProperty.Create(
    //         nameof(RightText),
    //         typeof(string),
    //         typeof(TwoOptionSwitch),
    //         defaultValue: "Right");

    // public string RightText
    // {
    //     get => (string)GetValue(RightTextProperty);
    //     set => SetValue(RightTextProperty, value);
    // }

    // Logical value when switch is OFF (left side)
    public static readonly BindableProperty LeftValueProperty = BindableProperty.Create(
        nameof(LeftValue),
        typeof(string),
        typeof(TwoOptionSwitch),
        defaultValue: "left",
        propertyChanged: OnValueMappingChanged
    );

    public string LeftValue
    {
        get => (string)GetValue(LeftValueProperty);
        set => SetValue(LeftValueProperty, value);
    }

    // Logical value when switch is ON (right side)
    public static readonly BindableProperty RightValueProperty = BindableProperty.Create(
        nameof(RightValue),
        typeof(string),
        typeof(TwoOptionSwitch),
        defaultValue: "right",
        propertyChanged: OnValueMappingChanged
    );

    public string RightValue
    {
        get => (string)GetValue(RightValueProperty);
        set => SetValue(RightValueProperty, value);
    }

    // The selected logical value ("repel", "attract", etc.)
    public static readonly BindableProperty SelectedValueProperty = BindableProperty.Create(
        nameof(SelectedValue),
        typeof(string),
        typeof(TwoOptionSwitch),
        defaultValue: null,
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: OnSelectedValueChanged
    );

    public string SelectedValue
    {
        get => (string)GetValue(SelectedValueProperty);
        set => SetValue(SelectedValueProperty, value);
    }

    private static void OnSelectedValueChanged(
        BindableObject bindable,
        object oldValue,
        object newValue
    )
    {
        var control = (TwoOptionSwitch)bindable;
        control.UpdateSwitchFromSelectedValue();
    }

    private static void OnValueMappingChanged(
        BindableObject bindable,
        object oldValue,
        object newValue
    )
    {
        var control = (TwoOptionSwitch)bindable;
        control.UpdateSwitchFromSelectedValue();
    }

    #endregion

    #region Internal wiring

    private void InnerSwitch_OnToggled(object sender, ToggledEventArgs e)
    {
        // Switch ON → RightValue; OFF → LeftValue
        SelectedValue = e.Value ? RightValue : LeftValue;
    }

    private void UpdateSwitchFromSelectedValue()
    {
        if (InnerSwitch == null)
            return;

        // If SelectedValue matches RightValue → switch ON, else OFF.
        // If SelectedValue is null, default to LeftValue.
        if (SelectedValue == null)
        {
            InnerSwitch.IsToggled = false;
            return;
        }

        InnerSwitch.IsToggled = string.Equals(
            SelectedValue,
            RightValue,
            StringComparison.OrdinalIgnoreCase
        );
    }

    #endregion
}
