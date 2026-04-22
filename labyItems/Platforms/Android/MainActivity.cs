using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.AppCompat.App;

namespace labyItems;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    internal static event EventHandler<ActivityResultEventArgs>? ActivityResultReceived;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        AppCompatDelegate.DefaultNightMode = AppCompatDelegate.ModeNightNo;
        base.OnCreate(savedInstanceState);
        Window.SetSoftInputMode(SoftInput.AdjustResize);
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Android.Content.Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        ActivityResultReceived?.Invoke(this, new ActivityResultEventArgs(requestCode, resultCode, data));
    }
}

internal sealed class ActivityResultEventArgs(int requestCode, Result resultCode, Android.Content.Intent? data) : EventArgs
{
    public int RequestCode { get; } = requestCode;
    public Result ResultCode { get; } = resultCode;
    public Android.Content.Intent? Data { get; } = data;
}
