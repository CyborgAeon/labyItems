using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace labyItems.Pages.Characters.ViewModels;

public sealed class ChoiceSetOptionCellVm : INotifyPropertyChanged
{
    private bool _isSelected;

    public ChoiceSetOptionCellVm(string label, int row, int column, int columnSpan, Action select)
    {
        Label = label;
        Row = row;
        Column = column;
        ColumnSpan = columnSpan;
        SelectCommand = new Command(select);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Label { get; }
    public int Row { get; }
    public int Column { get; }
    public int ColumnSpan { get; }
    public ICommand SelectCommand { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }
}
