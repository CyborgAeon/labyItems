using labyItems.Models;
using labyItems.Models.Characters;
using labyItems.Pages.Characters.ViewModels;
using labyItems.Services;
using Microsoft.Maui.ApplicationModel;

namespace labyItems.Pages.Characters;

public partial class CharacterReviewPage : ContentPage
{
    private readonly WizardVm _vm;

    public CharacterReviewPage(Character character)
    {
        InitializeComponent();

        var draft = LiteDbService.ToDraft(character) ?? new CharacterDraft();
        _vm = new WizardVm(draft);

        Title = string.IsNullOrWhiteSpace(draft.Name) ? "Character" : draft.Name;
        Review.BindingContext = _vm;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await _vm.CharacterBuilderVm.RefreshDraftAbilitiesAsync();
        });
    }
}
