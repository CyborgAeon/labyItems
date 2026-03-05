using System.Threading;

namespace labyItems.Helpers;

public static class CardExpandAnimationHelper
{
    public const uint UnifiedDurationMs = 200;
    public static readonly Easing UnifiedEasing = Easing.CubicInOut;

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
        => content.Measure(width, double.PositiveInfinity).Height;

    private static double ResolveWidthFromVisualTree(VisualElement? start)
    {
        var current = start;
        while (current != null)
        {
            if (current.Width > 0)
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
            if (current is Page page && page.Width > 0)
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
                UnifiedEasing);

            animation.Commit(
                owner: owner,
                name: animationName,
                rate: 16,
                length: UnifiedDurationMs,
                finished: (_, __) => tcs.TrySetResult());

            await tcs.Task;
        }
        finally
        {
            registration.Dispose();
        }
    }
}
