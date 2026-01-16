using labyItems.Pages.Characters.ViewModels;

namespace labyItems.Pages.Characters;

public partial class Wizard : ContentPage
{
    public Wizard()
    {
        InitializeComponent();
        BindingContext = new WizardVm();
    }
}