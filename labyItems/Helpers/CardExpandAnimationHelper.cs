using System.Threading;

namespace labyItems.Helpers;

public static class CardExpandAnimationHelper
{
    public const uint UnifiedDurationMs = 200;
    public static readonly Easing UnifiedEasing = Easing.CubicInOut;

    public static bool IsFinite(double value)
        => !double.IsNaN(value) && !double.IsInfinity(value);

    public static double ResolveMeasureWidth(VisualElement primary, params VisualElement[] fallbacks)
    {
        var resolved = ResolveWidthFromVisualTree(primary);
        if (resolved > 0)
            return resolved;

        foreach (var fallback in fallbacks)
        {
            resolved = ResolveWidthFromVisualTree(fallback);
            if (resolved > 0)
                return resolved;
        }

        return ResolveWidthFromWindow(primary);
    }

    public static double MeasureContentHeight(VisualElement content, double width)
    {
        if (!IsFinite(width) || width <= 0)
            return -1;

        var measured = content.Measure(width, double.PositiveInfinity).Height;
        return IsFinite(measured) && measured > 0 ? measured : -1;
    }

    private static double ResolveWidthFromVisualTree(VisualElement? start)
    {
        var current = start;
        while (current != null)
        {
            if (IsFinite(current.Width) && current.Width > 0)
                return current.Width;

            current = current.Parent as VisualElement;
        }

        return -1;
    }

    private static double ResolveWidthFromWindow(VisualElement element)
    {
        var current = element.Parent;
        while (current != null)
        {
            if (current is Page page && IsFinite(page.Width) && page.Width > 0)
                return page.Width;

            current = current.Parent;
        }

        return -1;
    }

    public static async Task AnimateHeightAsync(
        VisualElement owner,
        VisualElement target,
        string animationName,
        double from,
        double to,
        uint length,
        Easing easing,
        Action<double>? onStep = null,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        if (!IsFinite(from) || !IsFinite(to))
            return;

        if (Math.Abs(from - to) < 0.5)
        {
            target.HeightRequest = to;
            onStep?.Invoke(to);
            return;
        }

        target.AbortAnimation(animationName);

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = cancellationToken.CanBeCanceled
            ? cancellationToken.Register(() =>
            {
                target.AbortAnimation(animationName);
                tcs.TrySetCanceled(cancellationToken);
            })
            : default;

        try
        {
            var animation = new Animation(
                v =>
                {
                    target.HeightRequest = v;
                    onStep?.Invoke(v);
                },
                from,
                to,
                easing ?? UnifiedEasing);

            animation.Commit(
                owner: owner,
                name: animationName,
                rate: 16,
                length: length == 0 ? UnifiedDurationMs : length,
                finished: (_, __) => tcs.TrySetResult());

            await tcs.Task;
        }
        finally
        {
            registration.Dispose();
        }
    }

    public static Task FadeAsync(VisualElement target, double to)
        => target.FadeTo(to, UnifiedDurationMs, UnifiedEasing);

    public static Task TranslateYAsync(VisualElement target, double to)
        => target.TranslateTo(0, to, UnifiedDurationMs, UnifiedEasing);
}
