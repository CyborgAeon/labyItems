using System.Collections.Specialized;
using Microsoft.Maui.ApplicationModel;
using labyItems.Infrastructure;
using Microsoft.Maui.Controls.Shapes;

namespace labyItems.Controls;

public class StepItem
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
}

public partial class StepIndicator : ContentView
{
    public StepIndicator()
    {
        InitializeComponent();
        ApplyThemeDefaults();
        SizeChanged += (_, __) => UpdateProgressLine();
        StepsGrid.SizeChanged += (_, __) => UpdateProgressLine();
        BindingContextChanged += (_, __) =>
            MainThread.BeginInvokeOnMainThread(Rebuild);
    }

    public static readonly BindableProperty StepsProperty =
        BindableProperty.Create(
            nameof(Steps),
            typeof(IList<StepItem>),
            typeof(StepIndicator),
            defaultValue: Array.Empty<StepItem>(),
            propertyChanged: (b, o, n) =>
                ((StepIndicator)b).OnStepsChanged(o as IList<StepItem>, n as IList<StepItem>));

    private INotifyCollectionChanged? _stepsNotify;

    public IList<StepItem> Steps
    {
        get => (GetValue(StepsProperty) as IList<StepItem>) ?? Array.Empty<StepItem>();
        set => SetValue(StepsProperty, value);
    }

    public static readonly BindableProperty CurrentStepProperty =
        BindableProperty.Create(
            nameof(CurrentStep),
            typeof(int),
            typeof(StepIndicator),
            0,
            propertyChanged: (b, o, n) =>
                ((StepIndicator)b).UpdateVisualStates());

    public int CurrentStep
    {
        get => (int)GetValue(CurrentStepProperty);
        set => SetValue(CurrentStepProperty, value);
    }

    public static readonly BindableProperty StepClickCommandProperty =
        BindableProperty.Create(
            nameof(StepClickCommand),
            typeof(Command<int>),
            typeof(StepIndicator),
            defaultValue: null);

    public Command<int>? StepClickCommand
    {
        get => (Command<int>?)GetValue(StepClickCommandProperty);
        set => SetValue(StepClickCommandProperty, value);
    }

    public static readonly BindableProperty BubbleSizeProperty =
        BindableProperty.Create(
            nameof(BubbleSize),
            typeof(double),
            typeof(StepIndicator),
            40d,
            propertyChanged: (b, o, n) =>
                ((StepIndicator)b).Rebuild());

    public double BubbleSize
    {
        get => (double)GetValue(BubbleSizeProperty);
        set => SetValue(BubbleSizeProperty, value);
    }

    public static readonly BindableProperty StepSpacingProperty =
        BindableProperty.Create(
            nameof(StepSpacing),
            typeof(double),
            typeof(StepIndicator),
            22d, // adjust to taste
            propertyChanged: (b, o, n) =>
                ((StepIndicator)b).Rebuild());

    public double StepSpacing
    {
        get => (double)GetValue(StepSpacingProperty);
        set => SetValue(StepSpacingProperty, value);
    }

    public event EventHandler<int>? StepClicked;

    private sealed class StepVisual
    {
        public required Grid Overlay;
        public required Border Halo;
        public required Button Bubble;
        public required Label BubbleText;
        public required Label Caption;
        public required int Index;
    }

    private readonly List<StepVisual> _items = new();

    private void OnStepsChanged(IList<StepItem>? oldSteps, IList<StepItem>? newSteps)
    {
        if (_stepsNotify != null)
            _stepsNotify.CollectionChanged -= Steps_CollectionChanged;

        _stepsNotify = newSteps as INotifyCollectionChanged;

        if (_stepsNotify != null)
            _stepsNotify.CollectionChanged += Steps_CollectionChanged;

        Rebuild();
    }

    private void Steps_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => MainThread.BeginInvokeOnMainThread(Rebuild);

