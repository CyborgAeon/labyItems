namespace labyItems.Controls;

public partial class ExpandableSectionCard : ContentView
{
    public static readonly BindableProperty HeaderTextProperty = BindableProperty.Create(
        nameof(HeaderText),
        typeof(string),
        typeof(ExpandableSectionCard),
        string.Empty);

    public static readonly BindableProperty IsExpandedProperty = BindableProperty.Create(
        nameof(IsExpanded),
        typeof(bool),
        typeof(ExpandableSectionCard),
        true,
        propertyChanged: OnIsExpandedChanged);

    public static readonly BindableProperty BodyContentProperty = BindableProperty.Create(
        nameof(BodyContent),
        typeof(View),
        typeof(ExpandableSectionCard));

    public ExpandableSectionCard()
    {
        InitializeComponent();
    }

    public string HeaderText
    {
        get => (string)GetValue(HeaderTextProperty);
        set => SetValue(HeaderTextProperty, value);
    }

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public View? BodyContent
    {
        get => (View?)GetValue(BodyContentProperty);
        set => SetValue(BodyContentProperty, value);
    }

    public string ChevronText => IsExpanded ? "\uF077" : "\uF078";

    private static void OnIsExpandedChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (ExpandableSectionCard)bindable;
        control.OnPropertyChanged(nameof(ChevronText));
    }

    private void OnHeaderTapped(object? sender, TappedEventArgs e)
    {
        IsExpanded = !IsExpanded;
    }
}
