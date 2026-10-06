using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using labyItems.Models.Enums;
using labyItems.Pages.Calculator;
using labyItems.Pages.SpellCard;
using labyItems.Services;

namespace labyItems.Pages.NonStandard;

public partial class NonStandardSpellCreatePage : ContentPage, INotifyPropertyChanged
{
    private static readonly JsonSerializerOptions JsonOptions = new();
    private SpellService.SpellRaw _draft = NewDraft();
    private CharacterAssignmentOptionVm? _character;
    private bool _isBusy;
    public new event PropertyChangedEventHandler? PropertyChanged;

    public NonStandardSpellCreatePage()
    {
        BackCommand = new Command(async () => await Navigation.PopAsync());
        SearchCommand = new Command(async () => await SelectTemplateAsync(), () => !IsBusy);
        ChooseCharacterCommand = new Command(async () => await ChooseCharacterAsync(), () => !IsBusy);
        SaveCommand = new Command(async () => await SaveAsync(), () => CanSave);
        InitializeComponent();
        BindingContext = this;
    }

    public SpellService.SpellRaw Draft { get => _draft; private set { _draft = value; Raise(); Raise(nameof(CanSave)); Raise(nameof(GestureDisplayText)); RefreshCommands(); } }
    public string CharacterName => string.IsNullOrWhiteSpace(_character?.Name) ? "No character selected" : _character.Name;
    public string GestureDisplayText => string.IsNullOrWhiteSpace(Draft.gesture) ? "Tap to set" : Draft.gesture;
    public bool CanSave => !IsBusy && _character != null && !string.IsNullOrWhiteSpace(Draft.name);
    public ICommand BackCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand ChooseCharacterCommand { get; }
    public ICommand SaveCommand { get; }
    public bool IsBusy { get => _isBusy; private set { if (_isBusy == value) return; _isBusy = value; Raise(); Raise(nameof(CanSave)); RefreshCommands(); } }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_character != null) return;
        var selected = LiteDbService.GetCharacters().OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        if (selected != null) _character = new CharacterAssignmentOptionVm(selected.Id, selected.Name, selected.PlayerName, selected.Class);
        Raise(nameof(CharacterName)); Raise(nameof(CanSave)); RefreshCommands();
    }

    private async Task SelectTemplateAsync()
    {
        try
        {
            var selected = await MpCatalogSearch.PickSpellTemplateAsync(Navigation, await SpellService.GetAllAsync());
            if (selected != null) Draft = Clone(selected);
        }
        catch (Exception ex) { await DisplayAlert("Spell search failed", ex.Message, "OK"); }
    }

    private async Task ChooseCharacterAsync()
    {
        var characters = LiteDbService.GetCharacters().OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
        if (characters.Count == 0) { await DisplayAlert("No characters", "Create a character before assigning a custom spell.", "OK"); return; }
        var labels = characters.Select(c => string.IsNullOrWhiteSpace(c.PlayerName) ? c.Name : $"{c.Name} — {c.PlayerName}").ToArray();
        var picked = await DisplayActionSheet("Assign to character", "Cancel", null, labels);
        var index = Array.IndexOf(labels, picked); if (index < 0) return;
        var selected = characters[index];
        _character = new CharacterAssignmentOptionVm(selected.Id, selected.Name, selected.PlayerName, selected.Class);
        Raise(nameof(CharacterName)); Raise(nameof(CanSave)); RefreshCommands();
    }

    private async void OnFieldEditRequested(object? sender, SpellFieldEditRequestedEventArgs e)
    {
        var draft = Clone(Draft);
        switch (e.Field)
        {
            case "name": draft.name = await PromptAsync("Name", "Spell name", draft.name); break;
            case "level": var raw = await PromptAsync("Level", "Spell level", draft.level.ToString(), Keyboard.Numeric); if (int.TryParse(raw, out var level)) draft.level = Math.Max(0, level); break;
            case "colour": draft.colour = await PickColourAsync(draft.colour); break;
            case "range": draft.range = await PromptAsync("Range", "Range", draft.range); break;
            case "duration": draft.duration = await PromptAsync("Duration", "Duration", draft.duration); break;
            case "description": draft.description = await PromptAsync("Description", "Description", draft.description); break;
            case "verbal": draft.verbal = await PromptAsync("Verbal", "Verbal", draft.verbal); break;
            case "notes": draft.notes = await PromptAsync("Notes", "Notes", draft.notes); break;
            case "isAdvanced": draft.isAdvanced = !(draft.isAdvanced ?? false); draft.IsAdvancedCompat = null; break;
            case "immunity":
                var ability = await MpCatalogSearch.PickAbilityAsync(Navigation, await EvolutionService.GetAllAbilitiesAsync());
                if (ability != null) { var display = string.IsNullOrWhiteSpace(ability.DisplayName) ? ability.Index : ability.DisplayName; draft.immunity = display; draft.immunityName = [display]; draft.immunityIndex = [ability.Index]; }
                break;
            case "Damage":
            case "Heal":
                var edited = await SpellCombatEditorPage.EditAsync(Navigation, draft, e.Field == "Damage");
                if (edited != null) draft = edited;
                break;
        }
        Draft = draft;
    }

    private async void OnGestureTapped(object? sender, TappedEventArgs e)
    {
        var draft = Clone(Draft); draft.gesture = await PromptAsync("Gesture", "Gesture", draft.gesture); Draft = draft;
    }

    private async Task EditDamageAsync(SpellService.SpellRaw draft)
    {
        draft.Damage ??= new SpellService.SpellDamageRaw();
        var current = draft.Damage.amount?.FirstOrDefault();
        var amount = await PromptAsync("Damage", "Amount, or a range such as 2,6. Leave blank to clear.", current == null ? string.Empty : string.Join(",", current));
        if (string.IsNullOrWhiteSpace(amount)) { draft.Damage.amount = []; return; }
        var numbers = amount.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(v => int.TryParse(v, out var n) ? Math.Max(0, n) : -1).Where(n => n >= 0).ToArray();
        if (numbers.Length == 0) return;
        draft.Damage.amount = [numbers];
        var effectType = await PromptAsync("Effect type", "Type is required when damage is supplied.", draft.Damage.type?.FirstOrDefault() ?? string.Empty);
        draft.Damage.type = string.IsNullOrWhiteSpace(effectType) ? [] : [effectType.Trim()];
        var categories = Enum.GetValues<DamageTypeEnum>().Where(v => v != DamageTypeEnum.Unknown).ToArray();
        var labels = categories.Select(v => v.ToDisplayText()).ToArray();
        var picked = await DisplayActionSheet("Damage type", "Cancel", null, labels);
        var categoryIndex = Array.IndexOf(labels, picked);
        if (categoryIndex >= 0) draft.Damage.DamageType = [categories[categoryIndex]];
        var armour = await PromptAsync("Armour applies", "Amount or range (optional)", draft.Damage.ArmourApplies?.FirstOrDefault() is { } a ? string.Join(",", a) : string.Empty);
        draft.Damage.ArmourApplies = ParseAmounts(armour);
        if (draft.Damage.ArmourApplies.Count > 0) draft.Damage.ArmourType = await PromptAsync("Armour type", "For example MAC or PAC", draft.Damage.ArmourType);
    }

    private async Task<string> PickColourAsync(string current)
    {
        var choices = new[] { "Black", "Blue", "Brown", "Green", "Grey", "Red", "White", "Yellow", "Sorcorial", "Custom…" };
        var picked = await DisplayActionSheet("Spell colour", "Cancel", null, choices);
        if (picked is null or "Cancel") return current;
        return picked == "Custom…" ? await PromptAsync("Spell colour", "Colour", current) : picked;
    }

    private async Task SaveAsync()
    {
        if (!CanSave || _character == null) return;
        if (Draft.Damage?.amount is { Count: > 0 } && (Draft.Damage.type is not { Count: > 0 } || Draft.Damage.DamageType is not { Count: > 0 })) { await DisplayAlert("Incomplete damage", "Damage type and effect type are required when damage amount is supplied.", "OK"); return; }
        if (Draft.Damage?.ArmourApplies is { Count: > 0 } && string.IsNullOrWhiteSpace(Draft.Damage.ArmourType)) { await DisplayAlert("Incomplete armour", "Armour type is required when armour applies is supplied.", "OK"); return; }
        IsBusy = true;
        try
        {
            Draft.nonStandard = true;
            await NonStandardContentService.SaveAsync(new NonStandardSaveRequest { EntityType = NonStandardEntityType.Spell, Name = Draft.name.Trim(), DataJson = JsonSerializer.Serialize(Draft, JsonOptions), AssignedCharacterId = _character.Id, AssignedCharacterName = _character.Name, AssignedCharacterPlayerName = _character.PlayerName });
            await DisplayAlert("Spell saved", $"'{Draft.name.Trim()}' was saved for {_character.Name}.", "OK"); await Navigation.PopAsync();
        }
        catch (Exception ex) { await DisplayAlert("Save failed", ex.Message, "OK"); }
        finally { IsBusy = false; }
    }

    private static List<int[]> ParseAmounts(string value) { var values = (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(v => int.TryParse(v, out var n) ? Math.Max(0, n) : -1).Where(n => n >= 0).ToArray(); return values.Length == 0 ? [] : [values]; }
    private async Task<string> PromptAsync(string title, string message, string initial, Keyboard? keyboard = null) => (await DisplayPromptAsync(title, message, "Apply", "Cancel", initialValue: initial ?? string.Empty, keyboard: keyboard ?? Keyboard.Default)) ?? initial ?? string.Empty;
    private static SpellService.SpellRaw NewDraft() => new() { nonStandard = true };
    private static SpellService.SpellRaw Clone(SpellService.SpellRaw source) => JsonSerializer.Deserialize<SpellService.SpellRaw>(JsonSerializer.Serialize(source, JsonOptions), JsonOptions) ?? NewDraft();
    private void RefreshCommands() { ((Command)SearchCommand).ChangeCanExecute(); ((Command)ChooseCharacterCommand).ChangeCanExecute(); ((Command)SaveCommand).ChangeCanExecute(); }
    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
