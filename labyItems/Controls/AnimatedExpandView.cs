using labyItems.Helpers;

namespace labyItems.Controls;

public class AnimatedExpandView : ContentView
{
    public static readonly BindableProperty IsExpandedProperty = BindableProperty.Create(
        nameof(IsExpanded),
        typeof(bool),
        typeof(AnimatedExpandView),
        true,
        propertyChanged: OnIsExpandedChanged);

    public static readonly BindableProperty AnimationNameProperty = BindableProperty.Create(
        nameof(AnimationName),
        typeof(string),
        typeof(AnimatedExpandView),
        "animated-expand");

    public static readonly BindableProperty ExpandDurationProperty = BindableProperty.Create(
        nameof(ExpandDuration),
        typeof(uint),
        typeof(AnimatedExpandView),
        (uint)240);

    public static readonly BindableProperty CollapseDurationProperty = BindableProperty.Create(
        nameof(CollapseDuration),
        typeof(uint),
        typeof(AnimatedExpandView),
        (uint)200);

    private CancellationTokenSource? _animationCts;
    private bool _isLoaded;

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public string AnimationName
    {
        get => (string)GetValue(AnimationNameProperty);
        set => SetValue(AnimationNameProperty, value);
    }

    public uint ExpandDuration
    {
        get => (uint)GetValue(ExpandDurationProperty);
        set => SetValue(ExpandDurationProperty, value);
    }

    public uint CollapseDuration
    {
        get => (uint)GetValue(CollapseDurationProperty);
        set => SetValue(CollapseDurationProperty, value);
    }

    public AnimatedExpandView()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        _isLoaded = true;
        ApplyImmediateState(IsExpanded);
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        _isLoaded = false;
        _animationCts?.Cancel();
    }

    private static void OnIsExpandedChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (AnimatedExpandView)bindable;
        view.ScheduleStateUpdate();
    }

    private void ScheduleStateUpdate()
    {
        if (!_isLoaded)
        {
            ApplyImmediateState(IsExpanded);
            return;
        }

        Dispatcher.Dispatch(async () => await AnimateStateChangeAsync(IsExpanded));
    }

    private void ApplyImmediateState(bool expanded)
    {
        this.AbortAnimation(AnimationName);
        _animationCts?.Cancel();
        HeightRequest = -1;
        Opacity = 1;
        IsVisible = expanded;
    }

    private async Task AnimateStateChangeAsync(bool expand)
    {
        if (Content == null)
        {
            ApplyImmediateState(expand);
            return;
        }

        _animationCts?.Cancel();
        _animationCts = new CancellationTokenSource();
        var token = _animationCts.Token;

        this.AbortAnimation(AnimationName);

        try
        {
            if (expand)
            {
                IsVisible = true;
                Opacity = 0;
                HeightRequest = -1;

                await Task.Yield();
                await Task.Delay(1, token);

                var parent = Parent as VisualElement;
                var width = parent != null
                    ? CardExpandAnimationHelper.ResolveMeasureWidth(this, parent)
                    : CardExpandAnimationHelper.ResolveMeasureWidth(this);
                var measured = width > 0
                    ? CardExpandAnimationHelper.MeasureContentHeight(Content, width)
                    : -1;

                if (measured <= 0)
                {
                    HeightRequest = -1;
                    Opacity = 1;
                    IsVisible = true;
                    return;
                }

                HeightRequest = 0;
                Opacity = 0;
                await CardExpandAnimationHelper.AnimateHeightAsync(
                    owner: this,
                    target: this,
                    animationName: AnimationName,
                    from: 0,
                    to: measured,
                    length: ExpandDuration,
                    easing: Easing.CubicOut,
                    onStep: v => Opacity = Math.Min(1, v / measured),
                    cancellationToken: token);

                if (token.IsCancellationRequested) return;

                HeightRequest = -1;
                Opacity = 1;
                IsVisible = true;
                return;
            }

            if (!IsVisible)
            {
                ApplyImmediateState(false);
                return;
            }

            var startHeight = Height;
            if (startHeight <= 0)
            {
                IsVisible = false;
                HeightRequest = -1;
                Opacity = 1;
                return;
            }

            HeightRequest = startHeight;
            await CardExpandAnimationHelper.AnimateHeightAsync(
                owner: this,
                target: this,
                animationName: AnimationName,
                from: startHeight,
                to: 0,
                length: CollapseDuration,
                easing: Easing.CubicIn,
                onStep: v => Opacity = startHeight <= 0 ? 0 : Math.Max(0, v / startHeight),
                cancellationToken: token);

            if (token.IsCancellationRequested) return;

            IsVisible = false;
            HeightRequest = -1;
            Opacity = 1;
        }
        catch (TaskCanceledException)
        {
            // Ignore rapid toggle interactions.
        }
        catch (OperationCanceledException)
        {
            // Ignore rapid toggle interactions.
        }
    }
}
