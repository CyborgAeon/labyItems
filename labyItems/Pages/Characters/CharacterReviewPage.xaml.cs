using labyItems.Models;
using labyItems.Models.Characters;
using labyItems.Pages;
using labyItems.Pages.Battleboard;
using labyItems.Pages.Characters.ViewModels;
using labyItems.Helpers;
using labyItems.Services;
using Microsoft.Maui.ApplicationModel;

namespace labyItems.Pages.Characters;

public partial class CharacterReviewPage : ContentPage
{
    private readonly WizardVm _vm;
    private readonly CharacterDraft _draft;
    private readonly Character _character;
    private bool _isNavigatingBack;
    private bool _isActionMenuExpanded;
    private bool _isActionMenuAnimating;

    public CharacterReviewPage(Character character)
    {
        InitializeComponent();
        ApplyActionButtonOrdering();

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

    protected override bool OnBackButtonPressed()
    {
        if (_isActionMenuExpanded)
        {
            _ = CollapseActionMenuAsync();
            return true;
        }

        return base.OnBackButtonPressed();
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        if (_isNavigatingBack)
            return;

        _isNavigatingBack = true;
        try
        {
            await CloseActionMenuIfOpenAsync();

            if (Navigation.NavigationStack.Count > 1)
            {
                await Navigation.PopAsync();
                return;
            }

            if (Shell.Current != null)
                await Shell.Current.GoToAsync("..");
        }
        finally
        {
            _isNavigatingBack = false;
        }
    }

    private async void OnActionsChevronClicked(object sender, EventArgs e)
    {
        if (_isActionMenuExpanded)
            await CollapseActionMenuAsync();
        else
            await ExpandActionMenuAsync();
    }

    private async void OnActionBackdropTapped(object sender, TappedEventArgs e)
    {
        await CollapseActionMenuAsync();
    }

    private async Task ExpandActionMenuAsync()
    {
        if (_isActionMenuAnimating || _isActionMenuExpanded)
            return;

        _isActionMenuAnimating = true;
        try
        {
            _isActionMenuExpanded = true;
            ActionsChevronButton.Text = "▴";

            ActionBackdrop.IsVisible = true;
            ActionBackdrop.InputTransparent = false;
            ExpandedActionsPanel.IsVisible = true;
            ExpandedActionsPanel.Opacity = 0;
            ExpandedActionsPanel.TranslationY = -8;
            CollapsedActionIcons.InputTransparent = true;

            await Task.WhenAll(
                CardExpandAnimationHelper.FadeAsync(ActionBackdrop, 1),
                CardExpandAnimationHelper.FadeAsync(CollapsedActionIcons, 0),
                CardExpandAnimationHelper.FadeAsync(ExpandedActionsPanel, 1),
                CardExpandAnimationHelper.TranslateYAsync(ExpandedActionsPanel, 0));
        }
        finally
        {
            _isActionMenuAnimating = false;
        }
    }

    private async Task CollapseActionMenuAsync()
    {
        if (_isActionMenuAnimating || !_isActionMenuExpanded)
            return;

        _isActionMenuAnimating = true;
        try
        {
            _isActionMenuExpanded = false;
            ActionsChevronButton.Text = "▾";

            await Task.WhenAll(
                CardExpandAnimationHelper.FadeAsync(ActionBackdrop, 0),
                CardExpandAnimationHelper.FadeAsync(CollapsedActionIcons, 1),
                CardExpandAnimationHelper.FadeAsync(ExpandedActionsPanel, 0),
                CardExpandAnimationHelper.TranslateYAsync(ExpandedActionsPanel, -8));

            ActionBackdrop.IsVisible = false;
            ActionBackdrop.InputTransparent = true;
            ExpandedActionsPanel.IsVisible = false;
            ExpandedActionsPanel.TranslationY = 0;
            CollapsedActionIcons.InputTransparent = false;
        }
        finally
        {
            _isActionMenuAnimating = false;
        }
    }

    private async Task CloseActionMenuIfOpenAsync()
    {
        if (_isActionMenuExpanded)
            await CollapseActionMenuAsync();
    }

    private async void OnExportExcelClicked(object sender, EventArgs e)
    {
        await CloseActionMenuIfOpenAsync();

        var choicesComplete = await GuildBenefitChoicePromptHelper.EnsureChoicesCompletedAsync(
            this,
            _vm.GuildsVm,
            refreshAfterSelection: _vm.RefreshReviewAsync,
            actionLabel: "exporting the battleboard");
        if (!choicesComplete)
            return;

        await _vm.DownloadBattleboardAsExcelAsync();
    }

    private async void OnAdvanceClicked(object sender, EventArgs e)
    {
        await CloseActionMenuIfOpenAsync();
        await Navigation.PushAsync(new AdvanceCharacterPage(_draft));
    }

    private async void OnBattleboardClicked(object sender, EventArgs e)
    {
        await CloseActionMenuIfOpenAsync();

        var choicesComplete = await GuildBenefitChoicePromptHelper.EnsureChoicesCompletedAsync(
            this,
            _vm.GuildsVm,
            refreshAfterSelection: _vm.RefreshReviewAsync,
            actionLabel: "opening the battleboard");
        if (!choicesComplete)
            return;

        await NavigateAwayFromSummaryAsync(new BattleboardPage(_draft));
    }

    private async void OnManufacturingClicked(object sender, EventArgs e)
    {
        await CloseActionMenuIfOpenAsync();
        await NavigateAwayFromSummaryAsync(new MakeSheetPage(_character));
    }

    private async Task NavigateAwayFromSummaryAsync(Page destination)
    {
        await Navigation.PushAsync(destination);
        if (Navigation.NavigationStack.Contains(this))
            Navigation.RemovePage(this);
    }

    private void ApplyActionButtonOrdering()
    {
        ApplyActionButtonOrdering(CollapsedActionIcons);
        ApplyActionButtonOrdering(ExpandedActionsPanel);
    }

    private static void ApplyActionButtonOrdering(Layout layout)
    {
        if (layout.Children.Count <= 1)
            return;

        var ordered = CharacterActionOrderHelper.GetOrderedViewsByStyleId(layout.Children);
        layout.Children.Clear();
        foreach (var child in ordered)
            layout.Children.Add(child);
    }
}
