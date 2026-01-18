using System.ComponentModel;
using System.Windows.Input;

namespace labyItems.Pages.Characters;

public partial class ClassCardView : ContentView
{
    public static readonly BindableProperty ToggleExpandedCommandProperty =
        BindableProperty.Create(nameof(ToggleExpandedCommand), typeof(ICommand), typeof(ClassCardView));

    public ICommand ToggleExpandedCommand
    {
        get => (ICommand)GetValue(ToggleExpandedCommandProperty);
        set => SetValue(ToggleExpandedCommandProperty, value);
    }


    public static readonly BindableProperty SelectCommandProperty =
        BindableProperty.Create(nameof(SelectCommand), typeof(ICommand), typeof(ClassCardView));
    public ICommand SelectCommand
    {
        get => (ICommand)GetValue(SelectCommandProperty);
        set => SetValue(SelectCommandProperty, value);
    }

    public ClassCardView()
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
        if (e.PropertyName == nameof(ClassCardVm.IsSelected))
            Dispatcher.Dispatch(async () => await AnimateSelectionAsync());
    }

    private async Task AnimateSelectionAsync()
    {
        if (BindingContext is not ClassCardVm vm) return;

        if (vm.IsSelected)
        {
            await CardFrame.ScaleTo(1.02, 110, Easing.CubicOut);
            await CardFrame.ScaleTo(1.0, 110, Easing.CubicOut);
        }
    }
}
