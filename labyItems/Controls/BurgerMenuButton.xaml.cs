using System.Windows.Input;

namespace labyItems.Controls;

public partial class BurgerMenuButton : ContentView
{
    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(BurgerMenuButton));

    public BurgerMenuButton()
    {
        InitializeComponent();
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }
}