    private void ApplyThemeDefaults()
    {
        LineBg.Color = TryGetColor("BorderColor", Colors.LightGray);
        LineFg.Color = TryGetColor("PrimaryColor", ColourScheme.Primary);
    }

    private Color TryGetColor(string key, Color fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var v) == true && v is Color c)
            return c;

        return fallback;
    }

    private void ClearDynamicChildren()
    {
        for (int i = StepsGrid.Children.Count - 1; i >= 0; i--)
        {
            var child = StepsGrid.Children[i];
            if (child == LineBg || child == LineFg)
                continue;

            StepsGrid.Children.RemoveAt(i);
        }
    }

    private void Rebuild()
    {
        ClearDynamicChildren();
        StepsGrid.ColumnDefinitions.Clear();
        _items.Clear();

        var steps = Steps ?? Array.Empty<StepItem>();
        if (steps.Count == 0)
        {
            UpdateProgressLine();
            return;
        }

        StepsGrid.RowDefinitions.Clear();
        StepsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        StepsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        StepsGrid.ColumnSpacing = StepSpacing;

        for (int i = 0; i < steps.Count; i++)
            StepsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        Grid.SetColumn(LineBg, 0);
        Grid.SetColumnSpan(LineBg, steps.Count);
        Grid.SetColumn(LineFg, 0);
        Grid.SetColumnSpan(LineFg, steps.Count);

        var haloDiameter = BubbleSize + 14;

        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];

            var halo = new Border
            {
                WidthRequest = haloDiameter,
                HeightRequest = haloDiameter,
                StrokeThickness = 2,
                Stroke = new SolidColorBrush(Colors.Transparent),
                BackgroundColor = Colors.Transparent,
                Opacity = 0,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                InputTransparent = true,
                StrokeShape = new RoundRectangle
                {
                    CornerRadius = (float)(haloDiameter / 2)
                }
            };

            var bubble = new Button
            {
                WidthRequest = BubbleSize,
                HeightRequest = BubbleSize,
                CornerRadius = (int)(BubbleSize / 2),
                Padding = 0,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };

            var bubbleText = new Label
            {
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                InputTransparent = true
            };

            var overlay = new Grid
            {
                WidthRequest = haloDiameter,
                HeightRequest = haloDiameter,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };

            // Order matters: halo behind, bubble middle, text on top
            overlay.Children.Add(halo);
            overlay.Children.Add(bubble);
            overlay.Children.Add(bubbleText);

            var caption = new Label
            {
                Text = step.Label,
                FontSize = 12,
                LineBreakMode = LineBreakMode.NoWrap,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Start,
                Margin = new Thickness(0, 5, 0, 0)
            };

            var stepIndex = i;
            bubble.Clicked += async (_, __) =>
            {
                // Keep your existing rule: can only click current or previous
                if (stepIndex > CurrentStep) return;

                await overlay.ScaleTo(0.97, 70, Easing.CubicInOut);
                await overlay.ScaleTo(1.00, 70, Easing.CubicInOut);

                StepClicked?.Invoke(this, stepIndex);
                StepClickCommand?.Execute(stepIndex);
            };

            StepsGrid.Add(overlay, i, 0);
            StepsGrid.Add(caption, i, 1);

            _items.Add(new StepVisual
            {
                Overlay = overlay,
                Halo = halo,
                Bubble = bubble,
                BubbleText = bubbleText,
                Caption = caption,
                Index = i
            });
        }

        UpdateVisualStates();

        // Ensure line updates after layout positions settle
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(1);
            UpdateProgressLine();
        });
    }

    private void UpdateVisualStates()
    {
        var count = Steps?.Count ?? 0;
        if (count == 0 || _items.Count == 0)
        {
            UpdateProgressLine();
            return;
        }

        // Clamp to valid range
        var current = Math.Max(0, Math.Min(CurrentStep, count - 1));
        if (current != CurrentStep)
            CurrentStep = current;

        var primary = TryGetColor("PrimaryColor", ColourScheme.Primary);
        var primaryText = TryGetColor("PrimaryForegroundColor", Colors.White);

        var secondary = TryGetColor("SecondaryColor", Colors.Gainsboro);
        var mutedText = TryGetColor("MutedForegroundColor", Colors.Gray);

        for (int i = 0; i < _items.Count; i++)
        {
            var v = _items[i];

            // Stop any previous halo animation when state changes
            v.Halo.AbortAnimation("halo");
            v.Halo.Opacity = 0;
            v.Halo.Scale = 1;

            var isCompleted = v.Index < CurrentStep;
            var isCurrent = v.Index == CurrentStep;
            var isClickable = v.Index <= CurrentStep;

            if (isCompleted || isCurrent)
            {
                v.Bubble.BackgroundColor = primary;
                v.BubbleText.TextColor = primaryText;
            }
            else
            {
                v.Bubble.BackgroundColor = secondary;
                v.BubbleText.TextColor = mutedText;
            }

            if (isCompleted)
            {
                v.BubbleText.Text = "✓";
                v.BubbleText.FontSize = 18;
            }
            else
            {
                v.BubbleText.Text = (v.Index + 1).ToString();
                v.BubbleText.FontSize = 14;
            }

            v.Caption.TextColor = isCurrent ? primary : mutedText;
            v.Caption.FontAttributes = isCurrent ? FontAttributes.Bold : FontAttributes.None;

            v.Bubble.IsEnabled = isClickable;
            v.Bubble.Opacity = isClickable ? 1.0 : 0.6;

            if (isCurrent)
                StartHaloPulse(v.Halo, primary);
        }

        UpdateProgressLine();
    }

    private void StartHaloPulse(VisualElement halo, Color primary)
    {
        if (halo.AnimationIsRunning("halo")) return;

        if (halo is Border b)
            b.Stroke = new SolidColorBrush(primary);

        var animation = new Animation();
        animation.Add(0.00, 0.50, new Animation(v => halo.Opacity = v, 0.00, 0.35, Easing.CubicInOut));
        animation.Add(0.50, 1.00, new Animation(v => halo.Opacity = v, 0.35, 0.00, Easing.CubicInOut));
        animation.Add(0.00, 0.50, new Animation(v => halo.Scale = v, 1.00, 1.08, Easing.CubicInOut));
        animation.Add(0.50, 1.00, new Animation(v => halo.Scale = v, 1.08, 1.00, Easing.CubicInOut));

        halo.Animate("halo", animation, length: 1100, repeat: () => halo.IsVisible);
    }

    private void UpdateProgressLine()
    {
        var count = Steps?.Count ?? 0;
        if (count <= 1 || _items.Count < 2)
        {
            LineFg.WidthRequest = 0;
            return;
        }

        if (StepsGrid.Width <= 0) return;

        var first = _items[0].Overlay;
        var last = _items[count - 1].Overlay;
        if (first.Width <= 0 || last.Width <= 0) return;

        var firstCenter = first.X + (first.Width / 2);
        var lastCenter = last.X + (last.Width / 2);
        var currentIndex = Math.Max(0, Math.Min(CurrentStep, count - 1));
        var current = _items[currentIndex].Overlay;
        if (current.Width <= 0) return;
        var currentCenter = current.X + (current.Width / 2);

        var leftMargin = Math.Max(0, firstCenter);
        var rightMargin = Math.Max(0, StepsGrid.Width - lastCenter);
        LineBg.Margin = new Thickness(leftMargin, 0, rightMargin, 0);
        LineFg.Margin = new Thickness(leftMargin, 0, 0, 0);
        var fgWidth = Math.Max(0, Math.Min(lastCenter - firstCenter, currentCenter - firstCenter));
        LineFg.WidthRequest = fgWidth;
    }
}
