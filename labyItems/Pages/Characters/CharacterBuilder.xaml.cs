using labyItems.Models;
using labyItems.Pages;
using labyItems.Services;

namespace labyItems.Pages.Characters;

public partial class CharacterBuilder : ContentView
{
    public CharacterBuilder()
    {
        InitializeComponent();
    }

    public CharacterBuilder(CharacterBuilderVm vm) : this()
    {
        BindingContext = vm;
    }

    private void OnClassFilterChipTapped(object? sender, TappedEventArgs e)
    {
        var chip = e.Parameter as CharacterBuilderVm.ClassFilterChipVm
            ?? (sender as BindableObject)?.BindingContext as CharacterBuilderVm.ClassFilterChipVm;

        if (chip == null || BindingContext is not CharacterBuilderVm vm)
            return;

        var command = vm.ToggleClassFilterChipCommand;
        if (command.CanExecute(chip))
            command.Execute(chip);
    }
}
