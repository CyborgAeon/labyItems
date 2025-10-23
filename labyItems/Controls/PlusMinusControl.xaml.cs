namespace labyItems.Controls;

public partial class PlusMinusControl : ContentView
{
    public PlusMinusControl()
    {
        InitializeComponent();
    }

    public static readonly BindableProperty LabelTextProperty =
            BindableProperty.Create(
                nameof(LabelText),
                typeof(string),
                typeof(PlusMinusControl),
                string.Empty);

    public string LabelText
    {
        get => (string)GetValue(LabelTextProperty);
        set => SetValue(LabelTextProperty, value);
    }

    public static readonly BindableProperty AllowMultipleProperty =
        BindableProperty.Create(nameof(AllowMultiple), typeof(bool), typeof(PlusMinusControl), false);

    public bool AllowMultiple
    {
        get => (bool)GetValue(AllowMultipleProperty);
        set => SetValue(AllowMultipleProperty, value);
    }

    public static readonly BindableProperty CountProperty =
        BindableProperty.Create(
            nameof(Count),
            typeof(int),
            typeof(PlusMinusControl),
            0,
            defaultBindingMode: BindingMode.TwoWay);

    public int Count
    {
        get => (int)GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    private void OnMinus(object sender, EventArgs e)
    {
        if (Count > 0)
            Count--;
    }

    private void OnPlus(object sender, EventArgs e)
    {
        if (!AllowMultiple && Count >= 1)
            return;
        Count++;
    }
}
