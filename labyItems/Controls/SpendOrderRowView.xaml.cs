using labyItems.Models.ViewModels;

namespace labyItems.Controls;

public partial class SpendOrderRowView : ContentView
{
    private const string DragRowKey = "CharacterSpendRow";
    public static readonly BindableProperty RowProperty = BindableProperty.Create(
        nameof(Row), typeof(CharacterSpendRowVm), typeof(SpendOrderRowView));

    public SpendOrderRowView() => InitializeComponent();
    public CharacterSpendRowVm? Row
    {
        get => (CharacterSpendRowVm?)GetValue(RowProperty);
        set => SetValue(RowProperty, value);
    }
    public event EventHandler<CharacterSpendRowVm>? RowTapped;
    public event EventHandler<CharacterSpendRowVm>? RowLongPressed;
    public event EventHandler<CharacterSpendRowVm>? OptionsClicked;
    public event EventHandler<CharacterSpendRowVm>? RowDropped;

    private void OnRowTapped(object? sender, TappedEventArgs e)
    {
        if (Row != null)
            RowTapped?.Invoke(this, Row);
    }
    private void OnOptionsClicked(object? sender, EventArgs e)
    {
        if (Row != null)
            OptionsClicked?.Invoke(this, Row);
    }
    private void OnDragStarting(object? sender, DragStartingEventArgs e)
    {
        if (Row == null) { e.Cancel = true; return; }
        e.Data.Properties[DragRowKey] = Row;
        RowLongPressed?.Invoke(this, Row);
        Opacity = 0.65;
    }
    private void OnDropCompleted(object? sender, DropCompletedEventArgs e) => Opacity = 1;
    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.AcceptedOperation = e.Data.Properties.ContainsKey(DragRowKey)
            ? DataPackageOperation.Copy : DataPackageOperation.None;
    }
    private void OnDrop(object? sender, DropEventArgs e)
    {
        if (e.Data.Properties.TryGetValue(DragRowKey, out var value) && value is CharacterSpendRowVm source)
            RowDropped?.Invoke(this, source);
    }
}
