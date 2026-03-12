using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using labyItems.Controls;
using labyItems.Helpers;
using labyItems.Models.Enums;
using labyItems.Services;
using Microsoft.Maui.Graphics;

namespace labyItems.Pages.Configs;

public sealed class SpellSelectionEntry : INotifyPropertyChanged, IConfigSelectionListItem
{
    private int _basicPerDay;
    private int _advancedPerDay;
    private bool _innateIsMantic;
    private bool _isTeachingScroll;
    private bool _addBasicToBaseList;
    private bool _addAdvancedToBaseList;
    private Color _rowBackgroundColor = Colors.White;

    public SpellSelectionEntry(SpellService.SpellRaw spell)
    {
        Spell = spell ?? new SpellService.SpellRaw();
        SpellName = Spell.name ?? string.Empty;
        Power = Math.Max(1, Spell.level);
        Colour = Spell.colour ?? string.Empty;
        IsAdvanced = Spell.isAdvanced ?? false;

        if (IsAdvanced)
            _advancedPerDay = 1;
        else
            _basicPerDay = 1;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public SpellService.SpellRaw Spell { get; }
    public string SpellName { get; }
    public int Power { get; }
    public string Colour { get; }
    public bool IsAdvanced { get; }

    public string DisplayName =>
        IsAdvanced
            ? $"{SpellName} (lvl {Power}, advanced)"
            : $"{SpellName} (lvl {Power})";

    public string ColourSummary =>
        string.IsNullOrWhiteSpace(Colour)
            ? string.Empty
            : $"Colour: {Colour}";

    public bool HasColourSummary => !string.IsNullOrWhiteSpace(ColourSummary);

    public int BasicPerDay
    {
        get => _basicPerDay;
        set
        {
            var next = Math.Max(0, value);
            if (_basicPerDay == next)
                return;

            _basicPerDay = next;
            Raise(nameof(BasicPerDay));
            Raise(nameof(InlineSummary));
        }
    }

    public int AdvancedPerDay
    {
        get => _advancedPerDay;
        set
        {
            var next = Math.Max(0, value);
            if (_advancedPerDay == next)
                return;

            _advancedPerDay = next;
            Raise(nameof(AdvancedPerDay));
            Raise(nameof(InlineSummary));
        }
    }

    public bool InnateIsMantic
    {
        get => _innateIsMantic;
        set
        {
            if (_innateIsMantic == value)
                return;

            _innateIsMantic = value;
            Raise(nameof(InnateIsMantic));
            Raise(nameof(InlineSummary));
        }
    }

    public bool IsTeachingScroll
    {
        get => _isTeachingScroll;
        set
        {
            if (_isTeachingScroll == value)
                return;

            _isTeachingScroll = value;
            Raise(nameof(IsTeachingScroll));
            Raise(nameof(InlineSummary));
        }
    }

    public bool AddBasicToBaseList
    {
        get => _addBasicToBaseList;
        set
        {
            if (_addBasicToBaseList == value)
                return;

            _addBasicToBaseList = value;
            if (value)
                _addAdvancedToBaseList = false;

            Raise(nameof(AddBasicToBaseList));
            Raise(nameof(AddAdvancedToBaseList));
            Raise(nameof(InlineSummary));
        }
    }

    public bool AddAdvancedToBaseList
    {
        get => _addAdvancedToBaseList;
        set
        {
            if (_addAdvancedToBaseList == value)
                return;

            _addAdvancedToBaseList = value;
            if (value)
                _addBasicToBaseList = false;

            Raise(nameof(AddAdvancedToBaseList));
            Raise(nameof(AddBasicToBaseList));
            Raise(nameof(InlineSummary));
        }
    }

    public bool ShowBasicConfig => !IsAdvanced;
    public bool ShowAdvancedConfig => IsAdvanced;
    public bool ShowAddBasic => !IsAdvanced;
    public bool ShowAddAdvanced => IsAdvanced;

    public Color RowBackgroundColor
    {
        get => _rowBackgroundColor;
        set
        {
            if (_rowBackgroundColor == value)
                return;

            _rowBackgroundColor = value;
            Raise(nameof(RowBackgroundColor));
        }
    }

    public string InlineSummary
    {
        get
        {
            var parts = new List<string>();
            if (ShowBasicConfig && BasicPerDay > 0)
                parts.Add($"basic x{BasicPerDay}/day");
            if (ShowAdvancedConfig && AdvancedPerDay > 0)
                parts.Add($"advanced x{AdvancedPerDay}/day");
            if (InnateIsMantic)
                parts.Add("innate(s) mantic");
            if (IsTeachingScroll)
                parts.Add("teaching scroll");
            if (AddBasicToBaseList)
                parts.Add("+basic list");
            if (AddAdvancedToBaseList)
                parts.Add("+advanced list");

            if (parts.Count == 0)
                return "No per-spell modifiers selected.";

            return string.Join(" • ", parts);
        }
    }

    public SpellSelectionEntry Clone()
    {
        return new SpellSelectionEntry(Spell)
        {
            BasicPerDay = BasicPerDay,
            AdvancedPerDay = AdvancedPerDay,
            InnateIsMantic = InnateIsMantic,
            IsTeachingScroll = IsTeachingScroll,
            AddBasicToBaseList = AddBasicToBaseList,
            AddAdvancedToBaseList = AddAdvancedToBaseList
        };
    }

    public void ApplyFrom(SpellSelectionEntry source)
    {
        if (source == null)
            return;

        BasicPerDay = source.BasicPerDay;
        AdvancedPerDay = source.AdvancedPerDay;
        InnateIsMantic = source.InnateIsMantic;
        IsTeachingScroll = source.IsTeachingScroll;
        AddBasicToBaseList = source.AddBasicToBaseList;
        AddAdvancedToBaseList = source.AddAdvancedToBaseList;
        NormalizeForSpellType();
    }

    public void NormalizeForSpellType()
    {
        if (IsAdvanced)
        {
            if (BasicPerDay != 0)
                BasicPerDay = 0;
            if (AddBasicToBaseList)
                AddBasicToBaseList = false;
        }
        else
        {
            if (AdvancedPerDay != 0)
                AdvancedPerDay = 0;
            if (AddAdvancedToBaseList)
                AddAdvancedToBaseList = false;
        }
    }

    private void Raise([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public class SpellConfig : ConfigBase
{
    private static readonly Color RowEvenColor = Colors.White;
    private static readonly Color RowOddColor = Color.FromArgb("#F6F6F6");

    public SpellConfig()
    {
        Name = "Spells";
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Breakdown) || e.PropertyName == nameof(BreakdownItems))
                return;
            RecalculateBreakdown();
        };

        SelectedSpells.CollectionChanged += OnSelectedSpellsChanged;
        RecalculateBreakdown();
    }

    protected override string NoneSelectedText => "Spells (none selected)";

    public ObservableCollection<SpellSelectionEntry> SelectedSpells { get; } = new();
    public bool HasSelectedSpells => SelectedSpells.Count > 0;

    private int _additionalGenericMana;
    public int AdditionalGenericMana
    {
        get => _additionalGenericMana;
        set =>
            SetProperty(
                ref _additionalGenericMana,
                Math.Clamp(value, 0, 12),
                affectsTotal: true,
                nameof(IsPowerStore));
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
                nameof(IsPowerStore));
    }

