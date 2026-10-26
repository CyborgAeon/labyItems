using System.Windows.Input;
using labyItems.Helpers;

namespace labyItems.Controls.Specialisations;

public partial class SpecialisationSectionView : ContentView
{
    public static readonly BindableProperty SectionNumberProperty = BindableProperty.Create(nameof(SectionNumber), typeof(int), typeof(SpecialisationSectionView), 1);
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(nameof(Title), typeof(string), typeof(SpecialisationSectionView), string.Empty, propertyChanged: OnSemanticPropertyChanged);
    public static readonly BindableProperty StatusTextProperty = BindableProperty.Create(nameof(StatusText), typeof(string), typeof(SpecialisationSectionView), string.Empty, propertyChanged: OnSemanticPropertyChanged);
    public static readonly BindableProperty IsExpandedProperty = BindableProperty.Create(nameof(IsExpanded), typeof(bool), typeof(SpecialisationSectionView), true, BindingMode.TwoWay, propertyChanged: OnExpandedChanged);
    public static readonly BindableProperty IsCompleteProperty = BindableProperty.Create(nameof(IsComplete), typeof(bool), typeof(SpecialisationSectionView), false, propertyChanged: OnCompleteChanged);
    public static readonly BindableProperty SuppressCompletionTransitionProperty = BindableProperty.Create(nameof(SuppressCompletionTransition), typeof(bool), typeof(SpecialisationSectionView), false);
    public static readonly BindableProperty AnimationsEnabledProperty = BindableProperty.Create(nameof(AnimationsEnabled), typeof(bool), typeof(SpecialisationSectionView), true);
    public static readonly BindableProperty ToggleCommandProperty = BindableProperty.Create(nameof(ToggleCommand), typeof(ICommand), typeof(SpecialisationSectionView));
    public static readonly BindableProperty SectionContentProperty = BindableProperty.Create(nameof(SectionContent), typeof(View), typeof(SpecialisationSectionView));

    private bool _loaded;
    public int SectionNumber { get => (int)GetValue(SectionNumberProperty); set => SetValue(SectionNumberProperty, value); }
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string StatusText { get => (string)GetValue(StatusTextProperty); set => SetValue(StatusTextProperty, value); }
    public bool IsExpanded { get => (bool)GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    public bool IsComplete { get => (bool)GetValue(IsCompleteProperty); set => SetValue(IsCompleteProperty, value); }
    public bool SuppressCompletionTransition { get => (bool)GetValue(SuppressCompletionTransitionProperty); set => SetValue(SuppressCompletionTransitionProperty, value); }
    public bool AnimationsEnabled { get => (bool)GetValue(AnimationsEnabledProperty); set => SetValue(AnimationsEnabledProperty, value); }
    public ICommand? ToggleCommand { get => (ICommand?)GetValue(ToggleCommandProperty); set => SetValue(ToggleCommandProperty, value); }
    public View? SectionContent { get => (View?)GetValue(SectionContentProperty); set => SetValue(SectionContentProperty, value); }

    public SpecialisationSectionView()
    {
        InitializeComponent();
        Loaded += (_, _) => { _loaded = true; ApplyCompleteState(false); ApplyExpandedState(false); };
        Unloaded += (_, _) => AbortAnimations();
    }

    private void OnHeaderTapped(object? sender, TappedEventArgs e)
    {
        if (ToggleCommand?.CanExecute(null) == true) ToggleCommand.Execute(null);
        else IsExpanded = !IsExpanded;
    }

    private static void OnCompleteChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (SpecialisationSectionView)bindable;
        view.ApplyCompleteState(view._loaded && view.AnimationsEnabled && !view.SuppressCompletionTransition);
    }
    private static void OnExpandedChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (SpecialisationSectionView)bindable;
        view.ApplyExpandedState(view._loaded && view.AnimationsEnabled);
    }
    private static void OnSemanticPropertyChanged(BindableObject bindable, object oldValue, object newValue)
        => ((SpecialisationSectionView)bindable).ApplySemantics();

    private async void ApplyCompleteState(bool animate)
    {
        AbortAnimations();
        var success = Color.FromArgb("#2E7D32");
        Indicator.BackgroundColor = IsComplete ? success : (Color)Application.Current!.Resources["Gray500"];
        Header.BackgroundColor = IsComplete ? Color.FromArgb("#E8F5E9") : Colors.Transparent;
        if (!animate)
        {
            NumberLabel.Opacity = IsComplete ? 0 : 1; CheckLabel.Opacity = IsComplete ? 1 : 0; Indicator.Rotation = 0;
        }
        else
        {
            await Task.WhenAll(
                Indicator.RotateTo(IsComplete ? 180 : -180, 110, Easing.CubicIn),
                NumberLabel.FadeTo(IsComplete ? 0 : 1, 110),
                CheckLabel.FadeTo(IsComplete ? 1 : 0, 110));
            NumberLabel.Opacity = IsComplete ? 0 : 1;
            CheckLabel.Opacity = IsComplete ? 1 : 0;
            Indicator.Rotation = 0;
        }
        ApplySemantics();
    }

    private async void ApplyExpandedState(bool animate)
    {
        if (!animate) { Chevron.Rotation = IsExpanded ? 90 : 0; }
        else await Chevron.RotateTo(IsExpanded ? 90 : 0, CardExpandAnimationHelper.UnifiedDurationMs, Easing.CubicInOut);
        ApplySemantics();
    }

    private void ApplySemantics() => SemanticProperties.SetDescription(Header,
        $"Section {SectionNumber}, {Title}, {StatusText}, {(IsComplete ? "complete" : "incomplete")}, {(IsExpanded ? "expanded" : "collapsed")}");
    private void AbortAnimations() { Indicator?.AbortAnimation("RotationTo"); NumberLabel?.AbortAnimation("FadeTo"); CheckLabel?.AbortAnimation("FadeTo"); Chevron?.AbortAnimation("RotationTo"); }
}
