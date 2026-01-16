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

    public ClassCardView()
    {
        InitializeComponent();
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (BindingContext is ClassCardVm vm)
            Dispatcher.Dispatch(async () => await vm.EnsureProgressionLoadedAsync());
    }
}
