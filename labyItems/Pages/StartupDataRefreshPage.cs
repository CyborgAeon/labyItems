using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace labyItems.Pages;

public sealed class StartupDataRefreshPage : ContentPage
{
    public StartupDataRefreshPage()
    {
        Title = "Updating data";
        BackgroundColor = Colors.White;
        Padding = new Thickness(24, 32);

        var title = new Label
        {
            Text = "Refreshing built-in game data",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.Black,
            HorizontalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center
        };

        var subtitle = new Label
        {
            Text = "The app is checking this install for new data and updating local storage. This screen cannot be dismissed.",
            FontSize = 16,
            TextColor = Colors.Black,
            HorizontalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 16, 0, 0)
        };

        var activity = new ActivityIndicator
        {
            IsRunning = true,
            Color = Colors.Black,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 32, 0, 0)
        };

        var note = new Label
        {
            Text = "Please wait while the data is refreshed. You will be taken to the app as soon as the update completes.",
            FontSize = 14,
            TextColor = Colors.Gray,
            HorizontalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 20, 0, 0)
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Spacing = 0,
                VerticalOptions = LayoutOptions.CenterAndExpand,
                Children = { title, subtitle, activity, note }
            }
        };
    }
}
