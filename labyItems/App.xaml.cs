using labyItems.Models;
using labyItems.Pages;

namespace labyItems;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        MainPage = new NavigationPage(new CharactersPage()); // first screen
    }
}

