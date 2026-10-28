using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace labyItems.Pages.Characters.ViewModels;

public sealed class ReviewSectionVm : INotifyPropertyChanged
{
    private bool _isSelected;
    private Color _backgroundColor = Colors.White;
    private Color _borderColor = Color.FromArgb("#E5E7EB");
    private Color _textColor = Colors.Black;

    public ReviewSectionVm(string key, string title)
    {
        Key = key;
        Title = title;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Key { get; }
    public string Title { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    public Color BackgroundColor
    {
        get => _backgroundColor;
        set => Set(ref _backgroundColor, value);
    }

    public Color BorderColor
    {
        get => _borderColor;
        set => Set(ref _borderColor, value);
    }

    public Color TextColor
    {
        get => _textColor;
        set => Set(ref _textColor, value);
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
