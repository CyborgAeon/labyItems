using labyItems.Controls;
using labyItems.Models.WorldMap;
using labyItems.Services;

namespace labyItems.Pages.WorldMap;

public sealed class RegionDetailsPage : ContentPage
{
    public RegionDetailsPage(WorldRegion region, WorldStatsPeriod period, IReadOnlyList<WorldStatistic> statistics, bool modal = false, IWorldRegionRepository? repository = null)
    {
        Title = region.Name;
        NavigationPage.SetHasNavigationBar(this, false);
        var body = new VerticalStackLayout { Padding = 20, Spacing = 14 };
        if (!modal) body.Add(new DetailPageHeader { HeaderText = region.Name });
        else
        {
            var close = new Button { Text = "Close", HorizontalOptions = LayoutOptions.End };
            close.Clicked += async (_, _) => await Navigation.PopModalAsync();
            body.Add(close);
            body.Add(new Label { Text = region.Name, FontSize = 28, FontAttributes = FontAttributes.Bold });
        }
        var subtitle = new Label { Text = $"{region.Category} · {period}" };
        subtitle.SetDynamicResource(Label.TextColorProperty, "MutedTextColor");
        body.Add(subtitle);
        if (region.StatisticsId is null && region.RelatedStatisticsIds.Count > 0 && repository is not null)
        {
            body.Add(new Label { Text = "Available statistics by constituent place. Values are not combined across this territory." });
            foreach (var place in region.RelatedStatisticsIds)
            {
                body.Add(new Label { Text = place, FontSize = 18, FontAttributes = FontAttributes.Bold });
                body.Add(new WorldStatisticsView(repository.GetStatistics(place, period)));
            }
        }
        else body.Add(new WorldStatisticsView(statistics));
        if (modal)
        {
            var details = new Button { Text = "View Full Details" };
            details.Clicked += async (_, _) => await Navigation.PushAsync(new RegionDetailsPage(region, period, statistics, repository: repository));
            body.Add(details);
        }
        Content = new ScrollView { Content = body };
    }
}
