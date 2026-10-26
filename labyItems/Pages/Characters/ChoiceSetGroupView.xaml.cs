using labyItems.Pages.Characters.ViewModels;
using labyItems.Services;
using AbilityCardPage = labyItems.Pages.AbilityCard.AbilityCard;
using SpecialisationCardPage = labyItems.Pages.SpecialisationCard.SpecialisationCard;

namespace labyItems.Pages.Characters;

public partial class ChoiceSetGroupView : ContentView
{
    public ChoiceSetGroupView()
    {
        InitializeComponent();
    }

    private async void OnAbilityInfoTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not ChoiceSetAbilityRowVm row || !row.IsValid)
            return;

        var detail = await DetailCardLookupService.ResolveDetailAsync(row.LookupKey, row.DisplayName);
        var navigation = Navigation?.NavigationStack.Count > 0
            ? Navigation
            : Shell.Current?.Navigation;
        if (navigation == null)
            return;

        if (detail?.Ability != null)
            await navigation.PushAsync(new AbilityCardPage(detail.Ability));
        else if (detail?.Specialisation != null)
            await navigation.PushAsync(new SpecialisationCardPage(detail.Key, detail.Specialisation));
    }
}
