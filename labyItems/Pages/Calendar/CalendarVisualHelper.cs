using labyItems.Services;
using Microsoft.Maui.Controls.Shapes;

namespace labyItems.Pages.Calendar;

internal static class CalendarVisualHelper
{
    public static void PopulateLegend(VerticalStackLayout host)
    {
        host.Children.Clear();

        host.Children.Add(new Label
        {
            Text = "Key",
            FontAttributes = FontAttributes.Bold,
            FontSize = 14,
            TextColor = ResolveColor("Gray950", Colors.Black)
        });

        var legendGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            RowSpacing = 8,
            ColumnSpacing = 10
        };

        var items = new[]
        {
            BuildLegendItem("Player", ResolveColor("CalendarBookedPlayerColor", Colors.Maroon)),
            BuildLegendItem("Referee", ResolveColor("CalendarBookedRefereeColor", Colors.Teal)),
            BuildLegendItem("A-ref", ResolveColor("CalendarBookedARefColor", Colors.Goldenrod)),
            BuildLegendItem("Crew", ResolveColor("CalendarBookedCrewColor", Colors.MediumPurple)),
            BuildLegendItem("Available", ResolveColor("Gray100", Color.FromArgb("#EDEDED"))),
            BuildLegendItem("Disabled", ResolveColor("CalendarDisabledColor", Color.FromArgb("#D4D4D8")))
        };

        for (var index = 0; index < items.Length; index++)
            legendGrid.Add(items[index], index % 2, index / 2);

        host.Children.Add(legendGrid);
    }

    public static Border BuildEventCard(CalendarEventRecord evt, Func<Task> onTapped)
    {
        var border = new Border
        {
            Padding = 16,
            Stroke = ResolveEventStrokeColor(evt),
            BackgroundColor = ResolveEventBackgroundColor(evt),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(18) },
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    BuildHeader(evt),
                    new Label { Text = evt.MonthDayLabel, FontSize = 14 },
                    new Label { Text = evt.Location, FontSize = 14 },
                    BuildRoleSummary(evt),
                    new Label { Text = evt.SummaryLine, FontSize = 13, TextColor = ResolveColor("Gray500", Colors.Gray) },
                    new Label { Text = evt.AreaSummary, FontSize = 13, FontAttributes = FontAttributes.Italic, TextColor = ResolveColor("Gray500", Colors.Gray) }
                }
            }
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, __) => await onTapped();
        border.GestureRecognizers.Add(tap);
        return border;
    }

    public static Color ResolveDayBackgroundColor(bool isSelected, bool hasEvents, CalendarEventRole? bookedRole, bool isDisabled)
    {
        if (isSelected)
            return ResolveColor("Primary", Colors.Maroon);
        if (isDisabled)
            return ResolveColor("CalendarDisabledColor", Color.FromArgb("#D4D4D8"));
        if (bookedRole.HasValue)
            return ResolveColor(bookedRole.Value.GetCalendarColorKey(), Colors.Maroon);
        if (hasEvents)
            return ResolveColor("Gray100", Color.FromArgb("#EDEDED"));

        return ResolveColor("White", Colors.White);
    }

    public static Color ResolveDayTextColor(bool isSelected, CalendarEventRole? bookedRole, bool isDisabled)
    {
        if (isSelected || bookedRole.HasValue)
            return ResolveColor("White", Colors.White);
        if (isDisabled)
            return ResolveColor("CalendarDisabledTextColor", Colors.Gray);

        return ResolveColor("Gray950", Colors.Black);
    }

    public static Color ResolveColor(string key, Color fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var resource) == true)
        {
            if (resource is Color color)
                return color;

            if (resource is SolidColorBrush brush)
                return brush.Color;
        }

        return fallback;
    }

    private static View BuildRoleSummary(CalendarEventRecord evt)
    {
        if (!evt.MyBookedRole.HasValue)
        {
            return new Label
            {
                Text = "Available to book",
                FontSize = 13,
                TextColor = ResolveColor("Gray500", Colors.Gray)
            };
        }

        return new Label
        {
            Text = $"Booked as {evt.MyBookedRole.Value.GetDisplayName()}",
            FontAttributes = FontAttributes.Bold,
            FontSize = 13,
            TextColor = ResolveColor(evt.MyBookedRole.Value.GetCalendarColorKey(), Colors.Maroon)
        };
    }

    private static View BuildLegendItem(string label, Color color)
    {
        return new HorizontalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Border
                {
                    WidthRequest = 16,
                    HeightRequest = 16,
                    StrokeThickness = 0,
                    StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(8) },
                    BackgroundColor = color,
                    VerticalOptions = LayoutOptions.Center
                },
                new Label
                {
                    Text = label,
                    FontSize = 13,
                    VerticalTextAlignment = TextAlignment.Center
                }
            }
        };
    }

    private static Color ResolveEventStrokeColor(CalendarEventRecord evt)
        => evt.MyBookedRole.HasValue
            ? ResolveColor(evt.MyBookedRole.Value.GetCalendarColorKey(), Colors.Maroon)
            : ResolveColor("SurfaceBorderColor", Colors.LightGray);

    private static Color ResolveEventBackgroundColor(CalendarEventRecord evt)
        => evt.MyBookedRole.HasValue
            ? ResolveColor(evt.MyBookedRole.Value.GetCalendarColorKey(), Colors.Maroon).WithAlpha(0.08f)
            : ResolveColor("White", Colors.White);

    private static View BuildHeader(CalendarEventRecord evt)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 8
        };

        grid.Add(new Label
        {
            Text = evt.Name,
            FontAttributes = FontAttributes.Bold,
            FontSize = 20,
            LineBreakMode = LineBreakMode.WordWrap,
            MaxLines = 2,
            VerticalTextAlignment = TextAlignment.Start
        });

        var badge = new Border
        {
            BackgroundColor = ResolveColor("Primary", Colors.Maroon),
            Padding = new Thickness(10, 4),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(999) },
            VerticalOptions = LayoutOptions.Start,
            HorizontalOptions = LayoutOptions.End,
            Content = new Label
            {
                Text = evt.ThresholdBadge,
                FontAttributes = FontAttributes.Bold,
                FontSize = 12,
                TextColor = ResolveColor("White", Colors.White)
            }
        };
        grid.Add(badge);
        Grid.SetColumn(badge, 1);

        return grid;
    }
}
