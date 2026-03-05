using System.ComponentModel;
using System.Windows.Input;

namespace labyItems.Pages.Characters;

public partial class ClassCardView : ContentView
{
    public static readonly BindableProperty ToggleExpandedCommandProperty =
        BindableProperty.Create(nameof(ToggleExpandedCommand), typeof(ICommand), typeof(ClassCardView));

    public ICommand ToggleExpandedCommand
    {
        get => (ICommand)GetValue(ToggleExpandedCommandProperty);
        set => SetValue(ToggleExpandedCommandProperty, value);
    }


    public static readonly BindableProperty SelectCommandProperty =
        BindableProperty.Create(nameof(SelectCommand), typeof(ICommand), typeof(ClassCardView));
    public ICommand SelectCommand
    {
        get => (ICommand)GetValue(SelectCommandProperty);
        set => SetValue(SelectCommandProperty, value);
    }

    public ClassCardView()
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

        if (BindingContext is ClassCardVm vm)
        {
            ExpandedContent.AbortAnimation("expand");
            ExpandedContent.IsVisible = vm.IsExpanded;
            ExpandedContent.HeightRequest = -1;
            ExpandedContent.Opacity = 1;
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ClassCardVm.IsSelected))
            Dispatcher.Dispatch(async () => await AnimateSelectionAsync());


        if (e.PropertyName == nameof(ClassCardVm.IsExpanded))
            Dispatcher.Dispatch(async () => await HandleExpandedChangedAsync());
    }

    private async Task AnimateSelectionAsync()
    {
        if (BindingContext is not ClassCardVm vm) return;

        if (vm.IsSelected)
        {
            await CardFrame.ScaleTo(1.02, 110, Easing.CubicOut);
            await CardFrame.ScaleTo(1.0, 110, Easing.CubicOut);
        }
    }

    private async Task HandleExpandedChangedAsync()
    {
        if (BindingContext is not ClassCardVm vm) return;
        _expandCts?.Cancel();
        _expandCts = new CancellationTokenSource();
        var token = _expandCts.Token;

        try
        {
            if (vm.IsExpanded)
            {
                await ScrollIntoViewAsync(vm, token);
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

    private async Task ScrollIntoViewAsync(ClassCardVm vm, CancellationToken token)
    {
        await Task.Delay(40, token);
        if (token.IsCancellationRequested) return;

        var cv = FindParentCollectionView();
        if (cv == null) return;

        cv.ScrollTo(vm, position: ScrollToPosition.Start, animate: true);
        await Task.Delay(120, token);
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

            var width = ExpandedContent.Width > 0 ? ExpandedContent.Width : CardFrame.Width;
            if (width <= 0)
                width = Width;

            var measured = ExpandedContent.Measure(width, double.PositiveInfinity).Height;
            if (measured <= 0)
            {
                ExpandedContent.Opacity = 1;
                ExpandedContent.HeightRequest = -1;
                return;
            }

            ExpandedContent.HeightRequest = 0;
            ExpandedContent.Opacity = 0;

            var tcs = new TaskCompletionSource();
            var animation = new Animation(
                v =>
                {
                    ExpandedContent.HeightRequest = v;
                    ExpandedContent.Opacity = Math.Min(1, v / measured);
                },
                0,
                measured,
                Easing.CubicOut);
            animation.Commit(this, "expand", length: 240, finished: (_, __) => tcs.TrySetResult());
            await tcs.Task;

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
        var collapseTcs = new TaskCompletionSource();
        var collapse = new Animation(
            v =>
            {
                ExpandedContent.HeightRequest = v;
                ExpandedContent.Opacity = startHeight <= 0 ? 0 : Math.Max(0, v / startHeight);
            },
            startHeight,
            0,
            Easing.CubicIn);
        collapse.Commit(this, "expand", length: 200, finished: (_, __) => collapseTcs.TrySetResult());
        await collapseTcs.Task;

        if (token.IsCancellationRequested) return;

        ExpandedContent.IsVisible = false;
        ExpandedContent.HeightRequest = -1;
        ExpandedContent.Opacity = 1;
    }

    private CollectionView? FindParentCollectionView()
    {
        Element? parent = this;
        while (parent != null && parent is not CollectionView)
            parent = parent.Parent;
        return parent as CollectionView;
    }


}
