
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Collections.ObjectModel;
using labyItems.Helpers;
using labyItems.Models.Enums;
using labyItems.Models;
using labyItems.Controls;
namespace labyItems.Pages.Configs;
public class ArmourConfig : ConfigBase
{
    public ObservableCollection<int?> ArmourLayers { get; } = new() { 0 };
    public ObservableCollection<ContributionRow> BreakdownItems { get; } = new();
    public const string magicString = "🪄 Magic";
    public const string spiritString = "⽰ Spirit";
    public const string manticString = "🍥 Mantic";
    private int _acBase;
    public int ACBase
    {
        get => _acBase;
        set { if (SetProperty(ref _acBase, Math.Max(0, value), affectsTotal: true)) OnPropertyChanged(nameof(Breakdown));}
        // var v = ; if (_acBase != v) { _acBase = v; OnPropertyChanged(); Recalculate(); } }
    }

    public bool ShowArmourColours =>
        SelectedArmour == magicString || SelectedArmour == manticString;
    public bool ShowArmourAlignments =>
        SelectedArmour == spiritString || SelectedArmour == manticString;

    public ObservableCollection<string> ArmourTypes { get; } =
        new() { magicString, spiritString, manticString };
    private string _selectedArmour;
    public string SelectedArmour
    {
        get => _selectedArmour;
        set
        {
            SetProperty(ref _selectedArmour, value, true);
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedArmour));
        }
    }
    private int _magicalColoursCount;
    public int MagicalColoursCount
    {
        get => _magicalColoursCount;
        set { if (SetProperty(ref _magicalColoursCount, Math.Max(0, value), affectsTotal: true)) OnPropertyChanged(nameof(Breakdown));}
    }

    private bool _spiritualNonOpposite;
    public bool SpiritualNonOpposite
    {
        get => _spiritualNonOpposite;
        set { if (SetProperty(ref _spiritualNonOpposite, value, affectsTotal: true)) OnPropertyChanged(nameof(Breakdown));}
    }

    private int _pac, _dac, _mac, _sac;
    public int PAC { get => _pac; set { if (SetProperty(ref _pac, Clamp0To6(value), affectsTotal: true))OnPropertyChanged(nameof(Breakdown));} }
    public int DAC { get => _dac; set { if (SetProperty(ref _dac, Clamp0To6(value), affectsTotal: true))OnPropertyChanged(nameof(Breakdown));} }
    public int MAC { get => _mac; set { if (SetProperty(ref _mac, Clamp0To6(value), affectsTotal: true))OnPropertyChanged(nameof(Breakdown));} }
    public int SAC { get => _sac; set { if (SetProperty(ref _sac, Clamp0To6(value), affectsTotal: true))OnPropertyChanged(nameof(Breakdown));} }
    
    // Outputs
    // private int _total;
    // public int Total { get => _total; private set { if (SetProperty(ref _total, value, affectsTotal: false))OnPropertyChanged(nameof(Breakdown)); } }

    private string _layeredSummary = string.Empty;
    public string LayeredSummary
    {
        get => _layeredSummary;
        set => SetProperty(ref _layeredSummary, value, affectsTotal: false);
    }

    private string _layeredBreakdown = string.Empty;
    public string LayeredBreakdown
    {
        get => _layeredBreakdown;
        set => SetProperty(ref _layeredBreakdown, value, affectsTotal: false);
    }

    private string _breakdown = "";
    public string Breakdown { get => _breakdown; private set { if (_breakdown != value) { _breakdown = value; OnPropertyChanged(); } } }

    // // Calculation
    protected override int ExtraTotal()
    {
        BreakdownItems.Clear();
        int total = 0;
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(LayeredBreakdown))
        {
            foreach (var line in LayeredBreakdown.Split('\n'))
            {
                var trimmed = line?.TrimEnd();
                if (!string.IsNullOrWhiteSpace(trimmed))
                    AddCost(trimmed!, 0, ref total, sb);
            }
        }

        if (SelectedArmour != null && ACBase > 0)
        {
            int perAc = SelectedArmour switch
            {
                magicString  => 2,
                spiritString => 2,
                manticString => 4,
                _ => 0
            };
            int flat = SelectedArmour switch
            {
                spiritString => 2,
                manticString => 2,
                _ => 0
            };
            int armourCost = perAc * ACBase + flat;
            AddCost($"{SelectedArmour} armour: {perAc} × AC({ACBase}) {(flat > 0 ? $"+ {flat} " : string.Empty)}= {armourCost}", armourCost, ref total, sb);
        }

        if (SelectedArmour == magicString && MagicalColoursCount > 0)
        {
            int c = 2 * MagicalColoursCount;
            AddCost($"+ Magical colours: 2 × {MagicalColoursCount} = {c}", c, ref total, sb);
        }

        if (SelectedArmour == spiritString && SpiritualNonOpposite)
        {
            AddCost("+ Spiritual non-opposite: 3", 3, ref total, sb);
        }

        AddTableCost(PAC, PacTable, "PAC", sb, ref total);
        AddTableCost(DAC, DacTable, "DAC", sb, ref total);
        AddTableCost(MAC, MacTable, "MAC", sb, ref total);
        AddTableCost(SAC, SacTable, "SAC", sb, ref total);

        Breakdown = sb.ToString().TrimEnd();
        return total;
    }

    private int Clamp0To6(int v) => Math.Min(6, Math.Max(0, v));

    public static readonly IReadOnlyList<int> PacTable = new[] { 0, 4, 12, 20, 32, 44, 60 };
    public static readonly IReadOnlyList<int> DacTable = new[] { 0, 6, 18, 30, 48, 66, 90 };
    public static readonly IReadOnlyList<int> MacTable = new[] { 0, 8, 24, 40, 64, 88, 120 };
    public static readonly IReadOnlyList<int> SacTable = new[] { 0, 6, 18, 30, 48, 66, 90 };


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

    public ObservableCollection<MagicColours?> ArmourColours { get; }
    public ObservableCollection<Alignments?> ArmourAlignments { get; }
    private int _armourColourCount;
    public int ArmourColourCount
    {
        get => _armourColourCount;
        set => SetProperty(ref _armourColourCount, Math.Max(0, value), affectsTotal: true);
    }

    private int _armourAlignmentCount;
    public int ArmourAlignmentCount
    {
        get => _armourAlignmentCount;
        set => SetProperty(ref _armourAlignmentCount, Math.Max(0, value), affectsTotal: true);
    }

    
    public static int GetTableCost(int ac, IReadOnlyList<int> table) =>
        table[Math.Min(table.Count - 1, Math.Max(0, ac))];

    private void AddTableCost(int ac, IReadOnlyList<int> table, string label, StringBuilder sb, ref int running)
    {
        int a = Math.Min(6, Math.Max(0, ac));
        int cost = table[a];
        if (a > 0)
            AddCost($"+{a} {label} = {cost}", cost, ref running, sb);
    }

    private void AddCost(string text, int cost, ref int running, StringBuilder builder)
    {
        if (cost > 0)
            running += cost;

        BreakdownItems.Add(new ContributionRow
        {
            Id = Guid.NewGuid().ToString(),
            Text = text,
            RunningTotal = running
        });
        builder.AppendLine(text);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
