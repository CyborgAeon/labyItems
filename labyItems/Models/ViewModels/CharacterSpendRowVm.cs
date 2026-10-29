using labyItems.Models.Characters;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Graphics;

namespace labyItems.Models.ViewModels;

public sealed class CharacterSpendRowVm : INotifyPropertyChanged
{
    private bool _isSelected;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; init; } = string.Empty;
    public int Position { get; init; }
    public string PositionText => Position.ToString();
    public CharacterSpendEntryType Type { get; init; }
    public string TypeText => Type switch
    {
        CharacterSpendEntryType.MultiClass => "Multi-class",
        CharacterSpendEntryType.MultiRace => "Multi-race",
        _ => "Ability"
    };
    public string Name { get; init; } = string.Empty;
    public int Cost { get; init; }
    public int RunningTotal { get; init; }
    public int? CharacterLevel { get; init; }
    public int? PurchaseLevel { get; init; }
    public string CostText => CharacterLevel is > 0 ? $"lvl {CharacterLevel}" : $"{Cost:n0} Pts";
    public string TotalText => $"{RunningTotal:n0} Pts";
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            Raise();
            Raise(nameof(RowBackgroundColor));
            Raise(nameof(BorderColor));
            Raise(nameof(BorderThickness));
        }
    }

    public Color RowBackgroundColor => IsSelected
        ? Color.FromArgb("#F4E7C4")
        : Position % 2 == 0 ? Color.FromArgb("#FAF8F3") : Color.FromArgb("#FFFFFF");
    public Color BorderColor => IsSelected ? Color.FromArgb("#530000") : Color.FromArgb("#00000000");
    public double BorderThickness => IsSelected ? 1 : 0;
    public Color TypeBackgroundColor => Type switch
    {
        CharacterSpendEntryType.MultiClass => Color.FromArgb("#EEE9FA"),
        CharacterSpendEntryType.MultiRace => Color.FromArgb("#DDF4EA"),
        _ => Color.FromArgb("#E1EFFB")
    };

    private void Raise([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
