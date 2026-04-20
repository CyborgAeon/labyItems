using System.Windows.Input;
using labyItems.Models;
using labyItems.Pages.Configs;
using labyItems.Services;
using SpellCardPage = labyItems.Pages.SpellCard.SpellCard;

namespace labyItems.Pages.Calculator;

public partial class SpellConfigPage : ConfigPageBase<SpellConfig>
{
    private bool _spellLookupLoaded;
    private bool _isLoading;
    private SpellSearchOption? _selectedSearchSpell;

    public SpellConfigPage()
    {
        AddSelectedSpellCommand = new Command<object?>(OnSpellResultSelected);
        EditSpellCommand = new Command<object?>(OnEditSpellRequested);
        ViewSpellInfoCommand = new Command<object?>(OnSpellInfoRequested);
        DeleteSpellCommand = new Command<object?>(OnDeleteSpellRequested);
        InitializeComponent();
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading == value) return;
            _isLoading = value;
            OnPropertyChanged();
        }
    }

    public ICommand AddSelectedSpellCommand { get; }
    public ICommand EditSpellCommand { get; }
    public ICommand ViewSpellInfoCommand { get; }
    public ICommand DeleteSpellCommand { get; }

    public Dictionary<string, SpellSearchOption> SpellLookup { get; private set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public SpellSearchOption? SelectedSearchSpell
    {
        get => _selectedSearchSpell;
        set
        {
            if (ReferenceEquals(_selectedSearchSpell, value))
                return;

            _selectedSearchSpell = value;
            OnPropertyChanged(nameof(SelectedSearchSpell));
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_spellLookupLoaded)
            return;

        _spellLookupLoaded = true;
        await LoadSpellLookupAsync();
    }

    protected override CalcResult BuildResult(SpellConfig cfg)
    {
        var details = new Dictionary<string, object?>();
        var selectedSpells = cfg.SelectedSpells
            .Select(spell => new
            {
                spellName = spell.SpellName,
                power = spell.Power,
                colour = spell.Colour,
                isAdvanced = spell.IsAdvanced,
                basicPerDay = spell.BasicPerDay,
                advancedPerDay = spell.AdvancedPerDay,
                innateIsMantic = spell.InnateIsMantic,
                isTeachingScroll = spell.IsTeachingScroll,
                addBasicToBaseList = spell.AddBasicToBaseList,
                addAdvancedToBaseList = spell.AddAdvancedToBaseList
            })
            .ToList();

        if (selectedSpells.Count > 0)
            details["spells"] = selectedSpells;

        if (cfg.AdditionalGenericMana > 0)
            details["additionalMana"] = cfg.AdditionalGenericMana;
        if (cfg.AdditionalManaOfColour > 0)
            details["additionalManaColour"] = new
            {
                colour = cfg.AdditionalManaColour?.ToString() ?? string.Empty,
                amount = cfg.AdditionalManaOfColour
            };
        if (cfg.PowerStoreRegenerates)
            details["powerStoreRegenerates"] = true;
        if (cfg.TurnHandbookToSixthMantic > 0)
            details["handbookToSixthManticPerDay"] = cfg.TurnHandbookToSixthMantic;
        if (cfg.TurnAnyBasicMantic > 0)
            details["anyBasicManticPerDay"] = cfg.TurnAnyBasicMantic;
        if (cfg.TurnAnyPublishedToSixthMantic > 0)
            details["anyPublishedToSixthManticPerDay"] = cfg.TurnAnyPublishedToSixthMantic;
        if (cfg.TurnAnyPublishedMantic > 0)
            details["anyPublishedManticPerDay"] = cfg.TurnAnyPublishedMantic;

        var abilityName = cfg.SelectedSpells.Count switch
        {
            0 => "Spell",
            1 => cfg.SelectedSpells[0].SpellName,
            _ => $"Spell list ({cfg.SelectedSpells.Count})"
        };

        return new CalcResult
        {
            AbilityType = "Spell",
            AbilityName = abilityName,
            TotalIsp = cfg.Total,
            Summary = cfg.Breakdown,
            Details = details
        };
    }

    private async Task LoadSpellLookupAsync()
    {
        IsLoading = true;
        try
        {
            var spells = await SpellService.GetAllAsync();
            SpellLookup = spells
                .Where(spell => !string.IsNullOrWhiteSpace(spell.name))
                .OrderBy(spell => spell.level)
                .ThenBy(spell => spell.name, StringComparer.OrdinalIgnoreCase)
                .Select(spell =>
                {
                    var option = new SpellSearchOption(spell);
                    var advancedToken = option.IsAdvanced ? "advanced" : "handbook";
                    var display = $"{option.Name} (lvl {option.Power} · {advancedToken} · {option.Colour})";
                    return new KeyValuePair<string, SpellSearchOption>(display, option);
                })
                .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First().Value, StringComparer.OrdinalIgnoreCase);

            OnPropertyChanged(nameof(SpellLookup));
            SpellSearch.ItemsSource = SpellLookup;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Spell load failed", ex.Message, "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OnSpellResultSelected(object? parameter)
    {
        var option = parameter as SpellSearchOption ?? SelectedSearchSpell;
        if (option == null)
            return;

        if (BindingContext is not SpellConfig cfg)
            return;

        cfg.TryAddSpell(option.Spell);

        SelectedSearchSpell = null;
    }

    private async void OnEditSpellRequested(object? parameter)
    {
        if (parameter is not SpellSelectionEntry entry)
            return;

        var modal = new SpellEntryConfigModalPage(entry);
        await Navigation.PushModalAsync(modal);
    }

    private async void OnSpellInfoRequested(object? parameter)
    {
        if (parameter is not SpellSelectionEntry entry)
            return;

        var spell = entry.Spell;
        if (string.IsNullOrWhiteSpace(spell.name))
            return;

        await Navigation.PushModalAsync(new NavigationPage(new SpellCardPage(spell)));
    }

    private void OnDeleteSpellRequested(object? parameter)
    {
        if (parameter is not SpellSelectionEntry entry)
            return;

        if (BindingContext is not SpellConfig cfg)
            return;

        cfg.RemoveSpell(entry);
    }
}
