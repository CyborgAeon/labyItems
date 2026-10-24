using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace labyItems.Models.Search;

public sealed class GlobalSearchFilterSectionVm : INotifyPropertyChanged
{
    private bool _isExpanded = true;

    public GlobalSearchFilterSectionVm(GlobalSearchKind kind, string title)
    {
        Kind = kind;
        Title = title;
        ToggleExpandedCommand = new Command(() => IsExpanded = !IsExpanded);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public GlobalSearchKind Kind { get; }
    public string Title { get; }
    public ObservableCollection<GlobalSearchFilterChipVm> Chips { get; } = new();
    public ICommand ToggleExpandedCommand { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        private set
        {
            if (_isExpanded == value)
                return;

            _isExpanded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
        }
    }
}
