using System.Windows.Input;
using labyItems.Models;
using labyItems.Pages.Configs;
using labyItems.Services;
using SpellCardPage = labyItems.Pages.SpellCard.SpellCard;

namespace labyItems.Pages.Calculator;

public partial class SpellConfigPage : ConfigPageBase<SpellConfig>
{
    private bool _spellLookupLoaded;
    private SpellSearchOption? _selectedSearchSpell;

    public SpellConfigPage()
    {
        InitializeComponent();
        AddSelectedSpellCommand = new Command<object?>(OnSpellResultSelected);
    }

    public ICommand AddSelectedSpellCommand { get; }

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
    }

    private async void OnSpellResultSelected(object? parameter)
    {
        var option = parameter as SpellSearchOption ?? SelectedSearchSpell;
        if (option == null)
            return;

        if (BindingContext is not SpellConfig cfg)
            return;

        var added = cfg.TryAddSpell(option.Spell);
        var entry = cfg.SelectedSpells.FirstOrDefault(spell =>
            string.Equals(spell.SpellName, option.Name, StringComparison.OrdinalIgnoreCase)
            && spell.Power == option.Power
            && spell.IsAdvanced == option.IsAdvanced);
        if (entry == null && added)
            entry = cfg.SelectedSpells.LastOrDefault();

        SelectedSearchSpell = null;

        if (entry != null)
            await Navigation.PushModalAsync(new SpellEntryConfigModalPage(entry));
    }

    private async void OnEditSpellClicked(object sender, EventArgs e)
    {
        if (sender is not BindableObject bindable || bindable.BindingContext is not SpellSelectionEntry entry)
            return;

        var modal = new SpellEntryConfigModalPage(entry);
        await Navigation.PushModalAsync(modal);
    }

    private async void OnSpellInfoClicked(object sender, EventArgs e)
    {
        if (sender is not BindableObject bindable || bindable.BindingContext is not SpellSelectionEntry entry)
            return;

        var spell = entry.Spell;
        if (string.IsNullOrWhiteSpace(spell.name))
            return;

        await Navigation.PushModalAsync(new NavigationPage(new SpellCardPage(spell)));
    }

    private void OnDeleteSpellClicked(object sender, EventArgs e)
    {
        if (sender is not BindableObject bindable || bindable.BindingContext is not SpellSelectionEntry entry)
            return;

        if (BindingContext is not SpellConfig cfg)
            return;

        cfg.RemoveSpell(entry);
    }
}
