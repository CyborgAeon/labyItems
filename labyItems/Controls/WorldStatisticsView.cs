using labyItems.Models.WorldMap;

namespace labyItems.Controls;

public sealed class WorldStatisticsView : ContentView
{
    public WorldStatisticsView(IReadOnlyList<WorldStatistic>? statistics)
    {
        statistics ??= [];
        var body = new VerticalStackLayout { Spacing = 12 };
        if (statistics.Count == 0)
            body.Add(new Label { Text = "No statistics published or mapped for this place in the selected month.", LineBreakMode = LineBreakMode.WordWrap });
        foreach (var category in new[] { "Administration", "Dragons", "Deities and Orders" })
        {
            var entries = statistics.Where(s => s.Category == category).ToList();
            if (entries.Count == 0) continue;
            var rows = new VerticalStackLayout { Spacing = 0 };
            foreach (var (entry, index) in entries.Select((entry, index) => (entry, index)))
                rows.Add(new ListItemRowView
                {
                    PrimaryText = entry.Name,
                    PrimaryTextMaxLines = -1,
                    SecondaryText = entry.Value.ToString("0.##"),
                    ShowSecondaryText = true,
                    PrimaryTextAlignment = TextAlignment.Start,
                    SecondaryTextAlignment = TextAlignment.End,
                    RowBackgroundColor = index % 2 == 0 ? Colors.White : Color.FromArgb("#FAF8F3")
                });
            var expanded = new AnimatedExpandView { IsExpanded = false, Content = rows };
            var chevron = new ChevronIconView { AnimateRotation = true, IconSize = 28, GlyphSize = 14, InputTransparent = true };
            var heading = new Label { Text = category, VerticalTextAlignment = TextAlignment.Center, InputTransparent = true };
            heading.SetDynamicResource(StyleProperty, "SectionHeaderLabelStyle");
            var header = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)], MinimumHeightRequest = 44 };
            header.Add(heading); header.Add(chevron, 1);
            var toggle = new TapGestureRecognizer();
            toggle.Tapped += (_, _) => { chevron.IsExpanded = !chevron.IsExpanded; expanded.IsExpanded = chevron.IsExpanded; };
            header.GestureRecognizers.Add(toggle);
            SemanticProperties.SetDescription(header, $"Expand or collapse {category}");
            body.Add(new VerticalStackLayout { Spacing = 4, Children = { header, expanded } });
        }
        Content = body;
    }
}
