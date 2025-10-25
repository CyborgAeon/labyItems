using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace labyItems.Pages.Configs;

public enum ConsumableType
{
    None,
    BatchOfPotions,
    MagicalScroll,     // basic magical scroll
    SpiritualScroll,   // basic spiritual scroll
    DruidicTalisman,   // any druidic talisman
    NeuronicShard
}

public record ConsumableEntry(string Name, int Value); 
// Value is: difficulty / MP / SP / EP / TBLPcost depending on Type

public class ConsumableConfig : INotifyPropertyChanged
{
    private ConsumableType _type = ConsumableType.None;
    public ConsumableType Type
    {
        get => _type;
        set { if (_type != value) { _type = value; OnPropertyChanged(); Recalculate(); } }
    }

    // Selected from search
    private string _selectedName = "(none)";
    public string SelectedName { get => _selectedName; private set { if (_selectedName != value) { _selectedName = value; OnPropertyChanged(); OnPropertyChanged(nameof(SelectedText)); } } }

    private int _selectedValue; // difficulty / MP / SP / EP / TBLPcost
    public int SelectedValue { get => _selectedValue; private set { if (_selectedValue != value) { _selectedValue = value; OnPropertyChanged(); Recalculate(); } } }

    // Modifiers
    private int _focussingCrystals; // +2 each
    public int FocussingCrystals
    {
        get => _focussingCrystals;
        set { var v = Math.Max(0, value); if (_focussingCrystals != v) { _focussingCrystals = v; OnPropertyChanged(); Recalculate(); } }
    }

    private int _batches500Grulls; // +1 each
    public int Batches500Grulls
    {
        get => _batches500Grulls;
        set { var v = Math.Max(0, value); if (_batches500Grulls != v) { _batches500Grulls = v; OnPropertyChanged(); Recalculate(); } }
    }

    // Outputs
    private int _total;
    public int Total { get => _total; private set { if (_total != value) { _total = value; OnPropertyChanged(); } } }

    private string _breakdown = "";
    public string Breakdown { get => _breakdown; private set { if (_breakdown != value) { _breakdown = value; OnPropertyChanged(); } } }

    public string SelectedText
        => Type switch
        {
            ConsumableType.BatchOfPotions => $"{SelectedName} (difficulty {SelectedValue})",
            ConsumableType.MagicalScroll  => $"{SelectedName} (MP {SelectedValue})",
            ConsumableType.SpiritualScroll=> $"{SelectedName} (SP {SelectedValue})",
            ConsumableType.DruidicTalisman=> $"{SelectedName} (EP {SelectedValue})",
            ConsumableType.NeuronicShard  => $"{SelectedName} (TBLPcost {SelectedValue})",
            _ => "(none)"
        };

    public void ApplyEntry(ConsumableEntry e)
    {
        SelectedName  = e.Name;
        SelectedValue = Math.Max(0, e.Value);
        Recalculate();
    }

    public void Recalculate()
    {
        int total = 0;
        var sb = new StringBuilder();

        // Base (from selected item)
        if (Type != ConsumableType.None && SelectedValue > 0)
        {
            int baseCost = Type switch
            {
                ConsumableType.BatchOfPotions => 1 * SelectedValue, // 1 * difficulty
                ConsumableType.MagicalScroll  => 1 * SelectedValue, // 1 * MP
                ConsumableType.SpiritualScroll=> 1 * SelectedValue, // 1 * SP
                ConsumableType.DruidicTalisman=> 1 * SelectedValue, // 1 * EP
                ConsumableType.NeuronicShard  => 1 * SelectedValue, // 1 * TBLPcost
                _ => 0
            };
            total += baseCost;
            sb.AppendLine($"Selected: {SelectedText} = {baseCost}");
        }
        else
        {
            if (Type != ConsumableType.None)
                sb.AppendLine("Selected: (missing search result)");
        }

        // Modifiers
        if (FocussingCrystals > 0)
        {
            int c = 2 * FocussingCrystals;
            total += c;
            sb.AppendLine($"+ Focussing crystals: 2 × {FocussingCrystals} = {c}");
        }

        if (Batches500Grulls > 0)
        {
            int c = 1 * Batches500Grulls;
            total += c;
            sb.AppendLine($"+ 500 grulls: 1 × {Batches500Grulls} = {c}");
        }

        Total = total;
        Breakdown = sb.ToString().TrimEnd();
    }

    // INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
