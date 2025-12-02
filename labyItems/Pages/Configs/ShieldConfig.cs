using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using labyItems.Controls;
using labyItems.Helpers;
using labyItems.Models.Enums;

namespace labyItems.Pages.Configs;

public class ShieldConfig : ConfigBase
{
    public ShieldConfig()
    {
        PacOptions = BuildTableDictionary(ArmourConfig.PacTable);
        DacOptions = BuildTableDictionary(ArmourConfig.DacTable);
        MacOptions = BuildTableDictionary(ArmourConfig.MacTable);
        SacOptions = BuildTableDictionary(ArmourConfig.SacTable);
        MacColours = new ObservableCollection<MagicColours?> { null };
        SacAlignments = new ObservableCollection<Alignments?> { null };
        ShieldColours = new ObservableCollection<MagicColours?> { null };
        ShieldAlignments = new ObservableCollection<Alignments?> { null };
        BreakdownItems = new ObservableCollection<ContributionRow>();
    }

    private ShieldType _selectedShield;
    public ShieldType SelectedShield
    {
        get => _selectedShield;
        set
        {
            var previous = _selectedShield;
            if (
                SetProperty(
                    ref _selectedShield,
                    value,
                    affectsTotal: true,
                    nameof(ShowMacColours),
                    nameof(ShowSacAlignment),
                    nameof(ShowShieldColours),
                    nameof(ShowShieldAlignments)
                )
            )
            {
                ResetHiddenSelections(previous, value);
            }
        }
    }

    private int _magicalColoursCount;
    public int MagicalColoursCount
    {
        get => _magicalColoursCount;
        set => SetProperty(ref _magicalColoursCount, Math.Max(0, value), affectsTotal: true);
    }

    private int _pac,
        _dac,
        _mac,
        _sac;
    public int PAC
    {
        get => _pac;
        set => SetProperty(ref _pac, Clamp0To6(value), affectsTotal: true);
    }

    public int DAC
    {
        get => _dac;
        set => SetProperty(ref _dac, Clamp0To6(value), affectsTotal: true);
    }

    public int MAC
    {
        get => _mac;
        set =>
            SetProperty(
                ref _mac,
                Clamp0To6(value),
                affectsTotal: true,
                alsoNotify: nameof(ShowMacColours)
            );
    }

    public int SAC
    {
        get => _sac;
        set =>
            SetProperty(
                ref _sac,
                Clamp0To6(value),
                affectsTotal: true,
                alsoNotify: nameof(ShowSacAlignment)
            );
    }

    public ObservableCollection<MagicColours?> MacColours { get; }
    private int _macColoursCount;
    public int MacColoursCount
    {
        get => _macColoursCount;
        set => SetProperty(ref _macColoursCount, Math.Max(0, value), affectsTotal: true);
    }

    public ObservableCollection<Alignments?> SacAlignments { get; }
    private int _sacAlignmentCount;
    public int SacAlignmentCount
    {
        get => _sacAlignmentCount;
        set => SetProperty(ref _sacAlignmentCount, Math.Max(0, value), affectsTotal: true);
    }

    public bool ShowMacColours => MAC > 0;
    public bool ShowSacAlignment => SAC > 0;

    public ObservableCollection<MagicColours?> ShieldColours { get; }
    public ObservableCollection<Alignments?> ShieldAlignments { get; }
    private int _shieldColourCount;
    public int ShieldColourCount
    {
        get => _shieldColourCount;
        set => SetProperty(ref _shieldColourCount, Math.Max(0, value), affectsTotal: true);
    }

    private int _shieldAlignmentCount;
    public int ShieldAlignmentCount
    {
        get => _shieldAlignmentCount;
        set => SetProperty(ref _shieldAlignmentCount, Math.Max(0, value), affectsTotal: true);
    }

    public bool ShowShieldColours =>
        SelectedShield == ShieldType.Magical || SelectedShield == ShieldType.Mantic;
    public bool ShowShieldAlignments =>
        SelectedShield == ShieldType.Mantic || SelectedShield == ShieldType.Spiritual;

    public ObservableCollection<ContributionRow> BreakdownItems { get; }

    private string _breakdown = "";
    public string Breakdown
    {
        get => _breakdown;
        private set => SetProperty(ref _breakdown, value, affectsTotal: false);
    }

    public IDictionary<string, int> PacOptions { get; }
    public IDictionary<string, int> DacOptions { get; }
    public IDictionary<string, int> MacOptions { get; }
    public IDictionary<string, int> SacOptions { get; }

    protected override int BaseTotal() => 0;

