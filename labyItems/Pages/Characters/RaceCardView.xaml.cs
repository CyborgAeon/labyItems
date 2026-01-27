using System.ComponentModel;
using System.Windows.Input;

namespace labyItems.Pages.Characters;

public partial class RaceCardView : ContentView
{
    public static readonly BindableProperty ToggleRaceExpandedCommandProperty =
        BindableProperty.Create(nameof(ToggleRaceExpandedCommand), typeof(ICommand), typeof(RaceCardView));

    public ICommand ToggleRaceExpandedCommand
    {
        get => (ICommand)GetValue(ToggleRaceExpandedCommandProperty);
        set => SetValue(ToggleRaceExpandedCommandProperty, value);
    }


    public static readonly BindableProperty SelectCommandProperty =
        BindableProperty.Create(nameof(SelectCommand), typeof(ICommand), typeof(RaceCardView));

    public ICommand SelectCommand
    {
        get => (ICommand)GetValue(SelectCommandProperty);
        set => SetValue(SelectCommandProperty, value);
    }

    public RaceCardView()
    {
        InitializeComponent();
    }

    private INotifyPropertyChanged? _boundVm;
    private CancellationTokenSource? _scrollCts;

    protected override void OnBindingContextChanged()
    {
        if (_boundVm != null)
            _boundVm.PropertyChanged -= OnVmPropertyChanged;

        base.OnBindingContextChanged();

        _scrollCts?.Cancel();
        _boundVm = BindingContext as INotifyPropertyChanged;
        if (_boundVm != null)
            _boundVm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RaceCardVm.IsSelected))
            Dispatcher.Dispatch(async () => await AnimateSelectionAsync());


        if (e.PropertyName == nameof(RaceCardVm.IsExpanded))
            Dispatcher.Dispatch(async () => await ScrollIntoViewIfExpandedAsync());
    }

    private async Task ScrollIntoViewIfExpandedAsync()
    {
        if (BindingContext is not RaceCardVm vm) return;
        if (!vm.IsExpanded) return;

        _scrollCts?.Cancel();
        _scrollCts = new CancellationTokenSource();
        var token = _scrollCts.Token;

        try
        {
            await Task.Delay(50, token);
            if (token.IsCancellationRequested) return;

            var cv = FindParentCollectionView();
            if (cv == null) return;

            cv.ScrollTo(vm, position: ScrollToPosition.Center, animate: true);
            await Task.Delay(140, token);
            if (token.IsCancellationRequested) return;

            cv.ScrollTo(vm, position: ScrollToPosition.Start, animate: true);
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

    private async Task AnimateSelectionAsync()
    {
        if (BindingContext is not RaceCardVm vm) return;

        if (vm.IsSelected)
        {
            await CardFrame.ScaleTo(1.02, 110, Easing.CubicOut);
            await CardFrame.ScaleTo(1.0, 110, Easing.CubicOut);
        }
    }

    private CollectionView? FindParentCollectionView()
    {
        Element? parent = this;
        while (parent != null && parent is not CollectionView)
            parent = parent.Parent;
        return parent as CollectionView;
    }
}
