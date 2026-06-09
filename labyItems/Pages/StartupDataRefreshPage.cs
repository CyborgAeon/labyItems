using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace labyItems.Pages;

public sealed class StartupDataRefreshPage : ContentPage
{
    private readonly Label _title;
    private readonly Label _subtitle;
    private readonly Label _note;
    private readonly ActivityIndicator _activity;
    private readonly Button _retryButton;

    public event EventHandler? RetryRequested;

    public StartupDataRefreshPage()
    {
        Title = "Updating data";
        BackgroundColor = Colors.White;
        Padding = new Thickness(24, 32);

        _title = new Label
        {
            Text = "Refreshing built-in game data",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.Black,
            HorizontalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center
        };

        _subtitle = new Label
        {
            Text = "The app is checking this install for new data and updating local storage. This screen cannot be dismissed.",
            FontSize = 16,
            TextColor = Colors.Black,
            HorizontalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 16, 0, 0)
        };

        _activity = new ActivityIndicator
        {
            IsRunning = true,
            Color = Colors.Black,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 32, 0, 0)
        };

        _note = new Label
        {
            Text = "Please wait while the data is refreshed. You will be taken to the app as soon as the update completes.",
            FontSize = 14,
            TextColor = Colors.Gray,
            HorizontalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 20, 0, 0)
        };

        _retryButton = new Button
        {
            Text = "Retry",
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 24, 0, 0),
            IsVisible = false
        };
        _retryButton.Clicked += (_, _) => RetryRequested?.Invoke(this, EventArgs.Empty);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Spacing = 0,
                VerticalOptions = LayoutOptions.CenterAndExpand,
                Children = { _title, _subtitle, _activity, _note, _retryButton }
            }
        };
    }

    public void SetWorking(string phase, string message)
    {
        _title.Text = string.IsNullOrWhiteSpace(phase) ? "Updating data" : phase;
        _subtitle.Text = string.IsNullOrWhiteSpace(message)
            ? "Preparing startup data."
            : message;
        _note.Text = "Please wait while the app prepares data in the background.";
        _retryButton.IsVisible = false;
        _activity.IsVisible = true;
        _activity.IsRunning = true;
    }

    public void SetError(string title, string message)
    {
        _title.Text = string.IsNullOrWhiteSpace(title) ? "Startup failed" : title;
        _subtitle.Text = string.IsNullOrWhiteSpace(message)
            ? "An error occurred during startup."
            : message;
        _note.Text = "You can retry initialization without restarting the app.";
        _activity.IsRunning = false;
        _activity.IsVisible = false;
        _retryButton.IsVisible = true;
    }
}
