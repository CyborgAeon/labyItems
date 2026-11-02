using labyItems.Pages;

namespace labyItems.Services;

public static class AccountNavigation
{
    public static async Task SwitchAsync(Window window)
    {
        // Replace the entire navigation tree before any asynchronous cleanup.
        window.Page = new ContentPage
        {
            Content = new ActivityIndicator { IsRunning = true, VerticalOptions = LayoutOptions.Center }
        };
        LabyrintheSession.Shared.InvalidateOperations();
        try
        {
            await Task.Run(() => AccountDataAccess.Workspace?.Close());
            LabyrintheSession.Shared.Logout();
            AccountStorageCoordinator.ResetCaches();
            window.Page = new NavigationPage(new LoginPage());
        }
        catch
        {
            // Keep all former pages inaccessible if local storage cannot be safely sealed.
            window.Page = new ContentPage { Content = new Label { Text = "Could not lock account storage. Close and reopen the app before continuing.", Margin = 24 } };
            throw;
        }
    }
}
