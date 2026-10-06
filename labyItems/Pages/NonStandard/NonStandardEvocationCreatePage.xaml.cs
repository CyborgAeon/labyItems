using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using labyItems.Pages.Calculator;
using labyItems.Pages.EvocationCard;
using labyItems.Services;
using labyItems.Models.Enums;

namespace labyItems.Pages.NonStandard;

public partial class NonStandardEvocationCreatePage : ContentPage, INotifyPropertyChanged
{
    // EvocRaw deliberately supports both the current `immunity` property and the
    // legacy `Immunity` property. Case-insensitive metadata treats those as the
    // same JSON name and throws before serialization starts.
    private static readonly JsonSerializerOptions JsonOptions = new();
    private DruidEvocationService.EvocRaw _draft = NewDraft();
    private CharacterAssignmentOptionVm? _character;
    private bool _isBusy;

    public new event PropertyChangedEventHandler? PropertyChanged;

    public NonStandardEvocationCreatePage()
    {
        BackCommand = new Command(async () => await Navigation.PopAsync());
        SearchCommand = new Command(async () => await SelectTemplateAsync(), () => !IsBusy);
        ChooseCharacterCommand = new Command(async () => await ChooseCharacterAsync(), () => !IsBusy);
        SaveCommand = new Command(async () => await SaveAsync(), () => CanSave);
        InitializeComponent();
        BindingContext = this;
    }

    public DruidEvocationService.EvocRaw Draft
    {
        get => _draft;
        private set { _draft = value; Raise(); Raise(nameof(CanSave)); RefreshCommands(); }
    }

