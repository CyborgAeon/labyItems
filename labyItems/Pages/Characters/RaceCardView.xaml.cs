using System.ComponentModel;
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


    public static readonly BindableProperty SelectCommandProperty =
        BindableProperty.Create(nameof(SelectCommand), typeof(ICommand), typeof(RaceCardView));

    public ICommand SelectCommand
    {
        get => (ICommand)GetValue(SelectCommandProperty);
        set => SetValue(SelectCommandProperty, value);
    }

    public RaceCardView()
    {
        InitializeComponent();
    }


    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (BindingContext is INotifyPropertyChanged npc)
            npc.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RaceCardVm.IsSelected))
            Dispatcher.Dispatch(async () => await AnimateSelectionAsync());
    }

    private async Task AnimateSelectionAsync()
    {
        if (BindingContext is not RaceCardVm vm) return;

        if (vm.IsSelected)
        {
            await CardFrame.ScaleTo(1.02, 110, Easing.CubicOut);
            await CardFrame.ScaleTo(1.0, 110, Easing.CubicOut);
        }
    }
}
