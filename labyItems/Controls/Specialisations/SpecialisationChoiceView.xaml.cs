using System.Windows.Input;

namespace labyItems.Controls.Specialisations;

public partial class SpecialisationChoiceView : ContentView
{
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(SpecialisationChoiceView), string.Empty,
        propertyChanged: (bindable, _, _) => ((SpecialisationChoiceView)bindable).ApplySemantics());

    public static readonly BindableProperty IsSelectedProperty = BindableProperty.Create(
        nameof(IsSelected), typeof(bool), typeof(SpecialisationChoiceView), false,
        propertyChanged: (bindable, _, _) => ((SpecialisationChoiceView)bindable).ApplyVisualState());

    public static readonly BindableProperty SecondaryTextProperty = BindableProperty.Create(
        nameof(SecondaryText), typeof(string), typeof(SpecialisationChoiceView), string.Empty,
        propertyChanged: (bindable, _, _) => ((SpecialisationChoiceView)bindable).ApplySemantics());

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command), typeof(ICommand), typeof(SpecialisationChoiceView));

    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(
        nameof(CommandParameter), typeof(object), typeof(SpecialisationChoiceView));

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public bool IsSelected { get => (bool)GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
    public string SecondaryText { get => (string)GetValue(SecondaryTextProperty); set => SetValue(SecondaryTextProperty, value); }
    public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

    public event EventHandler? SelectionRequested;

    public SpecialisationChoiceView()
    {
        InitializeComponent();
        ApplyVisualState();
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == IsEnabledProperty.PropertyName)
            ApplyVisualState();
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        if (!IsEnabled)
            return;

        SelectionRequested?.Invoke(this, EventArgs.Empty);
        if (Command?.CanExecute(CommandParameter) == true)
            Command.Execute(CommandParameter);
    }

    private void ApplyVisualState()
    {
        if (ChoiceBorder == null)
            return;

        ChoiceBorder.Opacity = IsEnabled ? 1 : 0.5;
        ChoiceBorder.Stroke = IsSelected
            ? new SolidColorBrush((Color)Application.Current!.Resources["AccentMaroonColor"])
            : new SolidColorBrush((Color)Application.Current!.Resources["SurfaceBorderColor"]);
        ChoiceBorder.BackgroundColor = IsSelected
            ? (Color)Application.Current!.Resources["SelectedSurfaceBackgroundColor"]
            : (Color)Application.Current!.Resources["White"];
        ApplySemantics();
    }

    private void ApplySemantics()
    {
        var availability = IsEnabled ? string.Empty : ", unavailable";
        var detail = string.IsNullOrWhiteSpace(SecondaryText) ? string.Empty : $", {SecondaryText}";
        SemanticProperties.SetDescription(this, $"{Text}{detail}, {(IsSelected ? "selected" : "not selected")}{availability}");
    }
}
