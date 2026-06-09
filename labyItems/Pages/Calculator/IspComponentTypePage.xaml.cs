using Microsoft.Maui.Controls.Shapes;
using System.Windows.Input;

namespace labyItems.Pages.Calculator;

public partial class IspComponentTypePage : ContentPage
{
    private readonly IspCalculator _calculator;
    public ICommand BackNavigationCommand { get; }

    private static readonly ComponentOptionDefinition[] ComponentOptions =
    {
        new(IspComponentKind.Shield, "Shield", "Add defensive shield effects", "🛡", Color.FromArgb("#D7EEF8")),
        new(IspComponentKind.Armour, "Armour", "Add armour or worn protection", "🦺", Color.FromArgb("#E8F1F8")),
        new(IspComponentKind.Weapon, "Weapon", "Add weapon abilities and enhancements", "⚔", Color.FromArgb("#F6EEE0")),
        new(IspComponentKind.Miracle, "Miracle", "Add miracles and spiritual effects", "✦", Color.FromArgb("#F3E8FF")),
        new(IspComponentKind.Spell, "Spell", "Add spells and magical utility", "✧", Color.FromArgb("#EDE9FE")),
        new(IspComponentKind.Evocation, "Evocation", "Add evocations and field effects", "🔥", Color.FromArgb("#FFE8E0")),
        new(IspComponentKind.Abilities, "Abilities", "Add general charm abilities", "🦂", Color.FromArgb("#EAF4EA")),
        new(IspComponentKind.Neuronic, "Neuronic", "Add neuronic or standard abilities", "🧠", Color.FromArgb("#E7F0FF")),
        new(IspComponentKind.Life, "Life", "Add life to the item", "❤", Color.FromArgb("#FFE4EC")),
        new(IspComponentKind.Utility, "Utility (More)", "Set status immunities, usage rules and more", "⚙", Color.FromArgb("#ECEFF3"))
    };

    public IspComponentTypePage(IspCalculator calculator)
    {
        _calculator = calculator;
        BackNavigationCommand = new Command(async () => await Navigation.PopAsync());
        InitializeComponent();
        BuildComponentButtons();
    }

    private void BuildComponentButtons()
    {
        ComponentList.Children.Clear();

        for (var index = 0; index < ComponentOptions.Length; index++)
        {
            var option = ComponentOptions[index];
            var isConfigured = _calculator.HasComponentKind(option.Kind);
            var isAvailable = _calculator.IsComponentKindAvailable(option.Kind);
            var description = ResolveDescription(option, isConfigured, isAvailable);
            var border = new Border
            {
                Padding = new Thickness(14),
                Stroke = Application.Current?.Resources.TryGetValue("SurfaceBorderColor", out var stroke) == true
                    ? (Color)stroke
                    : Color.FromArgb("#E5E7EB"),
                StrokeShape = new RoundRectangle { CornerRadius = 18 },
                Opacity = isAvailable ? 1.0 : 0.45
            };

            if (isAvailable)
            {
                border.GestureRecognizers.Add(new TapGestureRecognizer
                {
                    Command = new Command(async () =>
                    {
                        await Navigation.PopAsync();
                        await _calculator.BeginAddComponentAsync(option.Kind);
                    })
                });
            }

            var content = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 10
            };

            var iconBadge = new Border
            {
                WidthRequest = 42,
                HeightRequest = 42,
                BackgroundColor = option.IconBackground,
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                Content = new Label
                {
                    Text = option.IconGlyph,
                    FontSize = 22,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center
                }
            };

            var textStack = new VerticalStackLayout
            {
                Spacing = 4,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label
                    {
                        Text = option.Title,
                        FontAttributes = FontAttributes.Bold,
                        FontSize = 17,
                        TextColor = isAvailable
                            ? Colors.Black
                            : (Application.Current?.Resources.TryGetValue("Gray500", out var titleText) == true
                                ? (Color)titleText
                                : Color.FromArgb("#6B7280")),
                        LineBreakMode = LineBreakMode.TailTruncation
                    },
                    new Label
                    {
                        Text = description,
                        FontSize = 13,
                        TextColor = Application.Current?.Resources.TryGetValue("Gray500", out var text) == true
                            ? (Color)text
                            : Color.FromArgb("#6B7280"),
                        LineBreakMode = LineBreakMode.WordWrap,
                        MaxLines = 3
                    }
                }
            };

            var chevron = new Label
            {
                Text = isAvailable ? "\uF054" : "\uF023",
                FontFamily = "FASolid",
                FontSize = 16,
                TextColor = Application.Current?.Resources.TryGetValue("Gray500", out var chevronText) == true
                    ? (Color)chevronText
                    : Color.FromArgb("#6B7280"),
                VerticalTextAlignment = TextAlignment.Center
            };

            content.Add(iconBadge);
            content.Add(textStack, 1, 0);
            content.Add(chevron, 2, 0);

            border.Content = content;
            ComponentList.Add(border, 0, index);
        }
    }

    private string ResolveDescription(ComponentOptionDefinition option, bool isConfigured, bool isAvailable)
    {
        if (isConfigured)
            return $"Edit existing {option.Title.ToLowerInvariant()} settings";

        if (!isAvailable)
        {
            var selected = _calculator.SelectedBodyComponentTitle;
            return string.IsNullOrWhiteSpace(selected)
                ? option.Description
                : $"Unavailable while {selected.ToLowerInvariant()} is selected";
        }

        return option.Description;
    }

    private sealed record ComponentOptionDefinition(
        IspComponentKind Kind,
        string Title,
        string Description,
        string IconGlyph,
        Color IconBackground);
}
