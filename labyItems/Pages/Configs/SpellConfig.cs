using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using labyItems.Models.Enums;
using labyItems.Pages.Calculator;
using labyItems.Pages.Configs;

namespace labyItems.Pages.Configs;

public class SpellConfig : ConfigBase
{
    protected override string NoneSelectedText => "Spell (none selected)";

    private string _spellName;
    public string SpellName
    {
        get => _spellName;
        set
        {
            if (
                SetProperty(
                    ref _spellName,
                    value,
                    affectsTotal: false,
                    nameof(ShowAddBasic),
                    nameof(ShowAddAdvanced)
                )
            )
                Name = value;
        }
    }

    private string _colour = "";
    public string Colour
    {
        get => _colour;
        set => SetProperty(ref _colour, value, affectsTotal: false);
    }

    private bool _innateIsMantic;
    public bool InnateIsMantic
    {
        get => _innateIsMantic;
        set => SetProperty(ref _innateIsMantic, value, affectsTotal: true);
    }

    private int _additionalGenericMana;
    public int AdditionalGenericMana
    {
        get => _additionalGenericMana;
        set =>
            SetProperty(
                ref _additionalGenericMana,
                Math.Clamp(value, 0, 12),
                affectsTotal: true,
                nameof(IsPowerStore)
            );
    }

    private int _additionalManaOfColour;
    public int AdditionalManaOfColour
    {
        get => _additionalManaOfColour;
        set =>
            SetProperty(
                ref _additionalManaOfColour,
                Math.Clamp(value, 0, 12),
                affectsTotal: true,
                nameof(IsPowerStore)
            );
    }

    private MagicColours? _additionalManaColour = null;
    public MagicColours? AdditionalManaColour
    {
        get => _additionalManaColour;
        set =>
            SetProperty(
                ref _additionalManaColour,
                value,
                affectsTotal: false,
                nameof(IsPowerStore)
            );
    }

    public bool IsPowerStore =>
        (
            AdditionalGenericMana > 0
            || (AdditionalManaOfColour > 0 && AdditionalManaColour.HasValue)
        );
    public bool ShowAddBasic => IsAdvanced == false;
    public bool ShowAddAdvanced => IsAdvanced == true;

    private bool _powerStoreRegenerates;
    public bool PowerStoreRegenerates
    {
        get => _powerStoreRegenerates;
        set => SetProperty(ref _powerStoreRegenerates, value, affectsTotal: true);
    }

    private int _turnHandbookToSixthMantic;
    public int TurnHandbookToSixthMantic
    {
        get => _turnHandbookToSixthMantic;
        set => SetProperty(ref _turnHandbookToSixthMantic, Math.Max(0, value), affectsTotal: true);
    }

    private int _turnAnyBasicMantic;
    public int TurnAnyBasicMantic
    {
        get => _turnAnyBasicMantic;
        set => SetProperty(ref _turnAnyBasicMantic, Math.Max(0, value), affectsTotal: true);
    }

    private int _turnAnyPublishedToSixthMantic;
    public int TurnAnyPublishedToSixthMantic
    {
        get => _turnAnyPublishedToSixthMantic;
        set =>
            SetProperty(ref _turnAnyPublishedToSixthMantic, Math.Max(0, value), affectsTotal: true);
    }

    private int _turnAnyPublishedMantic;
    public int TurnAnyPublishedMantic
    {
        get => _turnAnyPublishedMantic;
        set => SetProperty(ref _turnAnyPublishedMantic, Math.Max(0, value), affectsTotal: true);
    }

    private bool _isTeachingScroll;
    public bool IsTeachingScroll
    {
        get => _isTeachingScroll;
        set => SetProperty(ref _isTeachingScroll, value, affectsTotal: true);
    }

    public int PublishedPerDay
    {
        get => AdvancedPerDay;
        set => AdvancedPerDay = value;
    }

    public bool AddBasicToBaseList
    {
        get => AddBasic;
        set => AddBasic = value;
    }

    public bool AddAdvancedToBaseList
    {
        get => AddAdvanced;
        set => AddAdvanced = value;
    }

    protected override int BaseTotal()
    {
        int innates =
            (2 * Power * Math.Max(0, BasicPerDay)) + (3 * Power * Math.Max(0, AdvancedPerDay));
        if (InnateIsMantic)
            innates *= 4;

        int t = innates;
        if (AddBasic)
            t += 15;
        if (AddAdvanced)
            t += 18;
        if (AddPrep)
            t += (int)Math.Round(Power / 2.0, MidpointRounding.AwayFromZero);
        return t;
    }

    protected override int ExtraTotal()
    {
        int t = 0;

        if (AdditionalGenericMana > 0)
            t += 4 * AdditionalGenericMana;
        if (AdditionalManaOfColour > 0 && AdditionalManaColour.HasValue)
            t += 3 * AdditionalManaOfColour;

        if (PowerStoreRegenerates)
            t += 25;

        if (TurnHandbookToSixthMantic > 0)
            t += 40 * TurnHandbookToSixthMantic;
        if (TurnAnyBasicMantic > 0)
            t += 50 * TurnAnyBasicMantic;
        if (TurnAnyPublishedToSixthMantic > 0)
            t += 60 * TurnAnyPublishedToSixthMantic;
        if (TurnAnyPublishedMantic > 0)
            t += 80 * TurnAnyPublishedMantic;

        if (IsTeachingScroll)
            t += 2 * Power;

        return t;
    }

    private bool? ShowIsAdvanced()
    {
        if (string.IsNullOrEmpty(SpellName))
        {
            return null;
        }
        return IsAdvanced;
    }

    public bool SpellPicked { get; set; } = false;
    public bool? IsAdvancedToggled => ShowIsAdvanced();

    public void ApplySpell(Spell.Result picked)
    {
        SpellName = string.IsNullOrWhiteSpace(picked.Name)
            ? "Configure Spell"
            : $"{picked.Name} ({picked.Power} Mana)";
        SpellPicked = true;
        Colour = picked.Colour;
        IsAdvanced = picked.IsAdvanced;
        Power = Math.Max(1, picked.Power);
        OnPropertyChanged(nameof(ShowAddBasic));
        OnPropertyChanged(nameof(ShowAddAdvanced));
        OnPropertyChanged(nameof(SpellPicked));
    }
}
