using System.Threading.Tasks;
using labyItems.Models.Characters;
using labyItems.Pages.Characters.ViewModels;

namespace labyItems.Pages.Characters;

public partial class Wizard : ContentPage
{
    public Wizard(CharacterDraft? draft = null, Func<Task>? onFinished = null)
    {
        InitializeComponent();
        BindingContext = new WizardVm(draft, onFinished);
    }
}
