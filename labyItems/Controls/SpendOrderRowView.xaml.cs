using labyItems.Models.ViewModels;

namespace labyItems.Controls;

public partial class SpendOrderRowView : ContentView
{
    public static readonly BindableProperty RowProperty = BindableProperty.Create(
        nameof(Row), typeof(CharacterSpendRowVm), typeof(SpendOrderRowView));

    private CancellationTokenSource? _holdCts;
    private bool _longPressRaised;

    public SpendOrderRowView() => InitializeComponent();

    public CharacterSpendRowVm? Row
    {
        get => (CharacterSpendRowVm?)GetValue(RowProperty);
        set => SetValue(RowProperty, value);
    }

    public event EventHandler<CharacterSpendRowVm>? RowTapped;
    public event EventHandler<CharacterSpendRowVm>? RowLongPressed;
    public event EventHandler<CharacterSpendRowVm>? OptionsClicked;
    public event EventHandler<int>? MoveRequested;

    private void OnSelectionPressed(object? sender, EventArgs e)
    {
        if (Row == null)
            return;

        _longPressRaised = false;
        _holdCts?.Cancel();
        _holdCts?.Dispose();
        _holdCts = new CancellationTokenSource();
        var token = _holdCts.Token;
        _ = DetectLongPressAsync(token);
    }

    private async Task DetectLongPressAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(500, token);
            if (token.IsCancellationRequested || Row == null)
                return;
            _longPressRaised = true;
            RowLongPressed?.Invoke(this, Row);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void OnSelectionReleased(object? sender, EventArgs e)
    {
        _holdCts?.Cancel();
        if (!_longPressRaised && Row != null)
            RowTapped?.Invoke(this, Row);
    }

    private void OnOptionsClicked(object? sender, EventArgs e)
    {
        if (Row != null)
            OptionsClicked?.Invoke(this, Row);
    }

    private void OnHandlePanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (sender is not VisualElement handle || Row == null)
            return;

        if (e.StatusType == GestureStatus.Running)
        {
            handle.TranslationY = Math.Clamp(e.TotalY, -36, 36);
            return;
        }

        if (e.StatusType is not (GestureStatus.Completed or GestureStatus.Canceled))
            return;

        var direction = e.TotalY switch
        {
            < -12 => -1,
            > 12 => 1,
            _ => 0
        };
        handle.TranslationY = 0;
        if (direction != 0)
            MoveRequested?.Invoke(this, direction);
    }
}
