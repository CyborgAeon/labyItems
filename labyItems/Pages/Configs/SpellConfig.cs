using System;
using System.Collections.ObjectModel;
using labyItems.Controls;
using labyItems.Helpers;
using labyItems.Models.Enums;
using labyItems.Pages.Calculator;
using labyItems.Pages.Configs;

namespace labyItems.Pages.Configs;

public class SpellConfig : ConfigBase
{
    public SpellConfig()
    {
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Breakdown) || e.PropertyName == nameof(BreakdownItems))
                return;

            RecalculateBreakdown();
        };

        RecalculateBreakdown();
    }

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

    public ObservableCollection<ContributionRow> BreakdownItems { get; } = new();

    private string _breakdown = string.Empty;
    public string Breakdown
    {
        get => _breakdown;
        private set
        {
            if (_breakdown != value)
            {
                _breakdown = value;
                OnPropertyChanged();
            }
        }
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

    private void RecalculateBreakdown()
    {
        var builder = new BreakdownBuilder();
        BreakdownItems.Clear();

        int basicCost = 2 * Power * Math.Max(0, BasicPerDay);
        if (basicCost > 0)
            builder.Add($"Basic casts x{BasicPerDay} @2×Power {Power} = {basicCost}", basicCost);

        int advancedCost = 3 * Power * Math.Max(0, AdvancedPerDay);
        if (advancedCost > 0)
            builder.Add($"Advanced casts x{AdvancedPerDay} @3×Power {Power} = {advancedCost}", advancedCost);

        int innateCost = basicCost + advancedCost;
        if (InnateIsMantic && innateCost > 0)
        {
            int manticExtra = innateCost * 3;
            builder.Add($"Innates are mantic ×4 = {manticExtra}", manticExtra);
            innateCost += manticExtra;
        }

        if (AddBasic)
        {
            builder.Add("Add basic spell to base list = 15", 15, includeWhenZero: true);
        }
        else if (AddAdvanced)
        {
            builder.Add("Add advanced spell to base list = 18", 18, includeWhenZero: true);
        }
        else if (AddPrep)
        {
            int prepCost = innateCost / 2;
            builder.Add($"Add to base list with 30s prep (50%) = {prepCost}", prepCost, includeWhenZero: innateCost > 0);
        }

        if (AdditionalGenericMana > 0)
        {
            int genericCost = 4 * AdditionalGenericMana;
            builder.Add($"Additional generic mana store +{AdditionalGenericMana} @4 each = {genericCost}", genericCost);
        }

        if (AdditionalManaOfColour > 0 && AdditionalManaColour.HasValue)
        {
            int colourCost = 3 * AdditionalManaOfColour;
            builder.Add($"Additional {AdditionalManaColour} mana store +{AdditionalManaOfColour} @3 each = {colourCost}", colourCost);
        }

        if (PowerStoreRegenerates)
            builder.Add("Power store regenerates = 25", 25);

        if (TurnHandbookToSixthMantic > 0)
        {
            int cost = 40 * TurnHandbookToSixthMantic;
            builder.Add($"Turn handbook→6th mantic x{TurnHandbookToSixthMantic} = {cost}", cost);
        }

        if (TurnAnyBasicMantic > 0)
        {
            int cost = 50 * TurnAnyBasicMantic;
            builder.Add($"Turn basic spell mantic x{TurnAnyBasicMantic} = {cost}", cost);
        }

        if (TurnAnyPublishedToSixthMantic > 0)
        {
            int cost = 60 * TurnAnyPublishedToSixthMantic;
            builder.Add($"Turn published spell→6th mantic x{TurnAnyPublishedToSixthMantic} = {cost}", cost);
        }

        if (TurnAnyPublishedMantic > 0)
        {
            int cost = 80 * TurnAnyPublishedMantic;
            builder.Add($"Turn any published spell mantic x{TurnAnyPublishedMantic} = {cost}", cost);
        }

        if (IsTeachingScroll)
        {
            int teachingCost = 2 * Power;
            builder.Add($"Teaching scroll (2×Power) = {teachingCost}", teachingCost);
        }

        foreach (var row in builder.Rows)
            BreakdownItems.Add(row);

        var header = string.IsNullOrWhiteSpace(SpellName) ? "Spell" : SpellName;
        Breakdown = builder.BuildSummary(header, Total);
    }
}
