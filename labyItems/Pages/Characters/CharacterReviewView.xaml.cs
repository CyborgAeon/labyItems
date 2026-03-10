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
        BindingContext = bindingContext;
    }

    public CharacterReviewView()
    {
        InitializeComponent();
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
            Dispatcher.Dispatch(async () => await HandleAdvancementExpandedChangedAsync());
    }

    private async Task HandleAdvancementExpandedChangedAsync()
    {
        if (BindingContext is not WizardVm vm) return;

        _post8ExpandCts?.Cancel();
        _post8ExpandCts = new CancellationTokenSource();
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

        if (!string.IsNullOrWhiteSpace(line.SpecialisationKey))
        {
            var match = await DetailCardLookupService.FindSpecialisationAsync(line.SpecialisationKey);
            if (!string.IsNullOrWhiteSpace(match.Key) && match.Record != null)
            {
                await nav.PushAsync(new SpecialisationCardPage(match.Key, match.Record, line.SelectedOption));
                return;
            }
        }

        if (line.Ability != null)
            await nav.PushAsync(new AbilityCardPage(line.Ability));
    }

    private INavigation? ResolveNavigation()
    {
        if (Navigation?.NavigationStack is { Count: > 0 })
            return Navigation;

        if (Shell.Current?.Navigation is { } shellNav)
            return shellNav;

        return Application.Current?.MainPage?.Navigation;
    }
}
