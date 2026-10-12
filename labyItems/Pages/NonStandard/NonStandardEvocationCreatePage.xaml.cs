using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using labyItems.Pages.Calculator;
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
    private ObservableCollection<string> _selectedFields = new();

    public new event PropertyChangedEventHandler? PropertyChanged;

    public NonStandardEvocationCreatePage()
    {
        BackCommand = new Command(async () => await Navigation.PopAsync());
        SearchCommand = new Command(async () => await SelectTemplateAsync(), () => !IsBusy);
        ChooseCharacterCommand = new Command(async () => await ChooseCharacterAsync(), () => !IsBusy);
        SaveCommand = new Command(async () => await SaveAsync(), () => CanSave);
        InitializeComponent();
        BindingContext = this;
        ReplaceSelectedFields(Draft.fields);
    }

    public DruidEvocationService.EvocRaw Draft
    {
        get => _draft;
        private set
        {
            _draft = value;
            ReplaceSelectedFields(value.fields);
            Raise(); Raise(nameof(PowerText)); Raise(nameof(PrerequisitesButtonText));
            Raise(nameof(ImmunityButtonText)); Raise(nameof(CanSave)); RefreshCommands();
        }
    }

    public string CharacterName => string.IsNullOrWhiteSpace(_character?.Name) ? "No character selected" : _character.Name;
    public string CharacterButtonText => _character == null ? "Choose character *" : $"Assigned to {_character.Name} · Change";
    public ObservableCollection<string> AvailableFields { get; } = new(Enum.GetValues<EvocationFields>().Select(FormatEnum));
    public ObservableCollection<string> SelectedFields
    {
        get => _selectedFields;
        private set { _selectedFields = value; Raise(); }
    }
    public string PowerText
    {
        get => Draft.power.ToString();
        set { if (int.TryParse(value, out var power)) Draft.power = Math.Max(0, power); }
    }
    public string PrerequisitesButtonText => Draft.preReqs.Count == 0 ? "Choose prerequisites" : $"Prerequisites · {Draft.preReqs.Count} selected";
    public string ImmunityButtonText => string.IsNullOrWhiteSpace(Draft.immunity) ? "Choose immunity" : $"Immunity · {Draft.immunity}";
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
        Raise(nameof(CharacterButtonText));
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
        var selected = await CharacterAssignmentPage.PickAsync(Navigation, _character?.Id);
        if (selected == null) return;
        _character = selected;
        Raise(nameof(CharacterName));
        Raise(nameof(CharacterButtonText));
        Raise(nameof(CanSave));
        RefreshCommands();
    }

    private void OnDraftTextChanged(object? sender, TextChangedEventArgs e)
    {
        Draft.name = e.NewTextValue ?? string.Empty;
        Raise(nameof(CanSave));
        RefreshCommands();
    }

    private void ReplaceSelectedFields(IEnumerable<string>? fields)
    {
        if (_selectedFields != null)
            _selectedFields.CollectionChanged -= OnSelectedFieldsChanged;
        SelectedFields = new ObservableCollection<string>(fields ?? []);
        foreach (var field in SelectedFields.Where(field => !AvailableFields.Contains(field, StringComparer.OrdinalIgnoreCase)))
            AvailableFields.Add(field);
        SelectedFields.CollectionChanged += OnSelectedFieldsChanged;
    }

    private void OnSelectedFieldsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => Draft.fields = SelectedFields.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private void OnAddCustomFieldClicked(object? sender, EventArgs e)
    {
        var value = CustomFieldEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(value)) return;
        if (!AvailableFields.Contains(value, StringComparer.OrdinalIgnoreCase)) AvailableFields.Add(value);
        if (!SelectedFields.Contains(value, StringComparer.OrdinalIgnoreCase)) SelectedFields.Add(value);
        CustomFieldEntry.Text = string.Empty;
    }

    private async void OnPrerequisitesClicked(object? sender, EventArgs e)
    {
        Draft.preReqs = (await MpCatalogSearch.PickGlobalPrerequisitesAsync(Navigation, Draft.preReqs)).ToList();
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
        var edited = await EvocationCombatEditorPage.EditAsync(Navigation, Clone(Draft), isDamage);
        if (edited != null) Draft = edited;
    }

    private static string FormatEnum(EvocationFields value)
    {
        var raw = value.ToString();
        return string.Concat(raw.Select((ch, index) => index > 0 && char.IsUpper(ch) ? $" {ch}" : ch.ToString()));
    }

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
            await Navigation.PopAsync();
        }
        catch (Exception ex) { await DisplayAlert("Save failed", ex.Message, "OK"); }
        finally { IsBusy = false; }
    }

    private static DruidEvocationService.EvocRaw NewDraft() => new() { nonStandard = true };
    private static DruidEvocationService.EvocRaw Clone(DruidEvocationService.EvocRaw source)
        => JsonSerializer.Deserialize<DruidEvocationService.EvocRaw>(JsonSerializer.Serialize(source, JsonOptions), JsonOptions) ?? NewDraft();
    private void RefreshCommands() { ((Command)SearchCommand).ChangeCanExecute(); ((Command)ChooseCharacterCommand).ChangeCanExecute(); ((Command)SaveCommand).ChangeCanExecute(); }
    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
