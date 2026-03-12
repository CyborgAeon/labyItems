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

public sealed class MiracleSelectionEntry : INotifyPropertyChanged, IConfigSelectionListItem
{
    private int _basicPerDay;
    private int _advancedPerDay;
    private bool _innateIsMantic;
    private bool _addBasicToBaseList;
    private bool _addAdvancedToBaseList;
    private bool _addWithPrep30;
    private bool _isTeachingScroll;
    private Color _rowBackgroundColor = Colors.White;

    public MiracleSelectionEntry(MiracleService.MiracRaw miracle)
    {
        Miracle = miracle ?? new MiracleService.MiracRaw();
        MiracleName = Miracle.name ?? string.Empty;
        Power = Math.Max(1, Miracle.power);
        IsAdvanced = Miracle.isAdvanced;
        Alignment = Miracle.alignment ?? string.Empty;

        if (IsAdvanced)
            _advancedPerDay = 1;
        else
            _basicPerDay = 1;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public MiracleService.MiracRaw Miracle { get; }
    public string MiracleName { get; }
    public int Power { get; }
    public bool IsAdvanced { get; }
    public string Alignment { get; }

    public string DisplayName =>
        IsAdvanced
            ? $"{MiracleName} (P{Power}, advanced)"
            : $"{MiracleName} (P{Power})";

    public string AlignmentSummary =>
        string.IsNullOrWhiteSpace(Alignment)
            ? string.Empty
            : $"Alignment: {Alignment}";

    public bool HasAlignmentSummary => !string.IsNullOrWhiteSpace(AlignmentSummary);

    public bool ShowBasicConfig => !IsAdvanced;
    public bool ShowAdvancedConfig => IsAdvanced;
    public bool ShowAddBasic => !IsAdvanced;
    public bool ShowAddAdvanced => IsAdvanced;

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
            Raise(nameof(HasInnates));
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
            Raise(nameof(HasInnates));
            Raise(nameof(InlineSummary));
        }
    }

    public bool HasInnates => BasicPerDay > 0 || AdvancedPerDay > 0;

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

    public bool AddBasicToBaseList
    {
        get => _addBasicToBaseList;
        set
        {
            if (_addBasicToBaseList == value)
                return;

            _addBasicToBaseList = value;
            if (value)
            {
                _addAdvancedToBaseList = false;
                _addWithPrep30 = false;
            }

            Raise(nameof(AddBasicToBaseList));
            Raise(nameof(AddAdvancedToBaseList));
            Raise(nameof(AddWithPrep30));
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
            {
                _addBasicToBaseList = false;
                _addWithPrep30 = false;
            }

            Raise(nameof(AddAdvancedToBaseList));
            Raise(nameof(AddBasicToBaseList));
            Raise(nameof(AddWithPrep30));
            Raise(nameof(InlineSummary));
        }
    }

    public bool AddWithPrep30
    {
        get => _addWithPrep30;
        set
        {
            if (_addWithPrep30 == value)
                return;

            _addWithPrep30 = value;
            if (value)
            {
                _addBasicToBaseList = false;
                _addAdvancedToBaseList = false;
            }

            Raise(nameof(AddWithPrep30));
            Raise(nameof(AddBasicToBaseList));
            Raise(nameof(AddAdvancedToBaseList));
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
                parts.Add("innate mantic");
            if (AddBasicToBaseList)
                parts.Add("+basic list");
            if (AddAdvancedToBaseList)
                parts.Add("+advanced list");
            if (AddWithPrep30)
                parts.Add("+prep 30s");
            if (IsTeachingScroll)
                parts.Add("scripture");

            if (parts.Count == 0)
                return "No per-miracle modifiers selected.";

            return string.Join(" • ", parts);
        }
    }

    public MiracleSelectionEntry Clone()
    {
        return new MiracleSelectionEntry(Miracle)
        {
            BasicPerDay = BasicPerDay,
            AdvancedPerDay = AdvancedPerDay,
            InnateIsMantic = InnateIsMantic,
            AddBasicToBaseList = AddBasicToBaseList,
            AddAdvancedToBaseList = AddAdvancedToBaseList,
            AddWithPrep30 = AddWithPrep30,
            IsTeachingScroll = IsTeachingScroll
        };
    }

    public void ApplyFrom(MiracleSelectionEntry source)
    {
        if (source == null)
            return;

        BasicPerDay = source.BasicPerDay;
        AdvancedPerDay = source.AdvancedPerDay;
        InnateIsMantic = source.InnateIsMantic;
        AddBasicToBaseList = source.AddBasicToBaseList;
        AddAdvancedToBaseList = source.AddAdvancedToBaseList;
        AddWithPrep30 = source.AddWithPrep30;
        IsTeachingScroll = source.IsTeachingScroll;
        NormalizeForMiracleType();
    }

    public void NormalizeForMiracleType()
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

