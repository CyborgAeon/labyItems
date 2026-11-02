using labyItems.Services;
using labyItems.Helpers;

namespace labyItems.Pages;

public partial class LoginPage : ContentPage
{
    private bool _isSigningIn;

    public LoginPage()
    {
        InitializeComponent();
        UsernameEntry.HandlerChanged += (_, _) => ConfigureAutofill();
        PasswordEntry.HandlerChanged += (_, _) => ConfigureAutofill();
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        if (_isSigningIn) return;
        await SignInAsync((UsernameEntry.Text ?? "").Trim(), PasswordEntry.Text ?? "");
    }

    private async Task SignInAsync(string email, string password)
    {
        SetSigningIn(true);
        try { await CompleteSignInAsync(email, password); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            await DisplayAlert("Login failed", "incorrect username or password", "OK");
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or IOException or UnauthorizedAccessException)
        {
            await DisplayAlert("Account storage locked", "Your local account data could not be opened. It has been preserved. Please try again.", "OK");
        }
        finally { SetSigningIn(false); }
    }

    private async Task CompleteSignInAsync(string email, string password)
    {
        if (!await LabyrintheSession.Shared.LoginAsync(email, password))
        {
            await DisplayAlert("Login failed", "incorrect username or password", "OK");
            return;
        }
        PasswordEntry.Text = string.Empty;
        await OpenHomeAsync();
    }

    private async Task OpenHomeAsync()
    {
        try
        {
            await AccountDataAccess.Workspace!.OpenAsync();
            AccountStorageCoordinator.ResetCaches();
            if (Window != null) Window.Page = new NavigationPage(new ItemRoutePage());
        }
        catch
        {
            LabyrintheSession.Shared.Logout();
            throw;
        }
    }

    private void SetSigningIn(bool value)
    {
        _isSigningIn = value;
        LoginButton.IsEnabled = GuestLoginButton.IsEnabled = PasswordVisibilityButton.IsEnabled = !value;
        UsernameEntry.IsEnabled = PasswordEntry.IsEnabled = !value;
    }

    private void ConfigureAutofill()
    {
#if ANDROID
        if (!OperatingSystem.IsAndroidVersionAtLeast(26)) return;
        if (UsernameEntry.Handler?.PlatformView is Android.Widget.EditText email)
            email.SetAutofillHints(Android.Views.View.AutofillHintUsername);
        if (PasswordEntry.Handler?.PlatformView is Android.Widget.EditText password)
            password.SetAutofillHints(Android.Views.View.AutofillHintPassword);
#elif IOS || MACCATALYST
        if (UsernameEntry.Handler?.PlatformView is UIKit.UITextField email)
            email.TextContentType = UIKit.UITextContentType.Username;
        if (PasswordEntry.Handler?.PlatformView is UIKit.UITextField password)
            password.TextContentType = UIKit.UITextContentType.Password;
#endif
    }

    private void OnUsernameCompleted(object sender, EventArgs e) => PasswordEntry.Focus();

    private void OnTogglePasswordClicked(object sender, EventArgs e)
    {
        PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
        PasswordVisibilityButton.Text = PasswordEntry.IsPassword ? FontAwesomeGlyphs.Eye : FontAwesomeGlyphs.EyeSlash;
        SemanticProperties.SetDescription(PasswordVisibilityButton, PasswordEntry.IsPassword ? "Show password" : "Hide password");
    }

    private async void OnGuestLoginClicked(object sender, EventArgs e)
    {
        if (_isSigningIn) return;
        SetSigningIn(true);
        try
        {
            LabyrintheSession.Shared.LoginAsGuest();
            PasswordEntry.Text = string.Empty;
            await OpenHomeAsync();
        }
        finally { SetSigningIn(false); }
    }
}
