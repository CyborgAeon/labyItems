using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using labyItems.Pages.Calculator;
using labyItems.Pages.MiracleCard;
using labyItems.Services;

namespace labyItems.Pages.NonStandard;

public partial class NonStandardMiracleCreatePage : ContentPage, INotifyPropertyChanged
{
    private static readonly JsonSerializerOptions JsonOptions = new();
    private MiracleService.MiracRaw _draft = NewDraft();
    private CharacterAssignmentOptionVm? _character;
    private bool _isBusy;

    public new event PropertyChangedEventHandler? PropertyChanged;
    public NonStandardMiracleCreatePage()
    {
        BackCommand = new Command(async () => await Navigation.PopAsync());
        SearchCommand = new Command(async () => await SelectTemplateAsync(), () => !IsBusy);
        ChooseCharacterCommand = new Command(async () => await ChooseCharacterAsync(), () => !IsBusy);
        SaveCommand = new Command(async () => await SaveAsync(), () => CanSave);
        InitializeComponent(); BindingContext = this;
    }

    public MiracleService.MiracRaw Draft { get => _draft; private set { _draft = value; Raise(); Raise(nameof(CanSave)); RefreshCommands(); } }
    public string CharacterName => string.IsNullOrWhiteSpace(_character?.Name) ? "No character selected" : _character.Name;
    public bool CanSave => !IsBusy && _character != null && !string.IsNullOrWhiteSpace(Draft.name);
    public ICommand BackCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand ChooseCharacterCommand { get; }
    public ICommand SaveCommand { get; }
    public bool IsBusy { get => _isBusy; private set { if (_isBusy == value) return; _isBusy = value; Raise(); Raise(nameof(CanSave)); RefreshCommands(); } }

    protected override void OnAppearing()
    {
        base.OnAppearing(); if (_character != null) return;
        var selected = LiteDbService.GetCharacters().OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        if (selected != null) _character = new(selected.Id, selected.Name, selected.PlayerName, selected.Class);
        Raise(nameof(CharacterName)); Raise(nameof(CanSave)); RefreshCommands();
    }

    private async Task SelectTemplateAsync()
    {
        try { var selected = await MpCatalogSearch.PickMiracleTemplateAsync(Navigation, await MiracleService.GetAllAsync()); if (selected != null) Draft = Clone(selected); }
        catch (Exception ex) { await DisplayAlert("Miracle search failed", ex.Message, "OK"); }
    }

    private async Task ChooseCharacterAsync()
    {
        var characters = LiteDbService.GetCharacters().OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
        if (characters.Count == 0) { await DisplayAlert("No characters", "Create a character before assigning a custom miracle.", "OK"); return; }
        var labels = characters.Select(c => string.IsNullOrWhiteSpace(c.PlayerName) ? c.Name : $"{c.Name} — {c.PlayerName}").ToArray();
        var picked = await DisplayActionSheet("Assign to character", "Cancel", null, labels); var index = Array.IndexOf(labels, picked); if (index < 0) return;
        var selected = characters[index]; _character = new(selected.Id, selected.Name, selected.PlayerName, selected.Class);
        Raise(nameof(CharacterName)); Raise(nameof(CanSave)); RefreshCommands();
    }

    private async void OnFieldEditRequested(object? sender, MiracleFieldEditRequestedEventArgs e)
    {
        var draft = Clone(Draft);
        switch (e.Field)
        {
            case "name": draft.name = await PromptAsync("Name", "Miracle name", draft.name); break;
            case "sphere": draft.sphere = await PromptAsync("Sphere", "Sphere", draft.sphere); break;
            case "level": draft.level = await PromptAsync("Level", "Level", draft.level); break;
            case "power": var power = await PromptAsync("Power", "Spirit point cost", draft.power.ToString(), Keyboard.Numeric); if (int.TryParse(power, out var parsed)) draft.power = Math.Max(0, parsed); break;
            case "duration": draft.duration = await PromptAsync("Duration", "Duration", draft.duration); break;
            case "gesture": draft.gesture = await PromptAsync("Gesture", "Gesture", draft.gesture); break;
            case "alignment": draft.alignment = await PromptAsync("Alignment", "Alignment", draft.alignment); break;
            case "description": draft.description = await PromptAsync("Description", "Description", draft.description); break;
            case "verbal": draft.verbal = await PromptAsync("Verbal", "Verbal", draft.verbal); break;
            case "isAdvanced": draft.isAdvanced = !draft.isAdvanced; break;
            case "immunity":
                var ability = await MpCatalogSearch.PickAbilityAsync(Navigation, await EvolutionService.GetAllAbilitiesAsync());
                if (ability != null) { var display = string.IsNullOrWhiteSpace(ability.DisplayName) ? ability.Index : ability.DisplayName; draft.immunity = display; draft.immunityName = [display]; draft.immunityIndex = [ability.Index]; } break;
            case "preReqs": draft.preReqs = (await MpCatalogSearch.PickGlobalPrerequisitesAsync(Navigation, draft.preReqs)).ToList(); break;
            case "Damage": case "Heal": var edited = await MiracleCombatEditorPage.EditAsync(Navigation, draft, e.Field == "Damage"); if (edited != null) draft = edited; break;
        }
        Draft = draft;
    }

    private async Task<string> PromptAsync(string title, string message, string initial, Keyboard? keyboard = null) =>
        (await DisplayPromptAsync(title, message, "Apply", "Cancel", initialValue: initial ?? string.Empty, keyboard: keyboard ?? Keyboard.Default)) ?? initial ?? string.Empty;

    private async Task SaveAsync()
    {
        if (!CanSave || _character == null) return; IsBusy = true;
        try { Draft.nonStandard = true; await NonStandardContentService.SaveAsync(new NonStandardSaveRequest { EntityType = NonStandardEntityType.Miracle, Name = Draft.name.Trim(), DataJson = JsonSerializer.Serialize(Draft, JsonOptions), AssignedCharacterId = _character.Id, AssignedCharacterName = _character.Name, AssignedCharacterPlayerName = _character.PlayerName }); await DisplayAlert("Miracle saved", $"'{Draft.name.Trim()}' was saved for {_character.Name}.", "OK"); await Navigation.PopAsync(); }
        catch (Exception ex) { await DisplayAlert("Save failed", ex.Message, "OK"); } finally { IsBusy = false; }
    }

    private static MiracleService.MiracRaw NewDraft() => new() { nonStandard = true };
    private static MiracleService.MiracRaw Clone(MiracleService.MiracRaw source) => JsonSerializer.Deserialize<MiracleService.MiracRaw>(JsonSerializer.Serialize(source, JsonOptions), JsonOptions) ?? NewDraft();
    private void RefreshCommands() { ((Command)SearchCommand).ChangeCanExecute(); ((Command)ChooseCharacterCommand).ChangeCanExecute(); ((Command)SaveCommand).ChangeCanExecute(); }
    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