    private MagicColours? _additionalManaColour;
    public MagicColours? AdditionalManaColour
    {
        get => _additionalManaColour;
        set =>
            SetProperty(
                ref _additionalManaColour,
                value,
                affectsTotal: false,
                nameof(IsPowerStore));
    }

    public bool IsPowerStore =>
        AdditionalGenericMana > 0
        || (AdditionalManaOfColour > 0 && AdditionalManaColour.HasValue);

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

    public ObservableCollection<ContributionRow> BreakdownItems { get; } = new();

    private string _breakdown = string.Empty;
    public string Breakdown
    {
        get => _breakdown;
        private set
        {
            if (_breakdown == value)
                return;
            _breakdown = value;
            OnPropertyChanged();
        }
    }

    public bool TryAddSpell(SpellService.SpellRaw spell)
    {
        if (spell == null || string.IsNullOrWhiteSpace(spell.name))
            return false;

        var existing = SelectedSpells.FirstOrDefault(x =>
            string.Equals(x.SpellName, spell.name, StringComparison.OrdinalIgnoreCase)
            && x.Power == Math.Max(1, spell.level)
            && x.IsAdvanced == (spell.isAdvanced ?? false));

        if (existing != null)
            return false;

        var entry = new SpellSelectionEntry(spell);
        entry.NormalizeForSpellType();
        SelectedSpells.Add(entry);
        return true;
    }

