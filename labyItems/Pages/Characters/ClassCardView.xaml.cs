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
        if (e.PropertyName == nameof(ClassCardVm.IsSelected))
            Dispatcher.Dispatch(async () => await AnimateSelectionAsync());


        if (e.PropertyName == nameof(ClassCardVm.IsExpanded))
            Dispatcher.Dispatch(async () => await ScrollIntoViewIfExpandedAsync());
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
    private async Task ScrollIntoViewIfExpandedAsync()
    {
        if (BindingContext is not ClassCardVm vm) return;
        if (!vm.IsExpanded) return;

        _scrollCts?.Cancel();
        _scrollCts = new CancellationTokenSource();
        var token = _scrollCts.Token;

        try
        {
            // Let the expansion layout complete
            await Task.Delay(80, token);
            if (token.IsCancellationRequested) return;

            var cv = FindParentCollectionView();
            if (cv == null) return;

            cv.ScrollTo(vm, position: ScrollToPosition.Center, animate: false);
            await Task.Delay(150, token);
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

    private CollectionView? FindParentCollectionView()
    {
        Element? parent = this;
        while (parent != null && parent is not CollectionView)
            parent = parent.Parent;
        return parent as CollectionView;
    }


}
