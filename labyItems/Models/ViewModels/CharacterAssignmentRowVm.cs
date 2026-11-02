using System.ComponentModel;
using System.Runtime.CompilerServices;
using labyItems.Helpers;
using labyItems.Models;
using labyItems.Pages.NonStandard;

namespace labyItems.Models.ViewModels;

public sealed class CharacterAssignmentRowVm : INotifyPropertyChanged
{
    private bool _isSelected;
    public event PropertyChangedEventHandler? PropertyChanged;
    public CharacterAssignmentOptionVm Option { get; }
    public string Name => Option.Name;
    public string Initial { get; }
    public string Subtitle { get; }
    public string PointsText { get; }
    public string LastPlayedText { get; }
    public IReadOnlyList<string> Brackets { get; }
    public bool HasSplitIcon => Brackets.Count >= 2;
    public string SingleGlyph => Brackets.Count == 0 ? "?" : ClassBracketIconHelper.GetBracketGlyph(Brackets[0]);
    public string SingleIconFontFamily => string.Empty;
    public Color SingleBackground => ClassBracketIconHelper.GetBracketColor(Brackets.Count == 0 ? "General" : Brackets[0]);
    public string SplitLeftGlyph => ClassBracketIconHelper.GetBracketGlyph(GetBracketAt(0, "General"));
    public string SplitRightGlyph => ClassBracketIconHelper.GetBracketGlyph(GetBracketAt(1, GetBracketAt(0, "General")));
    public Color SplitLeftBackground => ClassBracketIconHelper.GetBracketColor(GetBracketAt(0, "General"));
    public Color SplitRightBackground => ClassBracketIconHelper.GetBracketColor(GetBracketAt(1, GetBracketAt(0, "General")));
    public string AccessibilityDescription => $"{Name}, {Subtitle}, {PointsText}. Double tap to select.";
    public bool IsSelected { get => _isSelected; set { if (_isSelected == value) return; _isSelected = value; Raise(); Raise(nameof(SelectionStroke)); Raise(nameof(SelectionStrokeThickness)); Raise(nameof(SelectionBackground)); } }
    public Color SelectionStroke => IsSelected ? ResolveColor("AccentMaroonColor", "#7A2432") : ResolveColor("SurfaceBorderColor", "#E5E7EB");
    public double SelectionStrokeThickness => IsSelected ? 2 : 1;
    public Color SelectionBackground => IsSelected ? ResolveColor("SelectedSurfaceBackgroundColor", "#F5E8EB") : ResolveColor("White", "#FFFFFF");

    public CharacterAssignmentRowVm(Character character, IReadOnlyList<string> classBrackets)
    {
        Option = new(character.Id, character.Name, character.PlayerName, character.Class);
        Initial = string.IsNullOrWhiteSpace(character.Name) ? "?" : character.Name.Trim()[0].ToString().ToUpperInvariant();
        Subtitle = string.Join(" • ", new[] { character.Class, character.PlayerName }.Where(v => !string.IsNullOrWhiteSpace(v)));
        PointsText = $"{Math.Max(0, character.Points):N0} pts";
        var age = DateTime.UtcNow - character.UpdatedUtc.ToUniversalTime();
        LastPlayedText = age.TotalDays >= 1 ? $"{Math.Floor(age.TotalDays)}d ago" : age.TotalHours >= 1 ? $"{Math.Floor(age.TotalHours)}h ago" : "recently";
        Brackets = classBrackets.Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
    private string GetBracketAt(int index, string fallback) => index >= 0 && index < Brackets.Count ? Brackets[index] : fallback;
    private static Color ResolveColor(string key, string fallback) => Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color ? color : Color.FromArgb(fallback);
    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
