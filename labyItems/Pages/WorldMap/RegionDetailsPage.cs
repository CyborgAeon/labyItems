using labyItems.Controls;
using labyItems.Helpers;
using labyItems.Models.WorldMap;
using labyItems.Services;

namespace labyItems.Pages.WorldMap;

public sealed class RegionDetailsPage : ContentPage
{
    public RegionDetailsPage(WorldRegion region, WorldStatsPeriod period, IWorldRegionRepository repository)
    {
        Title = region.Name;
        NavigationPage.SetHasNavigationBar(this, false);
        Shell.SetNavBarIsVisible(this, false);
        var header = new Grid { Padding = 16, ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 12 };
        header.Add(new VerticalStackLayout { Spacing = 4, Children =
        {
            new Label { Text = region.Name, FontSize = 24, FontAttributes = FontAttributes.Bold },
            new Label { Text = $"{region.Category} · {period}", FontSize = 13 }
        } });
        var close = new Button { Text = FontAwesomeGlyphs.Remove, FontFamily = FontAwesomeGlyphs.SolidFamily, BackgroundColor = Colors.Transparent, Padding = 8, MinimumWidthRequest = 44, MinimumHeightRequest = 44 };
        SemanticProperties.SetDescription(close, "Close place statistics");
        close.Clicked += async (_, _) => await Navigation.PopModalAsync();
        header.Add(close, 1);
        var details = new VerticalStackLayout { Padding = new Thickness(16, 0, 16, 20), Spacing = 16 };
        var ids = region.StatisticsId is not null ? new[] { region.StatisticsId } : region.RelatedStatisticsIds ?? [];
        if (ids.Count == 0) details.Add(new WorldStatisticsView([]));
        foreach (var id in ids)
        {
            if (region.StatisticsId is null) details.Add(new Label { Text = id, FontSize = 18, FontAttributes = FontAttributes.Bold });
            details.Add(new WorldStatisticsView(repository.GetStatistics(id, period)));
        }
        var root = new Grid { RowDefinitions = [new(GridLength.Auto), new(GridLength.Star)] };
        root.Add(header); root.Add(new ScrollView { Content = details }, 0, 1);
        Content = root;
    }
}
