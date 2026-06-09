using Microsoft.Maui.Controls;

namespace labyItems.Controls;

public partial class LoadingIndicator : ContentView
{
    public static readonly BindableProperty IsRunningProperty =
        BindableProperty.Create(
            nameof(IsRunning),
            typeof(bool),
            typeof(LoadingIndicator),
            true,
            propertyChanged: (bindable, _, _) => ((LoadingIndicator)bindable).ApplyState());

    public static readonly BindableProperty MessageProperty =
        BindableProperty.Create(
            nameof(Message),
            typeof(string),
            typeof(LoadingIndicator),
            string.Empty,
            propertyChanged: (bindable, _, _) => ((LoadingIndicator)bindable).ApplyState());

    public static readonly BindableProperty IndicatorSizeProperty =
        BindableProperty.Create(
            nameof(IndicatorSize),
            typeof(double),
            typeof(LoadingIndicator),
            32d,
            propertyChanged: (bindable, _, _) => ((LoadingIndicator)bindable).ApplyState());

    public LoadingIndicator()
    {
        InitializeComponent();
        ApplyState();
    }

    public bool IsRunning
    {
        get => (bool)GetValue(IsRunningProperty);
        set => SetValue(IsRunningProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public double IndicatorSize
    {
        get => (double)GetValue(IndicatorSizeProperty);
        set => SetValue(IndicatorSizeProperty, value);
    }

    private void ApplyState()
    {
        if (Spinner == null || MessageLabel == null)
            return;

        Spinner.IsRunning = IsRunning;
        Spinner.IsVisible = IsRunning;
        Spinner.WidthRequest = IndicatorSize;
        Spinner.HeightRequest = IndicatorSize;

        var message = (Message ?? string.Empty).Trim();
        MessageLabel.Text = message;
        MessageLabel.IsVisible = message.Length > 0;
    }
}
