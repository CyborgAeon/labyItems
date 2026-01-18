using System.Collections.ObjectModel;
using labyItems.Infrastructure;
using labyItems.Pages.Characters;
using System.Collections.Specialized;
using Microsoft.Maui.ApplicationModel;

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
        SizeChanged += (_, __) => UpdateProgressLine();
        StepsGrid.SizeChanged += (_, __) => UpdateProgressLine();
        ApplyThemeDefaults();

        BindingContextChanged += (_, __) => MainThread.BeginInvokeOnMainThread(Rebuild);
    }

    public static readonly BindableProperty StepsProperty =
        BindableProperty.Create(
            nameof(Steps),
            typeof(IList<StepItem>),
            typeof(StepIndicator),
            defaultValue: Array.Empty<StepItem>(),
            propertyChanged: (b, o, n) => ((StepIndicator)b).OnStepsChanged(o as IList<StepItem>, n as IList<StepItem>));

    private INotifyCollectionChanged? _stepsNotify;

    private void OnStepsChanged(IList<StepItem>? oldSteps, IList<StepItem>? newSteps)
    {
        // Unhook old
        if (_stepsNotify != null)
            _stepsNotify.CollectionChanged -= Steps_CollectionChanged;

        // Hook new (if observable)
        _stepsNotify = newSteps as INotifyCollectionChanged;
        if (_stepsNotify != null)
            _stepsNotify.CollectionChanged += Steps_CollectionChanged;

        Rebuild();
    }

    private void Steps_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Always marshal to UI thread
        MainThread.BeginInvokeOnMainThread(Rebuild);
    }

    public IList<StepItem> Steps
    {
        get => (GetValue(StepsProperty) as IList<StepItem>) ?? Array.Empty<StepItem>();
        set => SetValue(StepsProperty, value);
    }

    // 0-based index (matches your React logic)
    public static readonly BindableProperty CurrentStepProperty =
        BindableProperty.Create(
            nameof(CurrentStep),
            typeof(int),
            typeof(StepIndicator),
            defaultValue: 0,
            propertyChanged: (b, o, n) => ((StepIndicator)b).UpdateVisualStates());

    public int CurrentStep
    {
        get => (int)GetValue(CurrentStepProperty);
        set => SetValue(CurrentStepProperty, value);
    }


    public event EventHandler<int>? StepClicked;

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
        BindableProperty.Create(nameof(BubbleSize), typeof(double), typeof(StepIndicator), 40d,
            propertyChanged: (b, o, n) => ((StepIndicator)b).UpdateVisualStates());

    public double BubbleSize
    {
        get => (double)GetValue(BubbleSizeProperty);
        set => SetValue(BubbleSizeProperty, value);
    }

    // Internals
    private readonly List<(Button bubble, Label bubbleText, Label label, int index)> _items = new();

    private void ApplyThemeDefaults()
    {
        // Map "bg-border", "bg-primary", etc. to MAUI theme resources if you have them.
        // Fallbacks here if resources don't exist.
        LineBg.Color = TryGetColor("BorderColor", Colors.LightGray);
        LineFg.Color = TryGetColor("PrimaryColor", ColourScheme.Primary);
    }

    private Color TryGetColor(string key, Color fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var v) == true && v is Color c)
            return c;

        return fallback;
    }

    private void Rebuild()
    {
        // Remove only dynamically-added children (keep the line BoxViews)
        for (int i = StepsGrid.Children.Count - 1; i >= 0; i--)
        {
            var child = StepsGrid.Children[i];
            if (child == LineBg || child == LineFg)
                continue;

            StepsGrid.Children.RemoveAt(i);
        }

        StepsGrid.ColumnDefinitions.Clear();
        _items.Clear();

        var steps = Steps ?? Array.Empty<StepItem>();
        if (steps.Count == 0)
        {
            UpdateProgressLine();
            return;
        }

        // Ensure row structure exists (line + bubbles in row 0, captions in row 1)
        StepsGrid.RowDefinitions.Clear();
        StepsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        StepsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        // Create N equal columns
        for (int i = 0; i < steps.Count; i++)
            StepsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        // Create step UI per column
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];

            // Bubble button
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
                FontAttributes = FontAttributes.Bold
            };

            var bubbleOverlay = new Grid
            {
                WidthRequest = BubbleSize,
                HeightRequest = BubbleSize,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };
            bubbleOverlay.Children.Add(bubble);
            bubbleOverlay.Children.Add(bubbleText);

            var caption = new Label
            {
                Text = step.Label,
                FontSize = 12,
                LineBreakMode = LineBreakMode.NoWrap,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Start,
                Margin = new Thickness(0, 8, 0, 0)
            };

            var stepIndex = i;
            bubble.Clicked += async (_, __) =>
            {
                if (stepIndex > CurrentStep) return;
                await bubbleOverlay.ScaleTo(0.95, 70, Easing.CubicInOut);
                await bubbleOverlay.ScaleTo(1.00, 70, Easing.CubicInOut);

                StepClicked?.Invoke(this, stepIndex);
                StepClickCommand?.Execute(stepIndex);
            };

            // IMPORTANT: add to StepsGrid directly (no container)
            StepsGrid.Add(bubbleOverlay, i, 0); // row 0 aligns with the line
            StepsGrid.Add(caption, i, 1);       // row 1

            _items.Add((bubble, bubbleText, caption, i));
            _ = AnimateAppear(bubbleOverlay, i);
            _ = AnimateAppear(caption, i);
        }

        UpdateVisualStates();
    }

    private async Task AnimateAppear(VisualElement element, int index)
    {
        try
        {
            element.Opacity = 0;
            element.Scale = 0.9;
            await Task.Delay(index * 80);
            await Task.WhenAll(
                element.FadeTo(1, 180, Easing.CubicOut),
                element.ScaleTo(1, 180, Easing.CubicOut)
            );
        }
        catch
        {
            // ignore if disposed during navigation
        }
    }

    private void UpdateVisualStates()
    {
        var stepsCount = Steps?.Count ?? 0;
        if (stepsCount == 0)
        {
            UpdateProgressLine();
            return;
        }

        // Clamp current step
        var current = Math.Max(0, Math.Min(CurrentStep, stepsCount - 1));
        if (current != CurrentStep) CurrentStep = current;

        var primary = TryGetColor("PrimaryColor", ColourScheme.Primary);
        var primaryText = TryGetColor("PrimaryForegroundColor", Colors.White);

        var secondary = TryGetColor("SecondaryColor", Colors.Gainsboro);
        var mutedText = TryGetColor("MutedForegroundColor", Colors.Gray);

        for (int i = 0; i < _items.Count; i++)
        {
            var (bubble, bubbleText, caption, idx) = _items[i];

            var isCompleted = idx < CurrentStep;
            var isCurrent = idx == CurrentStep;
            var isClickable = idx <= CurrentStep;
            if (isCompleted || isCurrent)
            {
                bubble.BackgroundColor = primary;
                bubbleText.TextColor = primaryText;
            }
            else
            {
                bubble.BackgroundColor = secondary;
                bubbleText.TextColor = mutedText;
            }

            if (isCompleted)
            {
                bubbleText.Text = "✓";
                bubbleText.FontSize = 18;
            }
            else
            {
                bubbleText.Text = (idx + 1).ToString();
                bubbleText.FontSize = 14;
            }

            caption.TextColor = isCurrent ? primary : mutedText;
            caption.FontAttributes = isCurrent ? FontAttributes.Bold : FontAttributes.None;
            bubble.IsEnabled = isClickable;
            bubble.Opacity = isClickable ? 1.0 : 0.6;
            if (isCurrent)
                _ = Pulse(bubble);
        }

        UpdateProgressLine();
    }

    private async Task Pulse(VisualElement bubble)
    {
        if (bubble.AnimationIsRunning("pulse")) return;

        var animation = new Animation();
        animation.Add(0, 0.5, new Animation(v => bubble.Scale = v, 1.0, 1.06, Easing.CubicInOut));
        animation.Add(0.5, 1, new Animation(v => bubble.Scale = v, 1.06, 1.0, Easing.CubicInOut));

        bubble.Animate("pulse", animation, length: 900, repeat: () => bubble.IsVisible);
        await Task.CompletedTask;
    }

    private void UpdateProgressLine()
    {
        var stepsCount = Steps?.Count ?? 0;
        if (stepsCount <= 1)
        {
            LineFg.WidthRequest = 0;
            return;
        }

        var totalWidth = StepsGrid.Width;
        if (totalWidth <= 0) return;

        var ratio = (double)CurrentStep / (stepsCount - 1);
        ratio = Math.Max(0, Math.Min(1, ratio));
        var columnWidth = totalWidth / stepsCount;
        var inset = columnWidth / 2;
        var span = Math.Max(0, totalWidth - columnWidth);

        LineBg.Margin = new Thickness(inset, 0, inset, 0);
        LineFg.Margin = new Thickness(inset, 0, 0, 0);

        LineFg.WidthRequest = span * ratio;
    }
}
