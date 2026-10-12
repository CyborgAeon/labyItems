using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using labyItems.Models;
using labyItems.Services;
using labyItems.Helpers;

namespace labyItems.Pages.NonStandard;

public partial class CharacterAssignmentPage : ContentPage, INotifyPropertyChanged
{
    private readonly TaskCompletionSource<CharacterAssignmentOptionVm?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CharacterAssignmentRowVm? _selectedRow;
    private bool _completed;
    private bool _loaded;
    private readonly string? _selectedCharacterId;

    public new event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<CharacterAssignmentRowVm> Rows { get; } = new();
    public bool IsEmpty => Rows.Count == 0;
    public bool CanContinue => SelectedRow != null;
    public ICommand CancelCommand { get; }
    public ICommand NextCommand { get; }

    private CharacterAssignmentPage(string? selectedCharacterId)
    {
        _selectedCharacterId = selectedCharacterId;
        CancelCommand = new Command(async () => await CompleteAsync(null));
        NextCommand = new Command(async () => await CompleteAsync(SelectedRow?.Option), () => CanContinue);
        InitializeComponent();
        BindingContext = this;

    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded) return;
        _loaded = true;

        var characters = LiteDbService.GetCharacters()
            .OrderByDescending(character => character.UpdatedUtc)
            .ToArray();
        var classes = await ClassService.GetAllAsync();

        foreach (var character in characters)
        {
            var row = new CharacterAssignmentRowVm(character, ResolveClassBrackets(character, classes));
            Rows.Add(row);
            if (string.Equals(character.Id, _selectedCharacterId, StringComparison.OrdinalIgnoreCase))
                SelectedRow = row;
        }
        Raise(nameof(IsEmpty));
    }

    public CharacterAssignmentRowVm? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (ReferenceEquals(_selectedRow, value)) return;
            if (_selectedRow != null) _selectedRow.IsSelected = false;
            _selectedRow = value;
            if (_selectedRow != null) _selectedRow.IsSelected = true;
            Raise(); Raise(nameof(CanContinue));
            ((Command)NextCommand).ChangeCanExecute();
        }
    }

    public static async Task<CharacterAssignmentOptionVm?> PickAsync(INavigation navigation, string? selectedCharacterId)
    {
        var page = new CharacterAssignmentPage(selectedCharacterId);
        await navigation.PushAsync(page);
        return await page._completion.Task;
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CompleteAsync(null);
        return true;
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        => SelectedRow = e.CurrentSelection.FirstOrDefault() as CharacterAssignmentRowVm;

    private void OnRowTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject bindable && bindable.BindingContext is CharacterAssignmentRowVm row)
            SelectedRow = row;
    }

    private static IReadOnlyList<string> ResolveClassBrackets(
        Character character,
        IReadOnlyDictionary<string, CharacterClassRecord> classes)
    {
        var className = (character.Class ?? string.Empty).Trim();
        if (classes.TryGetValue(className, out var exact))
            return exact.Brackets;

        var wanted = LifeScalesService.NormalizeKey(className);
        var match = classes.FirstOrDefault(pair =>
                LifeScalesService.NormalizeKey(pair.Key).Equals(wanted, StringComparison.OrdinalIgnoreCase))
            .Value;
        return match?.Brackets ?? (IReadOnlyList<string>)Array.Empty<string>();
    }

    private async Task CompleteAsync(CharacterAssignmentOptionVm? result)
    {
        if (_completed) return;
        _completed = true;
        _completion.TrySetResult(result);
        await Navigation.PopAsync();
    }

    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}

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
    public Color SelectionBackground => IsSelected ? ResolveColor("SurfaceMutedBackgroundColor", "#F7F7F8") : Colors.Transparent;

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
