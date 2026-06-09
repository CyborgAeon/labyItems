using System.Linq;
using System.ComponentModel;
using labyItems.Helpers;
using labyItems.Pages.Characters.ViewModels;
using labyItems.Services;
using Microsoft.Maui.Controls;
using AbilityCardPage = labyItems.Pages.AbilityCard.AbilityCard;
using SpecialisationCardPage = labyItems.Pages.SpecialisationCard.SpecialisationCard;

namespace labyItems.Pages.Characters;

public partial class CharacterReviewView : ContentView
{
    private enum ReviewEditDestination
    {
        Details,
        Race,
        Class,
        Specialisation
    }

    public static readonly BindableProperty ShowSaveButtonProperty = BindableProperty.Create(
        nameof(ShowSaveButton),
        typeof(bool),
        typeof(CharacterReviewView),
        true);

    public static readonly BindableProperty ShowPost8CardProperty = BindableProperty.Create(
        nameof(ShowPost8Card),
        typeof(bool),
        typeof(CharacterReviewView),
        true);

    public bool ShowSaveButton
    {
        get => (bool)GetValue(ShowSaveButtonProperty);
        set => SetValue(ShowSaveButtonProperty, value);
    }

    public bool ShowPost8Card
    {
        get => (bool)GetValue(ShowPost8CardProperty);
        set => SetValue(ShowPost8CardProperty, value);
    }

    public CharacterReviewView(object bindingContext)
    {
        InitializeComponent();
        AttachLifecycleHandlers();
        BindingContext = bindingContext;
    }

    public CharacterReviewView()
    {
        InitializeComponent();
        AttachLifecycleHandlers();
    }

    private INotifyPropertyChanged? _boundVm;
    private CancellationTokenSource? _post8ExpandCts;