    public void RemoveSpell(SpellSelectionEntry entry)
    {
        if (entry == null)
            return;
        SelectedSpells.Remove(entry);
    }

    protected override int BaseTotal()
    {
        var total = 0;
        foreach (var entry in SelectedSpells)
            total += CalculateSpellEntryTotal(entry);
        return total;
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

        return t;
    }

    private void OnSelectedSpellsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems.OfType<SpellSelectionEntry>())
                item.PropertyChanged -= OnSpellEntryPropertyChanged;
        }

        if (e.NewItems != null)
        {
            foreach (var item in e.NewItems.OfType<SpellSelectionEntry>())
                item.PropertyChanged += OnSpellEntryPropertyChanged;
        }

        RefreshSpellRowStyles();
        OnPropertyChanged(nameof(HasSelectedSpells));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(TotalWithBase));
        RecalculateBreakdown();
    }

    private void OnSpellEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is SpellSelectionEntry entry)
            entry.NormalizeForSpellType();

        if (e.PropertyName == nameof(SpellSelectionEntry.RowBackgroundColor))
            return;

        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(TotalWithBase));
        RecalculateBreakdown();
    }

    private void RefreshSpellRowStyles()
    {
        for (int i = 0; i < SelectedSpells.Count; i++)
            SelectedSpells[i].RowBackgroundColor = i % 2 == 0 ? RowEvenColor : RowOddColor;
    }

    private static int CalculateSpellEntryTotal(SpellSelectionEntry entry)
    {
        var baseCost = CalculateSpellEntryBaseCastCost(entry);
        var total = baseCost;

        if (entry.InnateIsMantic && baseCost > 0)
            total += baseCost * 3;

        if (entry.AddBasicToBaseList)
            total += 15;
        if (entry.AddAdvancedToBaseList)
            total += 18;
        if (entry.IsTeachingScroll)
            total += 2 * entry.Power;

        return total;
    }

    private static int CalculateSpellEntryBaseCastCost(SpellSelectionEntry entry)
    {
        var basic = 2 * entry.Power * Math.Max(0, entry.BasicPerDay);
        var advanced = 3 * entry.Power * Math.Max(0, entry.AdvancedPerDay);
        return basic + advanced;
    }

    private void RecalculateBreakdown()
    {
        var builder = new BreakdownBuilder();
        BreakdownItems.Clear();

        foreach (var entry in SelectedSpells)
        {
            var prefix = entry.DisplayName;

            var basicCost = 2 * entry.Power * Math.Max(0, entry.BasicPerDay);
            if (basicCost > 0)
                builder.Add($"{prefix} basic casts x{entry.BasicPerDay} @2×Power {entry.Power} = {basicCost}", basicCost);

            var advancedCost = 3 * entry.Power * Math.Max(0, entry.AdvancedPerDay);
            if (advancedCost > 0)
                builder.Add($"{prefix} advanced casts x{entry.AdvancedPerDay} @3×Power {entry.Power} = {advancedCost}", advancedCost);

            var castCost = basicCost + advancedCost;
            if (entry.InnateIsMantic && castCost > 0)
            {
                var manticExtra = castCost * 3;
                builder.Add($"{prefix} Innate(s) are mantic ×4 = {manticExtra}", manticExtra);
            }

            if (entry.AddBasicToBaseList)
                builder.Add($"{prefix} add basic spell to base list = 15", 15, includeWhenZero: true);

            if (entry.AddAdvancedToBaseList)
                builder.Add($"{prefix} add advanced spell to base list = 18", 18, includeWhenZero: true);

            if (entry.IsTeachingScroll)
            {
                var teachingCost = 2 * entry.Power;
                builder.Add($"{prefix} teaching scroll (2×Power) = {teachingCost}", teachingCost);
            }
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

        foreach (var row in builder.Rows)
            BreakdownItems.Add(row);

        var header = SelectedSpells.Count switch
        {
            0 => "Spells",
            1 => SelectedSpells[0].DisplayName,
            _ => $"{SelectedSpells.Count} spells"
        };

        Breakdown = builder.BuildSummary(header, Total);
    }
}
