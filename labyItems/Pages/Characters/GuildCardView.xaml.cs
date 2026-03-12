using System.ComponentModel;
using System.Windows.Input;
using labyItems.Helpers;
using labyItems.Services;
using MiracleCardPage = labyItems.Pages.MiracleCard.MiracleCard;

namespace labyItems.Pages.Characters;

public partial class GuildCardView : ContentView
{
    public static readonly BindableProperty ToggleExpandedCommandProperty =
        BindableProperty.Create(nameof(ToggleExpandedCommand), typeof(ICommand), typeof(GuildCardView));

    public ICommand ToggleExpandedCommand
    {
        get => (ICommand)GetValue(ToggleExpandedCommandProperty);
        set => SetValue(ToggleExpandedCommandProperty, value);
    }

    public static readonly BindableProperty SelectCommandProperty =
        BindableProperty.Create(nameof(SelectCommand), typeof(ICommand), typeof(GuildCardView));

    public ICommand SelectCommand
    {
        get => (ICommand)GetValue(SelectCommandProperty);
        set => SetValue(SelectCommandProperty, value);
    }

    public GuildCardView()
    {
        InitializeComponent();
    }

    private INotifyPropertyChanged? _boundVm;
    private CancellationTokenSource? _expandCts;

    protected override void OnBindingContextChanged()
    {
        if (_boundVm != null)
            _boundVm.PropertyChanged -= OnVmPropertyChanged;

        base.OnBindingContextChanged();

        _expandCts?.Cancel();
        _boundVm = BindingContext as INotifyPropertyChanged;
        if (_boundVm != null)
            _boundVm.PropertyChanged += OnVmPropertyChanged;

        if (BindingContext is GuildCardVm vm)
        {
            ExpandedContent.AbortAnimation("expand");
            ExpandedContent.IsVisible = vm.IsExpanded;
            ExpandedContent.HeightRequest = -1;
            ExpandedContent.Opacity = 1;
        }
        else
        {
            ExpandedContent.AbortAnimation("expand");
            ExpandedContent.IsVisible = false;
            ExpandedContent.HeightRequest = -1;
            ExpandedContent.Opacity = 1;
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GuildCardVm.IsSelected))
            Dispatcher.Dispatch(async () => await AnimateSelectionAsync());

        if (e.PropertyName == nameof(GuildCardVm.IsExpanded))
            Dispatcher.Dispatch(async () => await HandleExpandedChangedAsync());
    }

    private async Task AnimateSelectionAsync()
    {
        if (BindingContext is not GuildCardVm vm) return;

        if (vm.IsSelected)
        {
            await CardFrame.ScaleTo(1.02, 110, Easing.CubicOut);
            await CardFrame.ScaleTo(1.0, 110, Easing.CubicOut);
        }
    }

    private async Task HandleExpandedChangedAsync()
    {
        if (BindingContext is not GuildCardVm vm) return;

        _expandCts?.Cancel();
        _expandCts = new CancellationTokenSource();
        var token = _expandCts.Token;

        try
        {
            if (vm.IsExpanded)
            {
                await AnimateExpandedContentAsync(expand: true, token);
            }
            else
            {
                await AnimateExpandedContentAsync(expand: false, token);
            }
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

    private async Task AnimateExpandedContentAsync(bool expand, CancellationToken token)
    {
        if (ExpandedContent == null)
            return;

        ExpandedContent.AbortAnimation("expand");

        if (expand)
        {
            ExpandedContent.IsVisible = true;
            ExpandedContent.Opacity = 0;
            ExpandedContent.HeightRequest = -1;

            await Task.Yield();
            await Task.Delay(1, token);

            var width = CardExpandAnimationHelper.ResolveMeasureWidth(ExpandedContent, CardFrame, this);
            var measured = width > 0
                ? CardExpandAnimationHelper.MeasureContentHeight(ExpandedContent, width)
                : -1;

            if (measured <= 0)
            {
                ExpandedContent.Opacity = 1;
                ExpandedContent.HeightRequest = -1;
                return;
            }

            ExpandedContent.HeightRequest = 0;
            ExpandedContent.Opacity = 0;
            await CardExpandAnimationHelper.AnimateHeightAsync(
                owner: this,
                target: ExpandedContent,
                animationName: "expand",
                from: 0,
                to: measured,
                length: 240,
                easing: Easing.CubicOut,
                onStep: v => ExpandedContent.Opacity = Math.Min(1, v / measured),
                cancellationToken: token);

            if (token.IsCancellationRequested) return;

            ExpandedContent.HeightRequest = -1;
            ExpandedContent.Opacity = 1;
            return;
        }

        if (!ExpandedContent.IsVisible)
            return;

        var startHeight = ExpandedContent.Height;
        if (startHeight <= 0)
        {
            ExpandedContent.IsVisible = false;
            ExpandedContent.HeightRequest = -1;
            ExpandedContent.Opacity = 1;
            return;
        }

        ExpandedContent.HeightRequest = startHeight;
        await CardExpandAnimationHelper.AnimateHeightAsync(
            owner: this,
            target: ExpandedContent,
            animationName: "expand",
            from: startHeight,
            to: 0,
            length: 200,
            easing: Easing.CubicIn,
            onStep: v => ExpandedContent.Opacity = startHeight <= 0 ? 0 : Math.Max(0, v / startHeight),
            cancellationToken: token);

        if (token.IsCancellationRequested) return;

        ExpandedContent.IsVisible = false;
        ExpandedContent.HeightRequest = -1;
        ExpandedContent.Opacity = 1;
    }

    private async void OnGuildMiracleInfoClicked(object sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not GuildMiracleRowVm row)
            return;

        var miracleName = (row.Name ?? string.Empty).Trim();
        if (miracleName.Length == 0)
            return;

        var miracles = await MiracleService.GetAllAsync();
        var miracle = miracles.FirstOrDefault(m =>
            string.Equals(m?.name ?? string.Empty, miracleName, StringComparison.OrdinalIgnoreCase));

        if (miracle == null)
            return;

        var navigation = Navigation;
        if (navigation == null)
            return;

        await navigation.PushModalAsync(new NavigationPage(new MiracleCardPage(miracle)));
    }
}