    protected override void OnBindingContextChanged()
    {
        if (_boundVm != null)
            _boundVm.PropertyChanged -= OnVmPropertyChanged;

        base.OnBindingContextChanged();

        _post8ExpandCts?.Cancel();
        _boundVm = BindingContext as INotifyPropertyChanged;
        if (_boundVm != null)
            _boundVm.PropertyChanged += OnVmPropertyChanged;

        if (BindingContext is WizardVm vm)
        {
            Post8ExpandedContent.AbortAnimation("post8-expand");
            Post8ExpandedContent.IsVisible = vm.IsAdvancementExpanded;
            Post8ExpandedContent.HeightRequest = -1;
            Post8ExpandedContent.Opacity = 1;
        }
        else
        {
            Post8ExpandedContent.AbortAnimation("post8-expand");
            Post8ExpandedContent.IsVisible = false;
            Post8ExpandedContent.HeightRequest = -1;
            Post8ExpandedContent.Opacity = 1;
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WizardVm.IsAdvancementExpanded))
        {
            UiDispatchHelper.BeginOnMainThread(() =>
                UiDispatchHelper.RunFireAndForget(
                    HandleAdvancementExpandedChangedAsync,
                    "CHARACTER_REVIEW_ADVANCEMENT_EXPAND_ANIMATION"));
        }
    }

    private async Task HandleAdvancementExpandedChangedAsync()
    {
        if (BindingContext is not WizardVm vm) return;

        var previousCts = _post8ExpandCts;
        previousCts?.Cancel();
        _post8ExpandCts = new CancellationTokenSource();
        previousCts?.Dispose();
        var token = _post8ExpandCts.Token;

        try
        {
            await AnimatePost8ExpandedContentAsync(vm.IsAdvancementExpanded, token);
        }
        catch (TaskCanceledException)
        {
            // Ignore rapid expand/collapse interactions.
        }
        catch (OperationCanceledException)
        {
            // Ignore rapid expand/collapse interactions.
        }
        catch (Exception ex)
        {
            RuntimeLog.Write(
                "CHARACTER_REVIEW_ADVANCEMENT_EXPAND_ANIMATION",
                "Post-8 advancement expand animation failed.",
                ex);
        }
    }

    private async Task AnimatePost8ExpandedContentAsync(bool expand, CancellationToken token)
    {
        if (Post8ExpandedContent == null)
            return;

        Post8ExpandedContent.AbortAnimation("post8-expand");

        if (expand)
        {
            Post8ExpandedContent.IsVisible = true;
            Post8ExpandedContent.Opacity = 0;
            Post8ExpandedContent.HeightRequest = -1;

            await Task.Yield();
            await Task.Delay(1, token);

            var width = CardExpandAnimationHelper.ResolveMeasureWidth(Post8ExpandedContent, Post8CardFrame, this);
            var measured = width > 0
                ? CardExpandAnimationHelper.MeasureContentHeight(Post8ExpandedContent, width)
                : -1;

            if (measured <= 0)
            {
                Post8ExpandedContent.Opacity = 1;
                Post8ExpandedContent.HeightRequest = -1;
                return;
            }

            Post8ExpandedContent.HeightRequest = 0;
            Post8ExpandedContent.Opacity = 0;
            await CardExpandAnimationHelper.AnimateHeightAsync(
                owner: this,
                target: Post8ExpandedContent,
                animationName: "post8-expand",
                from: 0,
                to: measured,
                length: 240,
                easing: Easing.CubicOut,
                onStep: v => Post8ExpandedContent.Opacity = Math.Min(1, v / measured),
                cancellationToken: token);

            if (token.IsCancellationRequested) return;

            Post8ExpandedContent.HeightRequest = -1;
            Post8ExpandedContent.Opacity = 1;
            return;
        }

        if (!Post8ExpandedContent.IsVisible)
            return;

        var startHeight = Post8ExpandedContent.Height;
        if (startHeight <= 0)
        {
            Post8ExpandedContent.IsVisible = false;
            Post8ExpandedContent.HeightRequest = -1;
            Post8ExpandedContent.Opacity = 1;
            return;
        }

        Post8ExpandedContent.HeightRequest = startHeight;
        await CardExpandAnimationHelper.AnimateHeightAsync(
            owner: this,
            target: Post8ExpandedContent,
            animationName: "post8-expand",
            from: startHeight,
            to: 0,
            length: 200,
            easing: Easing.CubicIn,
            onStep: v => Post8ExpandedContent.Opacity = startHeight <= 0 ? 0 : Math.Max(0, v / startHeight),
            cancellationToken: token);

        if (token.IsCancellationRequested) return;

        Post8ExpandedContent.IsVisible = false;
        Post8ExpandedContent.HeightRequest = -1;
        Post8ExpandedContent.Opacity = 1;
    }

    private async void OnViewSpecialisationAbilityDetailsClicked(object sender, EventArgs e)
    {
        if (sender is not Button button)
            return;

        if (button.CommandParameter is not WizardVm.SpecialisationSummaryLineVm line || !line.HasDetails)
            return;

        var nav = ResolveNavigation();
        if (nav == null)
            return;

        var keysToTry = new List<string>();
        var directKey = (line.SpecialisationKey ?? string.Empty).Trim();
        if (directKey.Length > 0)
            keysToTry.Add(directKey);

        var parsedFromSummary = ExtractSpecialisationTitleFromSummary(line.Text);
        if (parsedFromSummary.Length > 0
            && !keysToTry.Contains(parsedFromSummary, StringComparer.OrdinalIgnoreCase))
        {
            keysToTry.Add(parsedFromSummary);
        }

        foreach (var key in keysToTry)
        {
            var match = await DetailCardLookupService.FindSpecialisationAsync(key);
            if (!string.IsNullOrWhiteSpace(match.Key) && match.Record != null)
            {
                await nav.PushAsync(new SpecialisationCardPage(match.Key, match.Record, line.SelectedOption));
                return;
            }
        }

        if (line.Ability != null)
            await nav.PushAsync(new AbilityCardPage(line.Ability));
    }

    private async void OnEditIdentityClicked(object sender, EventArgs e)
    {
        await NavigateToEditDestinationAsync(ReviewEditDestination.Details);
    }

    private async void OnEditDetailsClicked(object sender, EventArgs e)
    {
        await NavigateToEditDestinationAsync(ReviewEditDestination.Details);
    }

    private async void OnEditRaceClicked(object sender, EventArgs e)
    {
        await NavigateToEditDestinationAsync(ReviewEditDestination.Race);
    }

    private async void OnEditClassClicked(object sender, EventArgs e)
    {
        await NavigateToEditDestinationAsync(ReviewEditDestination.Class);
    }

    private async void OnEditSpecialisationClicked(object sender, EventArgs e)
    {
        await NavigateToEditDestinationAsync(ReviewEditDestination.Specialisation);
    }

    private async void OnEditGuildChoicesClicked(object sender, EventArgs e)
    {
        if (BindingContext is not WizardVm vm)
            return;

        if (sender is not Button button || button.CommandParameter is not GuildReviewSummaryRowVm row)
            return;

        if (!row.HasChoices)
            return;

        var hostPage = ResolveHostPage();
        if (hostPage == null)
            return;

        await GuildBenefitChoicePromptHelper.EditChoicesForGuildAsync(
            hostPage,
            vm.GuildsVm,
            row.GuildName,
            refreshAfterSelection: () => vm.RefreshReviewAsync());
    }

    private async void OnViewGuildDetailsClicked(object sender, EventArgs e)
    {
        if (BindingContext is not WizardVm vm)
            return;

        if (sender is not Button button || button.CommandParameter is not GuildReviewSummaryRowVm row)
            return;

        var detailCard = await vm.GuildsVm.BuildGuildDetailCardAsync(row.GuildName, expand: true);
        if (detailCard == null)
            return;

        var navigation = ResolveNavigation();
        if (navigation == null)
            return;

        var detailView = new GuildCardView
        {
            BindingContext = detailCard,
            ToggleExpandedCommand = new Command<GuildCardVm>(card =>
            {
                if (card == null)
                    return;
                card.IsExpanded = !card.IsExpanded;
            }),
            SelectCommand = new Command<GuildCardVm>(_ => { })
        };

        var page = new ContentPage
        {
            Title = detailCard.Name,
            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = new Thickness(16, 16, 16, 24),
                    Children = { detailView }
                }
            }
        };

        await navigation.PushAsync(page);
    }

    private async void OnGetBattleboardClicked(object sender, EventArgs e)
    {
        if (BindingContext is not WizardVm vm)
            return;

        var page = ResolveHostPage();
        if (page == null)
            return;

        var choicesComplete = await GuildBenefitChoicePromptHelper.EnsureChoicesCompletedAsync(
            page,
            vm.GuildsVm,
            refreshAfterSelection: () => vm.RefreshReviewAsync(),
            actionLabel: "getting battleboard output");
        if (!choicesComplete)
            return;

        const string cancel = "cancel";
        const string emailToDesk = "email to desk";
        const string downloadPdf = "download as pdf";
        const string downloadExcel = "download as excel";

        var selected = await page.DisplayActionSheet(
            "Get battleboard",
            cancel,
            null,
            emailToDesk,
            downloadPdf,
            downloadExcel);

        if (string.IsNullOrWhiteSpace(selected) || selected.Equals(cancel, StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            if (selected.Equals(emailToDesk, StringComparison.OrdinalIgnoreCase))
            {
                await vm.EmailBattleboardPdfToDeskAsync();
                return;
            }

            if (selected.Equals(downloadPdf, StringComparison.OrdinalIgnoreCase))
            {
                await vm.DownloadBattleboardAsPdfAsync();
                return;
            }

            if (selected.Equals(downloadExcel, StringComparison.OrdinalIgnoreCase))
            {
                await vm.DownloadBattleboardAsExcelAsync();
            }
        }
        catch (Exception ex)
        {
            RuntimeLog.Write("BATTLEBOARD_EXPORT", "Battleboard export action failed.", ex);
            await page.DisplayAlert(
                "Battleboard export failed",
                BuildDetailedExceptionMessage(ex),
                "OK");
        }
    }

    private async Task NavigateToEditDestinationAsync(ReviewEditDestination destination)
    {
        if (BindingContext is not WizardVm vm)
            return;

        var hostPage = ResolveHostPage();
        if (hostPage is Wizard)
        {
            await vm.NavigateToEditTargetAsync(MapDestination(destination));
            return;
        }

        var nav = ResolveNavigation();
        if (nav == null)
            return;

        var wizardPage = new Wizard(vm.Draft, async () => await nav.PopAsync());
        await nav.PushAsync(wizardPage);

        if (wizardPage.BindingContext is WizardVm wizardVm)
            await wizardVm.NavigateToEditTargetAsync(MapDestination(destination));
    }

    private static WizardEditTarget MapDestination(ReviewEditDestination destination)
    {
        switch (destination)
        {
            case ReviewEditDestination.Details:
                return WizardEditTarget.Details;
            case ReviewEditDestination.Race:
                return WizardEditTarget.Race;
            case ReviewEditDestination.Class:
                return WizardEditTarget.Class;
            case ReviewEditDestination.Specialisation:
                return WizardEditTarget.Specialisation;
            default:
                return WizardEditTarget.Details;
        }
    }

    private static string ExtractSpecialisationTitleFromSummary(string? summaryText)
    {
        var text = (summaryText ?? string.Empty).Trim();
        if (text.Length == 0)
            return string.Empty;

        var colonIndex = text.IndexOf(':');
        if (colonIndex > 0)
            text = text[..colonIndex].Trim();

        var levelSuffixIndex = text.IndexOf(" (Lvl", StringComparison.OrdinalIgnoreCase);
        if (levelSuffixIndex > 0)
            text = text[..levelSuffixIndex].Trim();

        return text;
    }

    private INavigation? ResolveNavigation()
    {
        if (Navigation?.NavigationStack is { Count: > 0 })
            return Navigation;

        if (Shell.Current?.Navigation is { } shellNav)
            return shellNav;

        return Application.Current?.MainPage?.Navigation;
    }

    private Page? ResolveHostPage()
    {
        Element? current = this;
        while (current != null)
        {
            if (current is Page page)
                return page;

            current = current.Parent;
        }

        return Application.Current?.MainPage;
    }

    private static string BuildDetailedExceptionMessage(Exception ex)
    {
        var parts = new List<string>();
        var current = ex;
        while (current != null)
        {
            var message = (current.Message ?? string.Empty).Trim();
            if (message.Length > 0)
                parts.Add(message);

            current = current.InnerException;
        }

        if (parts.Count == 0)
            return "An unknown error occurred. Check runtime.log for details.";

        var combined = string.Join("\n\n", parts.Distinct(StringComparer.Ordinal));
        return $"{combined}\n\nLog: {RuntimeLog.LogPath}";
    }

    private void AttachLifecycleHandlers()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        if (_boundVm != null)
            return;

        _boundVm = BindingContext as INotifyPropertyChanged;
        if (_boundVm != null)
            _boundVm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        if (_boundVm != null)
            _boundVm.PropertyChanged -= OnVmPropertyChanged;

        _boundVm = null;
        _post8ExpandCts?.Cancel();
        _post8ExpandCts?.Dispose();
        _post8ExpandCts = null;

        Post8ExpandedContent?.AbortAnimation("post8-expand");
    }
}
