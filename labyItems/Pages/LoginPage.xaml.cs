using labyItems.Services;
using labyItems.Helpers;

namespace labyItems.Pages;

public partial class LoginPage : ContentPage
{
    private readonly SavedLoginStore _savedLogin = new(new DeviceSavedLoginStorage(), BiometricLogin.AuthenticateAsync);
    private bool _isSigningIn;

    public LoginPage()
    {
        InitializeComponent();
        UsernameEntry.HandlerChanged += (_, _) => ConfigureAutofill();
        PasswordEntry.HandlerChanged += (_, _) => ConfigureAutofill();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RefreshSavedLoginActions();
    }

    private void RefreshSavedLoginActions()
    {
        BiometricActions.IsVisible = _savedLogin.HasSavedLogin && BiometricLogin.IsAvailable;
        ForgetLoginButton.IsVisible = _savedLogin.HasSavedLogin;
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        if (_isSigningIn) return;
        await SignInAsync((UsernameEntry.Text ?? "").Trim(), PasswordEntry.Text ?? "", offerSave: true);
    }

    private async void OnBiometricLoginClicked(object sender, EventArgs e)
    {
        if (_isSigningIn) return;
        SetSigningIn(true);
        try
        {
            var login = await _savedLogin.UnlockAsync();
            if (login == null) return;
            // Credentials remain out of the entry controls and are sent only after authentication.
            await CompleteSignInAsync(login.Email, login.Password, offerSave: false);
        }
        catch (Exception)
        {
            await DisplayAlert("Unable to sign in", "Please enter your email address and password to sign in.", "OK");
        }
        finally { SetSigningIn(false); RefreshSavedLoginActions(); }
    }

    private async Task SignInAsync(string email, string password, bool offerSave)
    {
        SetSigningIn(true);
        try { await CompleteSignInAsync(email, password, offerSave); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            await DisplayAlert("Login failed", "incorrect username or password", "OK");
        }
        finally { SetSigningIn(false); }
    }

    private async Task CompleteSignInAsync(string email, string password, bool offerSave)
    {
        if (!await LabyrintheSession.Shared.LoginAsync(email, password))
        {
            await DisplayAlert("Login failed", "incorrect username or password", "OK");
            return;
        }
        if (offerSave && !LabyrintheSession.Shared.IsFallback)
        {
            var save = await DisplayAlert(_savedLogin.HasSavedLogin ? "Update saved login?" : "Save login?",
                "Save your login securely on this device? You can use biometrics to sign in when available.", "Save", "Not now");
            if (save)
            {
                try { await _savedLogin.SaveAsync(email, password); }
                catch (Exception)
                {
                    await DisplayAlert("Login not saved", "Your device could not save the login. You can still sign in with your password.", "OK");
                }
            }
        }
        PasswordEntry.Text = string.Empty;
        await OpenHomeAsync();
    }

    private async Task OpenHomeAsync()
    {
        var homePage = new ItemRoutePage();
        Navigation.InsertPageBefore(homePage, this);
        await Navigation.PopAsync();
    }

    private void SetSigningIn(bool value)
    {
        _isSigningIn = value;
        LoginButton.IsEnabled = BiometricLoginButton.IsEnabled = ForgetLoginButton.IsEnabled = GuestLoginButton.IsEnabled = PasswordVisibilityButton.IsEnabled = !value;
        UsernameEntry.IsEnabled = PasswordEntry.IsEnabled = !value;
    }

    private async void OnForgetLoginClicked(object sender, EventArgs e)
    {
        if (_isSigningIn) return;
        SetSigningIn(true);
        try
        {
            if (BiometricLogin.IsAvailable)
            {
                if (!await BiometricLogin.AuthenticateAsync()) return;
            }
            else if (!await DisplayAlert("Forget saved login?", "Remove the login saved on this device?", "Remove", "Cancel")) return;
            _savedLogin.Remove();
        }
        catch (Exception) { await DisplayAlert("Unable to remove login", "Please try again.", "OK"); }
        finally { SetSigningIn(false); RefreshSavedLoginActions(); }
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
