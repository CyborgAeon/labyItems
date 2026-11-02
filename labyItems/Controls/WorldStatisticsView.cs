using labyItems.Models.WorldMap;

namespace labyItems.Controls;

public sealed class WorldStatisticsView : ContentView
{
    public WorldStatisticsView(IReadOnlyList<WorldStatistic> statistics)
    {
        var body = new VerticalStackLayout { Spacing = 12 };
        if (statistics.Count == 0)
            body.Add(new Label { Text = "No statistics published or mapped for this place in the selected month.", LineBreakMode = LineBreakMode.WordWrap });
        foreach (var category in new[] { "Administration", "Dragons", "Deities and Orders" })
        {
            var entries = statistics.Where(s => s.Category == category).ToList();
            if (entries.Count == 0) continue;
            var rows = new VerticalStackLayout { Spacing = 0 };
            foreach (var (entry, index) in entries.Select((entry, index) => (entry, index)))
            {
                var row = new Grid
                {
                    ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
                    ColumnSpacing = 12,
                    Padding = new Thickness(8),
                    BackgroundColor = index % 2 == 0 ? Colors.White : Color.FromArgb("#FAF8F3")
                };
                row.Add(new Label { Text = entry.Name, FontSize = 14, VerticalTextAlignment = TextAlignment.Center });
                row.Add(new Label { Text = entry.Value.ToString("0.##"), FontSize = 14, HorizontalTextAlignment = TextAlignment.End, VerticalTextAlignment = TextAlignment.Center }, 1);
                rows.Add(row);
            }
            body.Add(new ExpandableSectionCard { HeaderText = category, IsExpanded = category == "Administration", BodyContent = rows });
        }
        Content = body;
    }
}
