using labyItems.Models;

namespace labyItems.Pages.Calculator;

public partial class ItemSummaryPage : ContentPage
{
    private int _totalIsp;
    private int _finishedTotal;
    private List<CalcResult> _abilities = new();

    public ItemSummaryPage(int totalIsp, List<CalcResult> abilities)
    {
        InitializeComponent();
        _totalIsp = totalIsp;
        _abilities = abilities ?? new();
        DisplaySummary();
    }

    private void DisplaySummary()
    {
        // Display the finished total ISP at the top
        FinishedTotalLabel.Text = _totalIsp.ToString();

        // Build the list of contributions with running totals
        int runningTotal = 0;
        ItemsContainer.Clear();

        // Check if there's a base ISP
        var baseAbility = _abilities.FirstOrDefault(a => 
            string.Equals(a.AbilityType, "Base", StringComparison.OrdinalIgnoreCase));

        if (baseAbility != null)
        {
            runningTotal += baseAbility.TotalIsp;
            AddItemRow(baseAbility.AbilityName, baseAbility.TotalIsp, runningTotal, true);
        }

        // Add all other abilities
        foreach (var ability in _abilities.Where(a => 
            !string.Equals(a.AbilityType, "Base", StringComparison.OrdinalIgnoreCase)))
        {
            runningTotal += ability.TotalIsp;
            AddItemRow(ability.AbilityName, ability.TotalIsp, runningTotal, false);
        }

        RunningTotalLabel.Text = runningTotal.ToString();
    }

    private void AddItemRow(string name, int cost, int runningTotal, bool isBase)
    {
        var frame = new Frame
        {
            BorderColor = isBase ? "#FF9800" : "#E0E0E0",
            CornerRadius = 8,
            HasShadow = false,
            Padding = 12,
            Margin = new Thickness(0, 4),
            BackgroundColor = isBase 
                ? (App.Current?.UserAppTheme == AppTheme.Dark ? Color.FromArgb("#3E2723") : Color.FromArgb("#FFF3E0"))
                : Color.FromArgb(0)
        };

        var grid = new Grid
        {
            ColumnDefinitions = "*, Auto",
            ColumnSpacing = 12
        };

        // Left side: name and details
        var leftStack = new VerticalStackLayout { Spacing = 2 };
        
        var nameLabel = new Label
        {
            Text = name,
            FontAttributes = FontAttributes.Bold,
            FontSize = 14,
            LineBreakMode = LineBreakMode.WordWrap
        };
        leftStack.Add(nameLabel);

        // Right side: cost and running total
        var rightStack = new VerticalStackLayout
        {
            Spacing = 4,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Center
        };

        var costLabel = new Label
        {
            Text = $"ISP: {cost}",
            FontSize = 12,
            HorizontalTextAlignment = TextAlignment.End,
            TextColor = Colors.Gray
        };
        rightStack.Add(costLabel);

        var runningLabel = new Label
        {
            Text = $"Total: {runningTotal}",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.End,
            TextColor = isBase ? Color.FromArgb("#FF9800") : Colors.Black
        };
        
        // Apply dark theme color if needed
        if (App.Current?.UserAppTheme == AppTheme.Dark)
        {
            runningLabel.TextColor = Colors.White;
        }
        
        rightStack.Add(runningLabel);

        grid.Add(leftStack, 0, 0);
        grid.Add(rightStack, 1, 0);

        frame.Content = grid;
        ItemsContainer.Add(frame);
    }
}
