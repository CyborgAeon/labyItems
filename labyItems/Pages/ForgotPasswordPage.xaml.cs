namespace labyItems.Pages;

public partial class ForgotPasswordPage : ContentPage
{
    public ForgotPasswordPage()
    {
        InitializeComponent();
    }

    private async void OnResetPasswordClicked(object sender, EventArgs e)
    {
        var email = (EmailEntry.Text ?? string.Empty).Trim();
        var message = email.Length == 0
            ? "Enter an email address to continue."
            : $"Password reset is not wired up yet. A reset link would be sent to {email}.";

        await DisplayAlert("Forgot Password", message, "OK");
    }

    private async void OnBackToLoginClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
