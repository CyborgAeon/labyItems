using System.Threading.Tasks;
using System.Windows.Input;
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

    protected override bool OnBackButtonPressed()
    {
#if ANDROID
        if (BindingContext is WizardVm vm && vm.CanGoBack)
        {
            if (vm.BackCommand is ICommand cmd && cmd.CanExecute(null))
                cmd.Execute(null);

            return true;
        }
#endif
        return base.OnBackButtonPressed();
    }

}
