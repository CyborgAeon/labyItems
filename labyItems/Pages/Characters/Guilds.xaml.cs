using labyItems.Pages.Characters.ViewModels;

namespace labyItems.Pages.Characters;

public partial class Guilds : ContentView
{
    public Guilds(GuildsVm vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
