using System.ComponentModel;

namespace labyItems.Models.Search;

public sealed class GlobalSearchFilterChipVm : INotifyPropertyChanged
{
    private string _label;
    private bool _isSelected;
    private bool _isBack;
    private bool _isEnabled = true;

    public GlobalSearchFilterChipVm(string key, string label, bool isSelected, bool isBack)
    {
        Key = key ?? string.Empty;
        _label = label ?? string.Empty;
        _isSelected = isSelected;
        _isBack = isBack;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Key { get; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
                return;

            _isEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ChipOpacity)));
        }
    }

    public double ChipOpacity => _isEnabled ? 1d : 0.35d;

    public string Label
    {
        get => _label;
        private set
        {
            if (_label == value)
                return;

            _label = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        private set
        {
            if (_isSelected == value)
                return;

            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public bool IsBack
    {
        get => _isBack;
        private set
        {
            if (_isBack == value)
                return;

            _isBack = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBack)));
        }
    }

    public void UpdateFrom(GlobalSearchFilterChipVm source)
    {
        Label = source.Label;
        IsSelected = source.IsSelected;
        IsBack = source.IsBack;
        IsEnabled = source.IsEnabled;
    }
}
