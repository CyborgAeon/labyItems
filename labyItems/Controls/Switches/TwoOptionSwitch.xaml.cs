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
    public static readonly BindableProperty AllowNullProperty = BindableProperty.Create(
        nameof(AllowNull),
        typeof(bool),
        typeof(TwoOptionSwitch),
        defaultValue: false
    );

    public bool AllowNull
    {
        get => (bool)GetValue(AllowNullProperty);
        set => SetValue(AllowNullProperty, value);
    }

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
        if (!AllowNull)
        {
            SelectedValue = e.Value ? RightValue : LeftValue;
            return;
        }

        SelectedValue = SelectedValue switch
        {
            null => RightValue,
            var v when v == RightValue => LeftValue,
            _ => null, // covers LeftValue and any other value
        };
    }

    private void UpdateSwitchFromSelectedValue()
    {
        if (InnerSwitch == null)
            return;

        InnerSwitch.IsToggled =
            SelectedValue != null
            && string.Equals(SelectedValue, RightValue, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
