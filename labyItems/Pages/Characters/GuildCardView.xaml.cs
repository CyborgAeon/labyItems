using System.ComponentModel;
using System.Windows.Input;

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

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (BindingContext is INotifyPropertyChanged npc)
            npc.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GuildCardVm.IsSelected))
            Dispatcher.Dispatch(async () => await AnimateSelectionAsync());

        if (e.PropertyName == nameof(GuildCardVm.IsExpanded))
            Dispatcher.Dispatch(async () => await ScrollIntoViewIfExpandedAsync());
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

    private CancellationTokenSource? _scrollCts;

    private async Task ScrollIntoViewIfExpandedAsync()
    {
        if (BindingContext is not GuildCardVm vm) return;
        if (!vm.IsExpanded) return;

        _scrollCts?.Cancel();
        _scrollCts = new CancellationTokenSource();
        var token = _scrollCts.Token;

        await Task.Delay(80, token);

        var cv = FindParentCollectionView();
        if (cv == null) return;

        cv.ScrollTo(vm, position: ScrollToPosition.Center, animate: false);
        await Task.Delay(150, token);
        cv.ScrollTo(vm, position: ScrollToPosition.Start, animate: true);
    }

    private CollectionView? FindParentCollectionView()
    {
        Element? parent = this;
        while (parent != null && parent is not CollectionView)
            parent = parent.Parent;
        return parent as CollectionView;
    }
}
