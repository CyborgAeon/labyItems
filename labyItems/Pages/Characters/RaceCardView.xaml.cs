using System.Windows.Input;

namespace labyItems.Pages.Characters;

public partial class RaceCardView : ContentView
{
    public static readonly BindableProperty ToggleRaceExpandedCommandProperty =
        BindableProperty.Create(nameof(ToggleRaceExpandedCommand), typeof(ICommand), typeof(RaceCardView));

    public ICommand ToggleRaceExpandedCommand
    {
        get => (ICommand)GetValue(ToggleRaceExpandedCommandProperty);
        set => SetValue(ToggleRaceExpandedCommandProperty, value);
    }

    public RaceCardView()
    {
        InitializeComponent();
    }
}
