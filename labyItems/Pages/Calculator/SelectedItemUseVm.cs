using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using labyItems.Models.ViewModels;
using Microsoft.Maui.Graphics;

namespace labyItems.Pages.Calculator;

public sealed class SelectedItemUseVm<TOption> : INotifyPropertyChanged, IListItemRowModel
    where TOption : struct
{
    private readonly Action<SelectedItemUseVm<TOption>> _onUsesChanged;
    private readonly string _displayName;
    private int _uses;
    private Color _rowBackgroundColor = Colors.White;

    public SelectedItemUseVm(
        TOption option,
        string displayText,
        Action<SelectedItemUseVm<TOption>> onUsesChanged,
        int initialUses = 1)
    {
        Option = option;
        _displayName = displayText ?? string.Empty;
        _onUsesChanged = onUsesChanged ?? (_ => { });
        _uses = Math.Max(1, initialUses);

        IncrementCommand = new Command(() => Uses++);
        DecrementCommand = new Command(() => Uses = Math.Max(0, Uses - 1));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public TOption Option { get; }
    public string DisplayText => DisplayName;
    public string DisplayName => _displayName;

    public int Uses
    {
        get => _uses;
        set
        {
            var sanitized = Math.Max(0, value);
            if (_uses == sanitized)
                return;

            _uses = sanitized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(InlineSummary));
            OnPropertyChanged(nameof(RowText));
            _onUsesChanged(this);
        }
    }

    public string InlineSummary => Uses == 1 ? "1 use assigned." : $"{Uses} uses assigned.";
    public string RowText => $"{Uses}: {DisplayName}";

    public Color RowBackgroundColor
    {
        get => _rowBackgroundColor;
        set
        {
            if (_rowBackgroundColor == value)
                return;

            _rowBackgroundColor = value;
            OnPropertyChanged();
        }
    }

    public ICommand IncrementCommand { get; }
    public ICommand DecrementCommand { get; }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
