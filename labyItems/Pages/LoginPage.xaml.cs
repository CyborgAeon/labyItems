namespace labyItems.Pages;

public partial class LoginPage : ContentPage
{
    private const string PlaceholderUsername = "admin";
    private const string PlaceholderPassword = "admin";

    public LoginPage()
    {
        InitializeComponent();
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        var username = (UsernameEntry.Text ?? string.Empty).Trim();
        var password = PasswordEntry.Text ?? string.Empty;

        if (!string.Equals(username, PlaceholderUsername, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(password, PlaceholderPassword, StringComparison.Ordinal))
        {
            await DisplayAlert("Login failed", "Use the placeholder credentials admin / admin.", "OK");
            return;
        }

        var homePage = new ItemRoutePage();
        Navigation.InsertPageBefore(homePage, this);
        await Navigation.PopAsync();
    }

    private async void OnForgotPasswordClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new ForgotPasswordPage());
    }
}
