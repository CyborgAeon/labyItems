using System.ComponentModel;
using labyItems.Helpers;
using labyItems.Services;

namespace labyItems.Models.Search;

public sealed record GlobalSearchResultVm(
    GlobalSearchKind Kind,
    string Name,
    string GroupText,
    string IconGlyph,
    string MetaText,
    string DescriptionText,
    EvolutionService.AbilityResult? Ability,
    SpellService.SpellRaw? Spell,
    MiracleService.MiracRaw? Miracle,
    DruidEvocationService.EvocRaw? Evocation,
    NeuronicService.NeuronicRaw? Neuronic) : INotifyPropertyChanged
{
    private bool _isSelected;

    public bool UsesImageIcon => Kind == GlobalSearchKind.Neuronic;
    public bool UsesFontIcon => !UsesImageIcon;
    public string? IconImageSource => UsesImageIcon ? IconAssets.Neuronic : null;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;

            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectionGlyph)));
        }
    }

    public string SelectionGlyph => IsSelected ? FontAwesomeGlyphs.CheckboxChecked : FontAwesomeGlyphs.CheckboxUnchecked;

    public bool Equals(GlobalSearchResultVm? other) => ReferenceEquals(this, other);
    public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);

    public string DetailKey { get; init; } = string.Empty;
    public int? AbilityTable { get; init; }

    public bool CanOpenDetails
        => Ability != null
           || Spell != null
           || Miracle != null
           || Evocation != null
           || Neuronic != null
           || Kind is GlobalSearchKind.Ability
               or GlobalSearchKind.Spell
               or GlobalSearchKind.Miracle
               or GlobalSearchKind.Evocation
               or GlobalSearchKind.Neuronic
               or GlobalSearchKind.Guild
               or GlobalSearchKind.Class
               or GlobalSearchKind.Race
               or GlobalSearchKind.MultiClass
               or GlobalSearchKind.MultiRace;

    public bool HasDescription => !string.IsNullOrWhiteSpace(DescriptionText);

    public string IconBackgroundColor => Kind switch
    {
        GlobalSearchKind.Ability => "#E0F2FE",
        GlobalSearchKind.Spell => "#DBEAFE",
        GlobalSearchKind.Miracle => "#FEF3C7",
        GlobalSearchKind.Evocation => "#DCFCE7",
        GlobalSearchKind.Neuronic => "#E0E7FF",
        GlobalSearchKind.Guild => "#F3E8FF",
        GlobalSearchKind.Class => "#FEE2E2",
        GlobalSearchKind.Race => "#FFEDD5",
        GlobalSearchKind.MultiClass => "#FCE7F3",
        GlobalSearchKind.MultiRace => "#ECFCCB",
        _ => "#E5E7EB"
    };

    public string IconBorderColor => Kind switch
    {
        GlobalSearchKind.Ability => "#7DD3FC",
        GlobalSearchKind.Spell => "#93C5FD",
        GlobalSearchKind.Miracle => "#FCD34D",
        GlobalSearchKind.Evocation => "#86EFAC",
        GlobalSearchKind.Neuronic => "#A5B4FC",
        GlobalSearchKind.Guild => "#D8B4FE",
        GlobalSearchKind.Class => "#FCA5A5",
        GlobalSearchKind.Race => "#FDBA74",
        GlobalSearchKind.MultiClass => "#F472B6",
        GlobalSearchKind.MultiRace => "#A3E635",
        _ => "#D1D5DB"
    };

    public string IconColor => Kind switch
    {
        GlobalSearchKind.Ability => "#0C4A6E",
        GlobalSearchKind.Spell => "#1E3A8A",
        GlobalSearchKind.Miracle => "#92400E",
        GlobalSearchKind.Evocation => "#166534",
        GlobalSearchKind.Neuronic => "#3730A3",
        GlobalSearchKind.Guild => "#581C87",
        GlobalSearchKind.Class => "#B91C1C",
        GlobalSearchKind.Race => "#C2410C",
        GlobalSearchKind.MultiClass => "#BE185D",
        GlobalSearchKind.MultiRace => "#4D7C0F",
        _ => "#374151"
    };
}
