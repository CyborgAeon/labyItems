using System.Windows.Input;

namespace labyItems.Controls;

public partial class ThreeOptionSelector : Grid
{
    public ThreeOptionSelector()
    {
        InitializeComponent();
        SelectCommand = new Command<string>(OnSelect);
        UpdateVisualState();
    }

    // SelectedIndex: -1 = none, 0/1/2 = selected option
    public static readonly BindableProperty SelectedIndexProperty =
        BindableProperty.Create(
            nameof(SelectedIndex),
            typeof(int),
            typeof(ThreeOptionSelector),
            -1,
            BindingMode.TwoWay,
            propertyChanged: OnSelectedIndexChanged);

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    // Option labels
    public static readonly BindableProperty Option0TextProperty =
        BindableProperty.Create(nameof(Option0Text), typeof(string), typeof(ThreeOptionSelector), string.Empty);

    public string Option0Text
    {
        get => (string)GetValue(Option0TextProperty);
        set => SetValue(Option0TextProperty, value);
    }

    public static readonly BindableProperty Option1TextProperty =
        BindableProperty.Create(nameof(Option1Text), typeof(string), typeof(ThreeOptionSelector), string.Empty);

    public string Option1Text
    {
        get => (string)GetValue(Option1TextProperty);
        set => SetValue(Option1TextProperty, value);
    }

    public static readonly BindableProperty Option2TextProperty =
        BindableProperty.Create(nameof(Option2Text), typeof(string), typeof(ThreeOptionSelector), string.Empty);

    public string Option2Text
    {
        get => (string)GetValue(Option2TextProperty);
        set => SetValue(Option2TextProperty, value);
    }

    // Command used by buttons
    public ICommand SelectCommand { get; }

    private static void OnSelectedIndexChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (ThreeOptionSelector)bindable;
        control.UpdateVisualState();
    }

    private void OnSelect(string parameter)
    {
        if (!int.TryParse(parameter, out var index))
            return;

        // Clicking again deselects (toggle off)
        SelectedIndex = SelectedIndex == index ? -1 : index;
    }

    private void UpdateVisualState()
    {
        // super-basic visual feedback – you can style this however you like
        Btn0.Opacity = SelectedIndex == 0 ? 1.0 : 0.5;
        Btn1.Opacity = SelectedIndex == 1 ? 1.0 : 0.5;
        Btn2.Opacity = SelectedIndex == 2 ? 1.0 : 0.5;
    }
}
