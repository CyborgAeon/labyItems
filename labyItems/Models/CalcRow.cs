using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace labyItems.Models;

public class CalcRow : INotifyPropertyChanged
{
    private int _count;

    public string Name { get; init; } = "";
    public int Cost { get; init; }
    public bool AllowMultiple { get; init; } = false;

    public int Count
    {
        get => _count;
        set
        {
            if (_count == value) return;
            _count = value;
            OnPropertyChanged();
        }
    }

    public string CostLabel => Cost.ToString();

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}