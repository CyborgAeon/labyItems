using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using labyItems.Models;
using labyItems.Models.ViewModels;
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
