using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace labyItems.Pages.Characters;

public sealed class RaceCardVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            Raise();
        }
    }


    public int Id { get; init; }
    public string Name { get; init; } = "";
    public IReadOnlyList<string> PeopleTypes { get; init; } = Array.Empty<string>();
    public string PeopleType { get; init; } = "";
    public bool IsNonStandard { get; init; }
    public string Description { get; init; } = "";
    public string SearchText { get; set; } = "";

    public string Icon { get; init; } = "👤";
    public string PeopleTypeDisplay => IsNonStandard
        ? string.IsNullOrWhiteSpace(PeopleType) ? "Non-standard" : $"{PeopleType} • Non-standard"
        : PeopleType;

}
