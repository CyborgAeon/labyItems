namespace labyItems.Pages.Calculator;

using Microsoft.Maui.ApplicationModel;

public partial class MpCalculator : TabbedPage
{
    public MpCalculator()
    {
        InitializeComponent();
        CurrentPageChanged += (_, _) => QueuePlatformTabLayoutRefresh();
        SizeChanged += (_, _) => QueuePlatformTabLayoutRefresh();
        HandlerChanged += (_, _) => QueuePlatformTabLayoutRefresh();
        QueuePlatformTabLayoutRefresh();
    }

    private void QueuePlatformTabLayoutRefresh()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(10);
            ApplyPlatformTabLayoutTweaks();
            await Task.Delay(60);
            ApplyPlatformTabLayoutTweaks();
        });
    }

    partial void ApplyPlatformTabLayoutTweaks();
}
