using System.Threading;

namespace labyItems.Helpers;

public static class CardExpandAnimationHelper
{
    public static double ResolveMeasureWidth(VisualElement primary, params VisualElement[] fallbacks)
    {
        if (primary.Width > 0)
            return primary.Width;

        foreach (var fallback in fallbacks)
        {
            if (fallback != null && fallback.Width > 0)
                return fallback.Width;
        }

        return -1;
    }

    public static double MeasureContentHeight(VisualElement content, double width)
        => content.Measure(width, double.PositiveInfinity).Height;

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
                easing);

            animation.Commit(
                owner: owner,
                name: animationName,
                rate: 16,
                length: length,
                finished: (_, __) => tcs.TrySetResult());

            await tcs.Task;
        }
        finally
        {
            registration.Dispose();
        }
    }
}
