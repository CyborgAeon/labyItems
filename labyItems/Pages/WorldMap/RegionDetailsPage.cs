using labyItems.Controls;
using labyItems.Helpers;
using labyItems.Models.WorldMap;
using labyItems.Services;

namespace labyItems.Pages.WorldMap;

public sealed class RegionDetailsPage : ContentPage
{
    private readonly IWorldRegionRepository repository;
    private readonly ToolsetNavigationMenu menu = new();
    private readonly VerticalStackLayout details = new() { Padding = 16, Spacing = 16 };
    private readonly Label name = new() { FontSize = 28, FontAttributes = FontAttributes.Bold, TextColor = Colors.White };
    private readonly Label category = new() { FontSize = 13, TextColor = Colors.White };
    private readonly Button date = new() { BackgroundColor = Colors.Transparent, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Start, Padding = 0, MinimumHeightRequest = 44 };
    private readonly VerticalStackLayout highlights = new() { Spacing = 8 };
    private WorldRegion region;
    private WorldStatsPeriod period;

    public RegionDetailsPage(WorldRegion region, WorldStatsPeriod period, IWorldRegionRepository repository)
    {
        this.region = region;
        this.period = period;
        this.repository = repository;
        NavigationPage.SetHasNavigationBar(this, false);
        Shell.SetNavBarIsVisible(this, false);
        var root = new Grid();
        var body = new VerticalStackLayout { Spacing = 0 };
        var header = new Grid();
        header.Add(new Image { Source = "login_watermark.png", Aspect = Aspect.AspectFill, InputTransparent = true });
        header.Add(new BoxView { Color = Primary, Opacity = .82, InputTransparent = true });
        var overview = new VerticalStackLayout { Padding = new Thickness(16, 8, 16, 20), Spacing = 10 };
        var bar = new Grid { ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto)] };
        var burger = GlyphButton(FontAwesomeGlyphs.Menu, "Open toolset menu", useSystemFont: true);
        burger.Command = menu.OpenMenuCommand;
        bar.Add(burger);
        bar.Add(new Label { Text = "Region overview", TextColor = Colors.White, FontSize = 19, FontAttributes = FontAttributes.Bold, VerticalTextAlignment = TextAlignment.Center }, 1);
        var options = GlyphButton(FontAwesomeGlyphs.MoreVertical, "Region options");
        options.Clicked += async (_, _) => await RunAsync(ShowOptionsAsync);
        bar.Add(options, 2);
        var close = GlyphButton(FontAwesomeGlyphs.Remove, "Close region overview");
        close.Clicked += async (_, _) => await Navigation.PopModalAsync();
        bar.Add(close, 3);
        overview.Add(bar); overview.Add(name); overview.Add(category);
        date.Clicked += async (_, _) => await RunAsync(SelectDateAsync);
        overview.Add(date); overview.Add(highlights);
        overview.Add(new Label
        {
            Text = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Integer vitae tellus in lacus placerat posuere. Sed at sapien vel justo consequat interdum.",
            TextColor = Colors.White, FontSize = 14, LineBreakMode = LineBreakMode.WordWrap
        });
        header.Add(overview);
        body.Add(header); body.Add(details);
        root.Add(new ScrollView { Content = body });
        root.Add(menu);
        Content = root;
        Refresh();
    }

    private static Color Primary => Application.Current?.Resources["Primary"] as Color ?? Color.FromArgb("#530000");

    private static Button GlyphButton(string glyph, string description, bool useSystemFont = false)
    {
        var button = new Button { Text = glyph, TextColor = Colors.White, BackgroundColor = Colors.Transparent, Padding = 8, MinimumWidthRequest = 44, MinimumHeightRequest = 44, FontSize = 18 };
        if (!useSystemFont) button.FontFamily = FontAwesomeGlyphs.SolidFamily;
        SemanticProperties.SetDescription(button, description);
        return button;
    }

    private void Refresh()
    {
        Title = region.Name;
        name.Text = region.Name;
        category.Text = region.Category;
        date.Text = period.ToString();
        SemanticProperties.SetDescription(date, $"Statistics for {period}. Select another month");
        var ids = region.StatisticsId is not null ? new[] { region.StatisticsId } : region.RelatedStatisticsIds ?? [];
        var groups = ids.Select(id => new WorldStatisticsGroup(id, repository.GetStatistics(id, period))).ToList();
        highlights.Clear();
        foreach (var section in new[] { "Administration", "Dragons", "Deities and Orders" })
        {
            var title = new Label { Text = $"Highest {section.ToLowerInvariant()}", TextColor = Colors.White, FontSize = 12, Opacity = .85 };
            highlights.Add(new VerticalStackLayout { Spacing = 2, Children = { title, new Label { Text = WorldStatisticsSummaryFormatter.HighestText(groups, section), TextColor = Colors.White, FontSize = 15 } } });
        }
        details.Clear();
        if (groups.Count == 0) details.Add(new WorldStatisticsView([]));
        foreach (var group in groups)
        {
            if (region.StatisticsId is null)
                details.Add(new Label { Text = group.Place, FontSize = 18, FontAttributes = FontAttributes.Bold });
            details.Add(new WorldStatisticsView(group.Statistics));
        }
    }

    private async Task ShowOptionsAsync()
    {
        var choice = await DisplayActionSheetAsync("Region options", "Cancel", null, "Select date", "Select region");
        if (choice == "Select date") await SelectDateAsync();
        else if (choice == "Select region") await SelectRegionAsync();
    }

    private async Task SelectDateAsync()
    {
        var periods = repository.GetPeriods();
        var choice = await DisplayActionSheetAsync("Statistics month", "Cancel", null, periods.Select(p => p.ToString()).ToArray());
        var selected = periods.FirstOrDefault(p => p.ToString() == choice);
        if (selected is null) return;
        period = selected;
        Refresh();
    }

    private async Task SelectRegionAsync()
    {
        var regions = (await repository.GetRegionsAsync()).ToList();
        var options = regions.OrderBy(r => r.Name, StringComparer.Ordinal).ThenBy(r => r.Category, StringComparer.Ordinal).ToList();
        options.AddRange(repository.GetStatisticsRegions().Where(id => !regions.Any(r => r.StatisticsId == id))
            .Select(id => new WorldRegion { Id = id, Name = id, Category = "Published place", StatisticsId = id }));
        var labels = options.Select(r => $"{r.Name} ({r.Category})").ToArray();
        var choice = await DisplayActionSheetAsync("Select region or location", "Cancel", null, labels);
        var index = Array.IndexOf(labels, choice);
        if (index < 0) return;
        region = options[index];
        Refresh();
    }

    private async Task RunAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex)
        {
            RuntimeLog.Write("WORLD_MAP", "Failed updating region overview.", ex);
            await DisplayAlertAsync("World statistics", "Unable to load the selected statistics.", "OK");
        }
    }
}
