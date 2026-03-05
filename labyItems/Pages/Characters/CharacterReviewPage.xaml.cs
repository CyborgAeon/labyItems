using labyItems.Models;
using labyItems.Models.Characters;
using labyItems.Pages;
using labyItems.Pages.Battleboard;
using labyItems.Pages.Characters.ViewModels;
using labyItems.Services;
using Microsoft.Maui.ApplicationModel;

namespace labyItems.Pages.Characters;

public partial class CharacterReviewPage : ContentPage
{
    private readonly WizardVm _vm;
    private readonly CharacterDraft _draft;
    private readonly Character _character;

    public CharacterReviewPage(Character character)
    {
        InitializeComponent();

        _character = character;
        _draft = LiteDbService.ToDraft(character) ?? new CharacterDraft();
        _vm = new WizardVm(_draft);

        Title = string.IsNullOrWhiteSpace(_draft.Name) ? "Character" : _draft.Name;
        Review.BindingContext = _vm;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await _vm.RefreshReviewAsync();
        });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.RefreshReviewAsync();
    }

    private async void OnAdvanceClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new AdvanceCharacterPage(_draft));
    }

    private async void OnBattleboardClicked(object sender, EventArgs e)
    {
        await NavigateAwayFromSummaryAsync(new BattleboardPage(_draft));
    }

    private async void OnManufacturingClicked(object sender, EventArgs e)
    {
        await NavigateAwayFromSummaryAsync(new MakeSheetPage(_character));
    }

    private async Task NavigateAwayFromSummaryAsync(Page destination)
    {
        await Navigation.PushAsync(destination);
        if (Navigation.NavigationStack.Contains(this))
            Navigation.RemovePage(this);
    }
}
