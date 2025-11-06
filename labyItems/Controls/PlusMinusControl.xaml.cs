namespace labyItems.Controls;

public partial class PlusMinusControl : ContentView
{
    public PlusMinusControl()
    {
        InitializeComponent();
    }

    // ---------- Label ----------
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

    // ---------- Allow Multiple ----------
    public static readonly BindableProperty AllowMultipleProperty =
        BindableProperty.Create(
            nameof(AllowMultiple),
            typeof(bool),
            typeof(PlusMinusControl),
            false);

    public bool AllowMultiple
    {
        get => (bool)GetValue(AllowMultipleProperty);
        set => SetValue(AllowMultipleProperty, value);
    }

    // ---------- Count ----------
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

    // ---------- Min ----------
    public static readonly BindableProperty MinProperty =
        BindableProperty.Create(
            nameof(Min),
            typeof(int),
            typeof(PlusMinusControl),
            0); // default lower bound

    public int Min
    {
        get => (int)GetValue(MinProperty);
        set => SetValue(MinProperty, value);
    }

    // ---------- Max ----------
    public static readonly BindableProperty MaxProperty =
        BindableProperty.Create(
            nameof(Max),
            typeof(int?),
            typeof(PlusMinusControl),
            null); // optional upper bound

    public int? Max
    {
        get => (int?)GetValue(MaxProperty);
        set => SetValue(MaxProperty, value);
    }

    // ---------- Button Handlers ----------
    private void OnMinus(object sender, EventArgs e)
    {
        if (Count > Min) Count--;
    }

    private void OnPlus(object sender, EventArgs e)
    {
        // respect AllowMultiple
        if (!AllowMultiple && Count >= 1)
            return;

        // enforce Max (if defined)
        if (Max.HasValue && Count >= Max.Value)
            return;

        Count++;
    }
}