    public string CharacterName => string.IsNullOrWhiteSpace(_character?.Name) ? "No character selected" : _character.Name;
    public bool CanSave => !IsBusy && _character != null && !string.IsNullOrWhiteSpace(Draft.name);
    public ICommand BackCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand ChooseCharacterCommand { get; }
    public ICommand SaveCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set { if (_isBusy == value) return; _isBusy = value; Raise(); Raise(nameof(CanSave)); RefreshCommands(); }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_character != null) return;
        var selected = LiteDbService.GetCharacters()
            .OrderBy(character => character.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (selected != null)
            _character = new CharacterAssignmentOptionVm(selected.Id, selected.Name, selected.PlayerName, selected.Class);
        Raise(nameof(CharacterName));
        Raise(nameof(CanSave));
        RefreshCommands();
    }

    private async Task SelectTemplateAsync()
    {
        try
        {
            var selected = await MpCatalogSearch.PickEvocationTemplateAsync(Navigation, await DruidEvocationService.GetAllAsync());
            if (selected != null) Draft = Clone(selected);
        }
        catch (Exception ex) { await DisplayAlert("Evocation search failed", ex.Message, "OK"); }
    }

    private async Task ChooseCharacterAsync()
    {
        var characters = LiteDbService.GetCharacters()
            .OrderBy(character => character.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (characters.Count == 0)
        {
            await DisplayAlert("No characters", "Create a character before assigning a custom evocation.", "OK");
            return;
        }

        var labels = characters.Select(character => string.IsNullOrWhiteSpace(character.PlayerName)
            ? character.Name
            : $"{character.Name} — {character.PlayerName}").ToArray();
        var picked = await DisplayActionSheet("Assign to character", "Cancel", null, labels);
        var index = Array.IndexOf(labels, picked);
        if (index < 0) return;
        var selected = characters[index];
        _character = new CharacterAssignmentOptionVm(selected.Id, selected.Name, selected.PlayerName, selected.Class);
        Raise(nameof(CharacterName));
        Raise(nameof(CanSave));
        RefreshCommands();
    }

    private async void OnFieldEditRequested(object? sender, EvocationFieldEditRequestedEventArgs e)
    {
        var draft = Clone(Draft);
        switch (e.Field)
        {
            case "name": draft.name = await PromptAsync("Name", "Evocation name", draft.name); break;
            case "power":
                var power = await PromptAsync("Power", "Earthpower cost", draft.power.ToString(), Keyboard.Numeric);
                if (int.TryParse(power, out var parsed)) draft.power = Math.Max(0, parsed);
                break;
            case "range": draft.range = await PromptAsync("Range", "Range", draft.range); break;
            case "duration": draft.duration = await PromptAsync("Duration", "Duration", draft.duration); break;
            case "description": draft.description = await PromptAsync("Description", "Description", draft.description); break;
            case "verbal": draft.verbal = await PromptAsync("Verbal", "Verbal or gesture", draft.verbal); break;
            case "fields":
                draft.fields = await PickFieldsAsync(draft.fields); break;
            case "preReqs":
                var prereqs = await MpCatalogSearch.PickGlobalPrerequisitesAsync(Navigation, draft.preReqs);
                draft.preReqs = prereqs.ToList();
                break;
            case "immunity":
                var ability = await MpCatalogSearch.PickAbilityAsync(Navigation, await EvolutionService.GetAllAbilitiesAsync());
                if (ability != null)
                {
                    var display = string.IsNullOrWhiteSpace(ability.DisplayName) ? ability.Index : ability.DisplayName;
                    draft.immunity = display;
                    draft.immunityName = [display];
                    draft.immunityIndex = [ability.Index];
                }
                break;
            case "isAdvanced":
                draft.isAdvanced = !draft.isAdvanced;
                break;
            case "Damage":
            case "Heal":
                var edited = await EvocationCombatEditorPage.EditAsync(Navigation, draft, e.Field == "Damage");
                if (edited != null) draft = edited;
                break;
        }
        Draft = draft;
    }

    private async Task<List<string>> PickFieldsAsync(IEnumerable<string> current)
    {
        var selected = new HashSet<string>(current ?? [], StringComparer.OrdinalIgnoreCase);
        var standard = Enum.GetValues<EvocationFields>().Select(FormatEnum).ToList();
        while (true)
        {
            var choices = standard.Select(value => $"{(selected.Contains(value) ? "✓" : "○")} {value}")
                .Append("Add custom field…").Append("Done").ToArray();
            var picked = await DisplayActionSheet("Evocation fields", "Cancel", null, choices);
            if (picked is null or "Cancel" or "Done") break;
            if (picked == "Add custom field…")
            {
                var custom = (await DisplayPromptAsync("Custom field", "Field name", "Add", "Cancel"))?.Trim();
                if (!string.IsNullOrWhiteSpace(custom)) selected.Add(custom);
                continue;
            }
            var value = picked.Length > 2 ? picked[2..] : picked;
            if (!selected.Add(value)) selected.Remove(value);
        }
        return selected.ToList();
    }

    private static string FormatEnum(EvocationFields value)
    {
        var raw = value.ToString();
        return string.Concat(raw.Select((ch, index) => index > 0 && char.IsUpper(ch) ? $" {ch}" : ch.ToString()));
    }

    private async Task<string> PromptAsync(string title, string message, string initial, Keyboard? keyboard = null)
        => (await DisplayPromptAsync(title, message, "Apply", "Cancel", initialValue: initial ?? string.Empty,
            keyboard: keyboard ?? Keyboard.Default)) ?? initial ?? string.Empty;

    private async Task SaveAsync()
    {
        if (!CanSave || _character == null) return;
        IsBusy = true;
        try
        {
            Draft.nonStandard = true;
            await NonStandardContentService.SaveAsync(new NonStandardSaveRequest
            {
                EntityType = NonStandardEntityType.Evocation,
                Name = Draft.name.Trim(),
                DataJson = JsonSerializer.Serialize(Draft, JsonOptions),
                AssignedCharacterId = _character.Id,
                AssignedCharacterName = _character.Name,
                AssignedCharacterPlayerName = _character.PlayerName
            });
            await DisplayAlert("Evocation saved", $"'{Draft.name.Trim()}' was saved for {_character.Name}.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex) { await DisplayAlert("Save failed", ex.Message, "OK"); }
        finally { IsBusy = false; }
    }

    private static DruidEvocationService.EvocRaw NewDraft() => new() { nonStandard = true };
    private static DruidEvocationService.EvocRaw Clone(DruidEvocationService.EvocRaw source)
        => JsonSerializer.Deserialize<DruidEvocationService.EvocRaw>(JsonSerializer.Serialize(source, JsonOptions), JsonOptions) ?? NewDraft();
    private static List<string> SplitList(string value) => (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    private void RefreshCommands() { ((Command)SearchCommand).ChangeCanExecute(); ((Command)ChooseCharacterCommand).ChangeCanExecute(); ((Command)SaveCommand).ChangeCanExecute(); }
    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