    protected override int ExtraTotal()
    {
        BreakdownItems.Clear();
        int running = 0;
        var sb = new StringBuilder();

        var shieldColours =
            ShieldColours?.Where(c => c.HasValue).Select(c => c!.Value).ToList()
            ?? new List<MagicColours>();
        var shieldAlignments =
            ShieldAlignments?.Where(a => a.HasValue).Select(a => a!.Value).ToList()
            ?? new List<Alignments>();

        int baseCost = SelectedShield switch
        {
            ShieldType.Magical => 15,
            ShieldType.Spiritual => 20,
            ShieldType.Mantic => 35,
            _ => 0,
        };
        AddCost($"{SelectedShield} Shield {baseCost}", baseCost, ref running, sb);

        foreach (var entry in shieldColours.Select((c, i) => (c, i)))
        {
            AddCost(
                entry.i == 0 ? $"Shield is {entry.c}" : $"Shield is also {entry.c} (+2)",
                entry.i == 0 ? 0 : 2,
                ref running,
                sb
            );
        }

        foreach (var entry in shieldAlignments.Select((a, i) => (a, i)))
        {
            AddCost(
                entry.i == 0 ? $"Shield is {entry.a}" : $"Shield is also {entry.a} (+3)",
                entry.i == 0 ? 0 : 3,
                ref running,
                sb
            );
        }

        AddTableCost(PAC, ArmourConfig.PacTable, "PAC", ref running, sb);
        AddTableCost(DAC, ArmourConfig.DacTable, "DAC", ref running, sb);

        AddMacCost(ref running, sb);
        AddSacCost(ref running, sb);

        Breakdown = sb.ToString().TrimEnd();
        return running;
    }

    private int AddMacCost(ref int running, StringBuilder sb)
    {
        int baseCost = ArmourConfig.MacTable[Clamp0To6(MAC)];
        if (MAC == 0)
            return 0;

        double factor = MacColoursCount switch
        {
            0 => 1.0,
            1 => 0.5,
            _ => 2.0 / 3.0,
        };

        int adjusted = (int)Math.Round(baseCost * factor, MidpointRounding.AwayFromZero);
        var macColourList = CostStringHelper.JoinSelections(MacColours);
        var adjustedText = CostStringHelper.FormatAdjustedText(macColourList, factor, adjusted);
        AddCost(
            $"+{MAC} MAC {(MacColoursCount == 0 ? $"= {baseCost}" : adjustedText)}",
            adjusted,
            ref running,
            sb
        );
        return adjusted;
    }

    private int AddSacCost(ref int running, StringBuilder sb)
    {
        int baseCost = ArmourConfig.SacTable[Clamp0To6(SAC)];
        if (SAC == 0)
            return 0;

        double factor = SacAlignmentCount > 0 ? 2.0 / 3.0 : 1.0;
        int adjusted = (int)Math.Round(baseCost * factor, MidpointRounding.AwayFromZero);
        var sacAlignmentList = CostStringHelper.JoinSelections(SacAlignments);
        var adjustedText = CostStringHelper.FormatAdjustedText(sacAlignmentList, factor, adjusted);
        AddCost(
            $"SAC: {SAC} AC → {baseCost} {(SacAlignmentCount == 0 ? string.Empty : adjustedText)}",
            adjusted,
            ref running,
            sb
        );
        return adjusted;
    }

    private static IDictionary<string, int> BuildTableDictionary(IReadOnlyList<int> table) =>
        table
            .Select((v, i) => new KeyValuePair<string, int>(i.ToString(), v))
            .ToDictionary(k => k.Key, v => v.Value);

    private static int Clamp0To6(int v) => Math.Min(6, Math.Max(0, v));

    private int AddTableCost(
        int ac,
        IReadOnlyList<int> table,
        string label,
        ref int running,
        StringBuilder sb
    )
    {
        int a = Clamp0To6(ac);
        int cost = table[a];
        if (a > 0)
            AddCost(CostStringHelper.FormatAcTableLine(label, a, cost), cost, ref running, sb);
        return cost;
    }

    private void AddCost(string text, int cost, ref int running, StringBuilder sb)
    {
        if (cost > 0)
            running += cost;

        BreakdownItems.Add(
            new ContributionRow
            {
                Id = Guid.NewGuid().ToString(),
                Text = text,
                RunningTotal = running,
            }
        );
        sb.AppendLine(text);
    }

    private void ResetHiddenSelections(ShieldType previous, ShieldType current)
    {
        if (!ShowShieldColours)
        {
            ShieldColours.Clear();
            ShieldColours.Add(null);
            ShieldColourCount = 0;
        }

        if (!ShowShieldAlignments)
        {
            ShieldAlignments.Clear();
            ShieldAlignments.Add(null);
            ShieldAlignmentCount = 0;
        }
    }
}
