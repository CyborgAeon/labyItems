using Foundation;
using Microsoft.Maui.Platform;
using UIKit;

namespace labyItems;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        KeyboardAutoManagerScroll.Connect();
        return base.FinishedLaunching(application, launchOptions);
    }

    public override void WillTerminate(UIApplication application)
    {
        KeyboardAutoManagerScroll.Disconnect();
        base.WillTerminate(application);
    }

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