public sealed class TrueBelieverGuildEntry : INotifyPropertyChanged
{
    private int _count = 1;
    private Color _rowBackgroundColor = Colors.White;

    public TrueBelieverGuildEntry(string guildName)
    {
        GuildName = (guildName ?? string.Empty).Trim();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string GuildName { get; }

    public int Count
    {
        get => _count;
        set
        {
            var next = Math.Max(0, value);
            if (_count == next)
                return;

            _count = next;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
        }
    }

    public Color RowBackgroundColor
    {
        get => _rowBackgroundColor;
        set
        {
            if (_rowBackgroundColor == value)
                return;

            _rowBackgroundColor = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RowBackgroundColor)));
        }
    }
}

public class MiracleConfig : ConfigBase
{
    private static readonly Color RowEvenColor = Colors.White;
    private static readonly Color RowOddColor = Color.FromArgb("#F6F6F6");

    public MiracleConfig()
    {
        Name = "Miracles";
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Breakdown) || e.PropertyName == nameof(BreakdownItems))
                return;
            RecalculateBreakdown();
        };

        SelectedMiracles.CollectionChanged += OnSelectedMiraclesChanged;
        TrueBelieverGuilds.CollectionChanged += OnTrueBelieverGuildsChanged;
        RecalculateBreakdown();
    }

    protected override string NoneSelectedText => "Miracles (none selected)";

    public ObservableCollection<MiracleSelectionEntry> SelectedMiracles { get; } = new();
    public bool HasSelectedMiracles => SelectedMiracles.Count > 0;
    public ObservableCollection<TrueBelieverGuildEntry> TrueBelieverGuilds { get; } = new();
    public bool HasTrueBelieverGuilds => TrueBelieverGuilds.Count > 0;

    private SpiritualSpheres? _additionalSphereSelected;
    public SpiritualSpheres? AdditionalSphereSelected
    {
        get => _additionalSphereSelected;
        set =>
            SetProperty(
                ref _additionalSphereSelected,
                value,
                affectsTotal: false,
                nameof(IsPowerStore));
    }

    public bool IsPowerStore => GeneralSpiritStore > 0 || (SphereSpiritStore > 0 && AdditionalSphereSelected.HasValue);

    private int _generalSpiritStore;
    public int GeneralSpiritStore
    {
        get => _generalSpiritStore;
        set =>
            SetProperty(
                ref _generalSpiritStore,
                Math.Clamp(value, 0, 12),
                affectsTotal: true,
                nameof(AnySpiritStore));
    }

    private int _sphereSpiritStore;
    public int SphereSpiritStore
    {
        get => _sphereSpiritStore;
        set =>
            SetProperty(
                ref _sphereSpiritStore,
                Math.Clamp(value, 0, 12),
                affectsTotal: true,
                nameof(AnySpiritStore));
    }

    public bool AnySpiritStore => GeneralSpiritStore > 0 || (SphereSpiritStore > 0 && AdditionalSphereSelected.HasValue);

    private bool _spiritStoreRegenerates;
    public bool SpiritStoreRegenerates
    {
        get => _spiritStoreRegenerates;
        set => SetProperty(ref _spiritStoreRegenerates, value, affectsTotal: true);
    }

    private int _turnBasicUpTo5thMantic;
    public int TurnBasicUpTo5thMantic
    {
        get => _turnBasicUpTo5thMantic;
        set => SetProperty(ref _turnBasicUpTo5thMantic, Math.Max(0, value), affectsTotal: true);
    }

    private int _turnBasicMantic;
    public int TurnBasicMantic
    {
        get => _turnBasicMantic;
        set => SetProperty(ref _turnBasicMantic, Math.Max(0, value), affectsTotal: true);
    }

    private int _turnAdvancedUpTo6thMantic;
    public int TurnAdvancedUpTo6thMantic
    {
        get => _turnAdvancedUpTo6thMantic;
        set => SetProperty(ref _turnAdvancedUpTo6thMantic, Math.Max(0, value), affectsTotal: true);
    }

    private int _turnAdvancedAbove6thMantic;
    public int TurnAdvancedAbove6thMantic
    {
        get => _turnAdvancedAbove6thMantic;
        set => SetProperty(ref _turnAdvancedAbove6thMantic, Math.Max(0, value), affectsTotal: true);
    }

    public int TrueBeliever => TrueBelieverGuilds.Sum(x => Math.Max(0, x.Count));

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

    public bool TryAddMiracle(MiracleService.MiracRaw miracle)
    {
        if (miracle == null || string.IsNullOrWhiteSpace(miracle.name))
            return false;

        var existing = SelectedMiracles.FirstOrDefault(x =>
            string.Equals(x.MiracleName, miracle.name, StringComparison.OrdinalIgnoreCase)
            && x.Power == Math.Max(1, miracle.power)
            && x.IsAdvanced == miracle.isAdvanced);

        if (existing != null)
            return false;

        var entry = new MiracleSelectionEntry(miracle);
        entry.NormalizeForMiracleType();
        SelectedMiracles.Add(entry);
        return true;
    }

    public bool TryAddTrueBelieverGuild(string guildName)
    {
        var trimmed = (guildName ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return false;

        var existing = TrueBelieverGuilds.FirstOrDefault(x =>
            string.Equals(x.GuildName, trimmed, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            existing.Count++;
            return true;
        }

        TrueBelieverGuilds.Add(new TrueBelieverGuildEntry(trimmed));
        return true;
    }

    public void RemoveTrueBelieverGuild(TrueBelieverGuildEntry entry)
    {
        if (entry == null)
            return;

        TrueBelieverGuilds.Remove(entry);
    }

    public void RemoveMiracle(MiracleSelectionEntry entry)
    {
        if (entry == null)
            return;

        SelectedMiracles.Remove(entry);
    }

    protected override int BaseTotal()
    {
        var total = 0;
        foreach (var entry in SelectedMiracles)
            total += CalculateMiracleEntryTotal(entry);

        return total;
    }

    protected override int ExtraTotal()
    {
        var extra = 0;

        if (GeneralSpiritStore > 0)
            extra += 4 * GeneralSpiritStore;
        if (SphereSpiritStore > 0 && AdditionalSphereSelected.HasValue)
            extra += 3 * SphereSpiritStore;
        if (AnySpiritStore && SpiritStoreRegenerates)
            extra += 25;

        if (TurnBasicUpTo5thMantic > 0)
            extra += 40 * TurnBasicUpTo5thMantic;
        if (TurnBasicMantic > 0)
            extra += 50 * TurnBasicMantic;
        if (TurnAdvancedUpTo6thMantic > 0)
            extra += 60 * TurnAdvancedUpTo6thMantic;
        if (TurnAdvancedAbove6thMantic > 0)
            extra += 80 * TurnAdvancedAbove6thMantic;

        if (TrueBeliever > 0)
            extra += 16 * TrueBeliever;

        return extra;
    }

    private void OnSelectedMiraclesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems.OfType<MiracleSelectionEntry>())
                item.PropertyChanged -= OnMiracleEntryPropertyChanged;
        }

        if (e.NewItems != null)
        {
            foreach (var item in e.NewItems.OfType<MiracleSelectionEntry>())
                item.PropertyChanged += OnMiracleEntryPropertyChanged;
        }

        RefreshMiracleRowStyles();
        OnPropertyChanged(nameof(HasSelectedMiracles));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(TotalWithBase));
        RecalculateBreakdown();
    }

    private void OnTrueBelieverGuildsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems.OfType<TrueBelieverGuildEntry>())
                item.PropertyChanged -= OnTrueBelieverGuildEntryPropertyChanged;
        }

        if (e.NewItems != null)
        {
            foreach (var item in e.NewItems.OfType<TrueBelieverGuildEntry>())
                item.PropertyChanged += OnTrueBelieverGuildEntryPropertyChanged;
        }

        RefreshTrueBelieverRowStyles();
        OnPropertyChanged(nameof(HasTrueBelieverGuilds));
        OnPropertyChanged(nameof(TrueBeliever));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(TotalWithBase));
        RecalculateBreakdown();
    }

    private void OnMiracleEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is MiracleSelectionEntry entry)
            entry.NormalizeForMiracleType();

        if (e.PropertyName == nameof(MiracleSelectionEntry.RowBackgroundColor))
            return;

        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(TotalWithBase));
        RecalculateBreakdown();
    }

    private void OnTrueBelieverGuildEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TrueBelieverGuildEntry.RowBackgroundColor))
            return;

        OnPropertyChanged(nameof(TrueBeliever));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(TotalWithBase));
        RecalculateBreakdown();
    }

    private void RefreshMiracleRowStyles()
    {
        for (int i = 0; i < SelectedMiracles.Count; i++)
            SelectedMiracles[i].RowBackgroundColor = i % 2 == 0 ? RowEvenColor : RowOddColor;
    }

    private void RefreshTrueBelieverRowStyles()
    {
        for (int i = 0; i < TrueBelieverGuilds.Count; i++)
            TrueBelieverGuilds[i].RowBackgroundColor = i % 2 == 0 ? RowEvenColor : RowOddColor;
    }

    private static int CalculateMiracleEntryTotal(MiracleSelectionEntry entry)
    {
        var basicCost = 2 * entry.Power * Math.Max(0, entry.BasicPerDay);
        var advancedCost = 3 * entry.Power * Math.Max(0, entry.AdvancedPerDay);
        var castCost = basicCost + advancedCost;

        var total = castCost;

        if (entry.InnateIsMantic && castCost > 0)
            total += castCost * 3;

        if (entry.IsAdvanced && entry.AddAdvancedToBaseList)
            total += 18;
        else if (!entry.IsAdvanced && entry.AddBasicToBaseList)
            total += 15;

        if (entry.AddWithPrep30)
            total += (int)Math.Round(entry.Power / 2.0, MidpointRounding.AwayFromZero);

        if (entry.IsTeachingScroll)
            total += 3 * entry.Power;

        return total;
    }

    private void RecalculateBreakdown()
    {
        var builder = new BreakdownBuilder();
        BreakdownItems.Clear();

        foreach (var entry in SelectedMiracles)
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
                builder.Add($"{prefix} innates are mantic ×4 = {castCost * 3}", castCost * 3);

            if (entry.IsAdvanced && entry.AddAdvancedToBaseList)
            {
                builder.Add($"{prefix} add advanced miracle to base list = 18", 18, includeWhenZero: true);
            }
            else if (!entry.IsAdvanced && entry.AddBasicToBaseList)
            {
                builder.Add($"{prefix} add basic miracle to base list = 15", 15, includeWhenZero: true);
            }
            else if (entry.AddWithPrep30)
            {
                var prepCost = (int)Math.Round(entry.Power / 2.0, MidpointRounding.AwayFromZero);
                builder.Add($"{prefix} add miracle with 30s prep (Power/2) = {prepCost}", prepCost, includeWhenZero: entry.Power > 0);
            }

            if (entry.IsTeachingScroll)
            {
                var teachingCost = 3 * entry.Power;
                builder.Add($"{prefix} scripture of faith (3×Power) = {teachingCost}", teachingCost);
            }
        }

        if (GeneralSpiritStore > 0)
        {
            var generalCost = 4 * GeneralSpiritStore;
            builder.Add($"General spirit store +{GeneralSpiritStore} @4 each = {generalCost}", generalCost);
        }

        if (SphereSpiritStore > 0 && AdditionalSphereSelected.HasValue)
        {
            var sphereCost = 3 * SphereSpiritStore;
            builder.Add($"{AdditionalSphereSelected.Value} spirit store +{SphereSpiritStore} @3 each = {sphereCost}", sphereCost);
        }

        if (AnySpiritStore && SpiritStoreRegenerates)
            builder.Add("Spirit store regenerates = 25", 25);

        if (TurnBasicUpTo5thMantic > 0)
        {
            var cost = 40 * TurnBasicUpTo5thMantic;
            builder.Add($"Turn basic to 5th mantic x{TurnBasicUpTo5thMantic} = {cost}", cost);
        }

        if (TurnBasicMantic > 0)
        {
            var cost = 50 * TurnBasicMantic;
            builder.Add($"Turn basic miracle mantic x{TurnBasicMantic} = {cost}", cost);
        }

        if (TurnAdvancedUpTo6thMantic > 0)
        {
            var cost = 60 * TurnAdvancedUpTo6thMantic;
            builder.Add($"Turn advanced to 6th mantic x{TurnAdvancedUpTo6thMantic} = {cost}", cost);
        }

        if (TurnAdvancedAbove6thMantic > 0)
        {
            var cost = 80 * TurnAdvancedAbove6thMantic;
            builder.Add($"Turn advanced above 6th mantic x{TurnAdvancedAbove6thMantic} = {cost}", cost);
        }

        foreach (var guild in TrueBelieverGuilds.Where(x => x.Count > 0))
        {
            var cost = 16 * guild.Count;
            builder.Add($"True believer ({guild.GuildName}) x{guild.Count} @16 = {cost}", cost);
        }

        foreach (var row in builder.Rows)
            BreakdownItems.Add(row);

        var header = SelectedMiracles.Count switch
        {
            0 => "Miracles",
            1 => SelectedMiracles[0].DisplayName,
            _ => $"{SelectedMiracles.Count} miracles"
        };

        Breakdown = builder.BuildSummary(header, Total);
    }
}
