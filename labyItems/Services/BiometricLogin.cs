#if ANDROID
using Android.Content;
using Android.Hardware.Biometrics;
using Android.OS;
#elif IOS || MACCATALYST
using LocalAuthentication;
#endif

namespace labyItems.Services;

public static class BiometricLogin
{
    public static bool IsAvailable
    {
        get
        {
#if ANDROID
            if (!OperatingSystem.IsAndroidVersionAtLeast(28)) return false;
            var context = Android.App.Application.Context;
            if (OperatingSystem.IsAndroidVersionAtLeast(29))
                return (context.GetSystemService(Context.BiometricService) as BiometricManager)?.CanAuthenticate() == BiometricCode.Success;
            var fingerprint = context.GetSystemService(Context.FingerprintService) as Android.Hardware.Fingerprints.FingerprintManager;
            return fingerprint?.IsHardwareDetected == true && fingerprint.HasEnrolledFingerprints;
#elif IOS || MACCATALYST
            using var context = new LAContext();
            return context.CanEvaluatePolicy(LAPolicy.DeviceOwnerAuthenticationWithBiometrics, out _);
#else
            return false;
#endif
        }
    }

    public static async Task<bool> AuthenticateAsync()
    {
        if (!IsAvailable) return false;
#if ANDROID
        var activity = Platform.CurrentActivity;
        if (activity == null) return false;
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationSignal();
        using var callback = new AuthenticationCallback(completion);
        using var negative = new CancelListener(completion);
        using var prompt = new BiometricPrompt.Builder(activity)
            .SetTitle("Sign in to Labyrinthe")
            .SetDescription("Unlock your saved login")
            .SetNegativeButton("Cancel", activity.MainExecutor!, negative)
            .Build();
        prompt.Authenticate(cancellation, activity.MainExecutor!, callback);
        var success = await completion.Task;
        cancellation.Cancel();
        return success;
#elif IOS || MACCATALYST
        using var context = new LAContext { LocalizedFallbackTitle = "" };
        var result = await context.EvaluatePolicyAsync(LAPolicy.DeviceOwnerAuthenticationWithBiometrics, "Unlock your saved Labyrinthe login");
        return result.Item1;
#else
        return false;
#endif
    }

#if ANDROID
    private sealed class AuthenticationCallback(TaskCompletionSource<bool> completion) : BiometricPrompt.AuthenticationCallback
    {
        public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult? result)
            => completion.TrySetResult(true);
        public override void OnAuthenticationError(BiometricErrorCode errorCode, Java.Lang.ICharSequence? errString)
            => completion.TrySetResult(false);
    }

    private sealed class CancelListener(TaskCompletionSource<bool> completion) : Java.Lang.Object, IDialogInterfaceOnClickListener
    {
        public void OnClick(IDialogInterface? dialog, int which) => completion.TrySetResult(false);
    }
#endif
}
