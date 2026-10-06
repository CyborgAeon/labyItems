using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using labyItems.Pages.AbilityCard;
using labyItems.Pages.Calculator;
using labyItems.Services;

namespace labyItems.Pages.NonStandard;

public partial class NonStandardAbilityCreatePage : ContentPage, INotifyPropertyChanged
{
    private EvolutionService.AbilityResult _draft = NewDraft();
    private CharacterAssignmentOptionVm? _character;
    private bool _isBusy;

    public new event PropertyChangedEventHandler? PropertyChanged;

    public NonStandardAbilityCreatePage()
    {
        BackCommand = new Command(async () => await Navigation.PopAsync());
        SearchCommand = new Command(async () => await SelectTemplateAsync(), () => !IsBusy);
        ChooseCharacterCommand = new Command(async () => await ChooseCharacterAsync(), () => !IsBusy);
        SaveCommand = new Command(async () => await SaveAsync(), () => CanSave);
        InitializeComponent();
        BindingContext = this;
    }

    public EvolutionService.AbilityResult Draft
    {
        get => _draft;
        private set { _draft = value; Raise(); Raise(nameof(CanSave)); RefreshCommands(); }
    }
    public string CharacterName => string.IsNullOrWhiteSpace(_character?.Name) ? "No character selected" : _character.Name;
    public bool CanSave => !IsBusy && _character != null && !string.IsNullOrWhiteSpace(Draft.Index);
    public bool IsBusy { get => _isBusy; private set { if (_isBusy == value) return; _isBusy = value; Raise(); Raise(nameof(CanSave)); RefreshCommands(); } }
    public ICommand BackCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand ChooseCharacterCommand { get; }
    public ICommand SaveCommand { get; }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_character != null) return;
        var selected = LiteDbService.GetCharacters().OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        if (selected != null) _character = new(selected.Id, selected.Name, selected.PlayerName, selected.Class);
        Raise(nameof(CharacterName)); Raise(nameof(CanSave)); RefreshCommands();
    }

    private async Task SelectTemplateAsync()
    {
        try
        {
            var selected = await MpCatalogSearch.PickAbilityTemplateAsync(Navigation, await EvolutionService.GetAllAbilitiesAsync());
            if (selected != null) Draft = selected with { IsNonStandard = true };
        }
        catch (Exception ex) { await DisplayAlert("Ability search failed", ex.Message, "OK"); }
    }

    private async Task ChooseCharacterAsync()
    {
        var characters = LiteDbService.GetCharacters().OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
        if (characters.Count == 0) { await DisplayAlert("No characters", "Create a character before assigning a custom ability.", "OK"); return; }
        var labels = characters.Select(c => string.IsNullOrWhiteSpace(c.PlayerName) ? c.Name : $"{c.Name} — {c.PlayerName}").ToArray();
        var picked = await DisplayActionSheet("Assign to character", "Cancel", null, labels);
        var index = Array.IndexOf(labels, picked);
        if (index < 0) return;
        var selected = characters[index];
        _character = new(selected.Id, selected.Name, selected.PlayerName, selected.Class);
        Raise(nameof(CharacterName)); Raise(nameof(CanSave)); RefreshCommands();
    }

    private async void OnFieldEditRequested(object? sender, AbilityFieldEditRequestedEventArgs e)
    {
        var draft = Draft;
        switch (e.Field)
        {
            case "name":
                var name = await PromptAsync("Name", "Ability name", draft.Index);
                draft = draft with { Index = name, DisplayName = name };
                break;
            case "cost": draft = draft with { Cost = await PromptIntAsync("Cost", "Ability point cost", draft.Cost, 0) }; break;
            case "table": draft = draft with { Table = await PromptIntAsync("Table", "Ability table", draft.Table, 1) }; break;
            case "available": draft = draft with { Available = await PromptAsync("Availability", "Who can buy this ability?", draft.Available), AvailabilityRules = [] }; break;
            case "description": draft = draft with { Description = await PromptAsync("Description", "Ability description", draft.Description) }; break;
            case "preReqs": draft = draft with { PreReqs = await MpCatalogSearch.PickGlobalPrerequisitesAsync(Navigation, draft.PreReqs) }; break;
            case "purchaseOptions":
                var multiple = await DisplayAlert("Repeat purchases", "Can this ability be purchased multiple times?", "Yes", "No");
                int? maximum = null;
                if (multiple)
                {
                    var raw = await DisplayPromptAsync("Maximum purchases", "Leave blank for no maximum", "Apply", "Cancel", draft.MaxAvailable?.ToString() ?? "", keyboard: Keyboard.Numeric);
                    if (int.TryParse(raw, out var parsed) && parsed > 0) maximum = parsed;
                }
                draft = draft with { CanBuyMultiple = multiple, MaxAvailable = maximum };
                break;
        }
        Draft = draft;
    }

    private async Task<string> PromptAsync(string title, string message, string initial)
        => (await DisplayPromptAsync(title, message, "Apply", "Cancel", initialValue: initial ?? string.Empty)) ?? initial ?? string.Empty;

    private async Task<int> PromptIntAsync(string title, string message, int initial, int minimum)
    {
        var raw = await DisplayPromptAsync(title, message, "Apply", "Cancel", initialValue: initial.ToString(), keyboard: Keyboard.Numeric);
        return int.TryParse(raw, out var value) ? Math.Max(minimum, value) : initial;
    }

    private async Task SaveAsync()
    {
        if (!CanSave || _character == null) return;
        IsBusy = true;
        try
        {
            var payload = new
            {
                index = Draft.Index.Trim(), desc = Draft.Description, cost = Draft.Cost, table = Draft.Table,
                available = Draft.Available, canBuyMultiple = Draft.CanBuyMultiple, preReqs = Draft.PreReqs,
                maxAvailable = Draft.MaxAvailable, asPer = Draft.AsPer, nonStandard = true
            };
            await NonStandardContentService.SaveAsync(new NonStandardSaveRequest
            {
                EntityType = NonStandardEntityType.Ability, Name = Draft.Index.Trim(), DataJson = JsonSerializer.Serialize(payload),
                AssignedCharacterId = _character.Id, AssignedCharacterName = _character.Name,
                AssignedCharacterPlayerName = _character.PlayerName
            });
            await DisplayAlert("Ability saved", $"'{Draft.Index.Trim()}' was saved for {_character.Name}.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex) { await DisplayAlert("Save failed", ex.Message, "OK"); }
        finally { IsBusy = false; }
    }

    private static EvolutionService.AbilityResult NewDraft() => new() { IsNonStandard = true, Table = 1 };
    private void RefreshCommands() { ((Command)SearchCommand).ChangeCanExecute(); ((Command)ChooseCharacterCommand).ChangeCanExecute(); ((Command)SaveCommand).ChangeCanExecute(); }
    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
