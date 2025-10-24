
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using labyItems.Models.Enums;

public class ArmourConfig : INotifyPropertyChanged
{
    // Inputs
    private int _acBase;
    public int ACBase { get => _acBase; set { if (_acBase != value) { _acBase = Math.Max(0, value); OnPropertyChanged(); } } }

    private ArmourKind _selectedArmour;
    public ArmourKind SelectedArmour { get => _selectedArmour; set { if (_selectedArmour != value) { _selectedArmour = value; OnPropertyChanged(); } } }

    private int _magicalColoursCount;
    public int MagicalColoursCount { get => _magicalColoursCount; set { var v = Math.Max(0, value); if (_magicalColoursCount != v) { _magicalColoursCount = v; OnPropertyChanged(); } } }

    private bool _spiritualNonOpposite;
    public bool SpiritualNonOpposite { get => _spiritualNonOpposite; set { if (_spiritualNonOpposite != value) { _spiritualNonOpposite = value; OnPropertyChanged(); } } }

    private int _pac, _dac, _mac, _sac;
    public int PAC { get => _pac; set { var v = Clamp0To6(value); if (_pac != v) { _pac = v; OnPropertyChanged(); } } }
    public int DAC { get => _dac; set { var v = Clamp0To6(value); if (_dac != v) { _dac = v; OnPropertyChanged(); } } }
    public int MAC { get => _mac; set { var v = Clamp0To6(value); if (_mac != v) { _mac = v; OnPropertyChanged(); } } }
    public int SAC { get => _sac; set { var v = Clamp0To6(value); if (_sac != v) { _sac = v; OnPropertyChanged(); } } }

    // Outputs
    private int _total;
    public int Total { get => _total; private set { if (_total != value) { _total = value; OnPropertyChanged(); } } }

    private string _breakdown = "";
    public string Breakdown { get => _breakdown; private set { if (_breakdown != value) { _breakdown = value; OnPropertyChanged(); } } }

    // Calculation
    public void Recalculate()
    {
        int total = 0;
        var sb = new StringBuilder();

        if (SelectedArmour != ArmourKind.None && ACBase > 0)
        {
            int perAc = SelectedArmour switch
            {
                ArmourKind.MagicalMasterCrafted   => 2,
                ArmourKind.SpiritualMasterCrafted => 2,
                ArmourKind.ManticMasterCrafted    => 4,
                _ => 0
            };
            int flat = SelectedArmour switch
            {
                ArmourKind.MagicalMasterCrafted   => 0,
                ArmourKind.SpiritualMasterCrafted => 2,
                ArmourKind.ManticMasterCrafted    => 2,
                _ => 0
            };
            int armourCost = perAc * ACBase + flat;
            total += armourCost;
            sb.AppendLine($"Armour: {perAc} × AC({ACBase}) + {flat} = {armourCost}");
        }

        if (SelectedArmour == ArmourKind.MagicalMasterCrafted && MagicalColoursCount > 0)
        {
            int c = 2 * MagicalColoursCount;
            total += c;
            sb.AppendLine($"+ Magical colours: 2 × {MagicalColoursCount} = {c}");
        }

        if (SelectedArmour == ArmourKind.SpiritualMasterCrafted && SpiritualNonOpposite)
        {
            total += 3;
            sb.AppendLine("+ Spiritual non-opposite: 3");
        }

        total += AddTableCost(PAC, PacTable, "PAC", sb);
        total += AddTableCost(DAC, DacTable, "DAC", sb);
        total += AddTableCost(MAC, MacTable, "MAC", sb);
        total += AddTableCost(SAC, SacTable, "SAC", sb);

        Total = total;
        Breakdown = sb.ToString().TrimEnd();
    }

    private static int Clamp0To6(int v) => Math.Min(6, Math.Max(0, v));

    private static readonly int[] PacTable = { 0, 4, 12, 20, 32, 44, 60 };
    private static readonly int[] DacTable = { 0, 6, 18, 30, 48, 66, 90 };
    private static readonly int[] MacTable = { 0, 8, 24, 40, 64, 88, 120 };
    private static readonly int[] SacTable = { 0, 6, 18, 30, 48, 66, 90 };

    private static int AddTableCost(int ac, int[] table, string label, StringBuilder sb)
    {
        int a = Math.Min(6, Math.Max(0, ac));
        int cost = table[a];
        if (a > 0) sb.AppendLine($"{label}: {a} AC → {cost}");
        return cost;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}