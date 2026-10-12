using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using labyItems.Pages.Calculator;
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

    public MiracleService.MiracRaw Draft { get => _draft; private set { _draft = value; Raise(); Raise(nameof(PowerText)); Raise(nameof(PrerequisitesButtonText)); Raise(nameof(ImmunityButtonText)); Raise(nameof(CanSave)); RefreshCommands(); } }
    public string CharacterName => string.IsNullOrWhiteSpace(_character?.Name) ? "No character selected" : _character.Name;
    public string CharacterButtonText => _character == null ? "Choose character *" : $"Assigned to {_character.Name} · Change";
    public string PowerText { get => Draft.power.ToString(); set { if (int.TryParse(value, out var power)) Draft.power = Math.Max(0, power); } }
    public string PrerequisitesButtonText => Draft.preReqs is not { Count: > 0 } ? "Choose prerequisites" : $"Prerequisites · {Draft.preReqs.Count} selected";
    public string ImmunityButtonText => string.IsNullOrWhiteSpace(Draft.immunity) ? "Choose immunity" : $"Immunity · {Draft.immunity}";
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
        Raise(nameof(CharacterName)); Raise(nameof(CharacterButtonText)); Raise(nameof(CanSave)); RefreshCommands();
    }

    private async Task SelectTemplateAsync()
    {
        try { var selected = await MpCatalogSearch.PickMiracleTemplateAsync(Navigation, await MiracleService.GetAllAsync()); if (selected != null) Draft = Clone(selected); }
        catch (Exception ex) { await DisplayAlert("Miracle search failed", ex.Message, "OK"); }
    }

    private async Task ChooseCharacterAsync()
    {
        var selected = await CharacterAssignmentPage.PickAsync(Navigation, _character?.Id);
        if (selected == null) return;
        _character = selected;
        Raise(nameof(CharacterName)); Raise(nameof(CharacterButtonText)); Raise(nameof(CanSave)); RefreshCommands();
    }

    private void OnDraftTextChanged(object? sender, TextChangedEventArgs e) { Draft.name = e.NewTextValue ?? string.Empty; Raise(nameof(CanSave)); RefreshCommands(); }

    private async void OnPrerequisitesClicked(object? sender, EventArgs e)
    {
        Draft.preReqs = (await MpCatalogSearch.PickGlobalPrerequisitesAsync(Navigation, Draft.preReqs ?? [])).ToList();
        Raise(nameof(PrerequisitesButtonText));
    }

    private async void OnImmunityClicked(object? sender, EventArgs e)
    {
        var ability = await MpCatalogSearch.PickAbilityAsync(Navigation, await EvolutionService.GetAllAbilitiesAsync());
        if (ability == null) return;
        var display = string.IsNullOrWhiteSpace(ability.DisplayName) ? ability.Index : ability.DisplayName;
        Draft.immunity = display; Draft.immunityName = [display]; Draft.immunityIndex = [ability.Index];
        Raise(nameof(ImmunityButtonText));
    }

    private async void OnDamageClicked(object? sender, EventArgs e) => await EditCombatAsync(true);
    private async void OnHealClicked(object? sender, EventArgs e) => await EditCombatAsync(false);
    private async Task EditCombatAsync(bool isDamage)
    {
        var edited = await MiracleCombatEditorPage.EditAsync(Navigation, Clone(Draft), isDamage);
        if (edited != null) Draft = edited;
    }

    private async Task SaveAsync()
    {
        if (!CanSave || _character == null) return; IsBusy = true;
        try { Draft.nonStandard = true; await NonStandardContentService.SaveAsync(new NonStandardSaveRequest { EntityType = NonStandardEntityType.Miracle, Name = Draft.name.Trim(), DataJson = JsonSerializer.Serialize(Draft, JsonOptions), AssignedCharacterId = _character.Id, AssignedCharacterName = _character.Name, AssignedCharacterPlayerName = _character.PlayerName }); await Navigation.PopAsync(); }
        catch (Exception ex) { await DisplayAlert("Save failed", ex.Message, "OK"); } finally { IsBusy = false; }
    }

    private static MiracleService.MiracRaw NewDraft() => new() { nonStandard = true };
    private static MiracleService.MiracRaw Clone(MiracleService.MiracRaw source) => JsonSerializer.Deserialize<MiracleService.MiracRaw>(JsonSerializer.Serialize(source, JsonOptions), JsonOptions) ?? NewDraft();
    private void RefreshCommands() { ((Command)SearchCommand).ChangeCanExecute(); ((Command)ChooseCharacterCommand).ChangeCanExecute(); ((Command)SaveCommand).ChangeCanExecute(); }
    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
