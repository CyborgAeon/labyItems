using labyItems.Models;
using labyItems.Models.Characters;
using labyItems.Pages.Characters.ViewModels;
using labyItems.Services;
using Microsoft.Maui.ApplicationModel;

namespace labyItems.Pages.Characters;

public partial class CharacterReviewPage : ContentPage
{
    private readonly WizardVm _vm;
    private readonly CharacterDraft _draft;

    public CharacterReviewPage(Character character)
    {
        InitializeComponent();

        _draft = LiteDbService.ToDraft(character) ?? new CharacterDraft();
        _vm = new WizardVm(_draft);

        Title = string.IsNullOrWhiteSpace(_draft.Name) ? "Character" : _draft.Name;
        Review.BindingContext = _vm;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await _vm.CharacterBuilderVm.RefreshDraftAbilitiesAsync();
        });
    }

    private async void OnAdvanceClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new AdvanceCharacterPage(_draft));
    }
}
