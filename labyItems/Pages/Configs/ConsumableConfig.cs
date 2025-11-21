using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text;

namespace labyItems.Pages.Configs;

public enum ConsumableType
{
    None,
    BatchOfPotions,
    MagicalScroll,
    SpiritualScroll,
    DruidicTalisman,
    NeuronicShard
}

public record ConsumableEntry(string Name, int Value);

public class ConsumableConfig : ConfigBase
{
    private ConsumableType _type = ConsumableType.None;
    public ConsumableType Type
    {
        get => _type;
        set => SetProperty(ref _type, value, affectsTotal: true, alsoNotify: nameof(SelectedText));
    }

    private string _selectedName = "(none)";
    public string SelectedName
    {
        get => _selectedName;
        set => SetProperty(ref _selectedName, value, affectsTotal: true, alsoNotify: nameof(SelectedText));
    }

    private int _selectedValue;
    public int SelectedValue
    {
        get => _selectedValue;
        set
        {
            var v = Math.Max(0, value);
            SetProperty(ref _selectedValue, v, affectsTotal: true, nameof(SelectedText));
        }
    }

    private int _focussingCrystals;
    public int FocussingCrystals
    {
        get => _focussingCrystals;
        set
        {
            var v = Math.Max(0, value);
            SetProperty(ref _focussingCrystals, v, affectsTotal: true);
        }
    }

    private int _batches500Grulls;
    public int Batches500Grulls
    {
        get => _batches500Grulls;
        set
        {
            var v = Math.Max(0, value);
            SetProperty(ref _batches500Grulls, v, affectsTotal: true);
        }
    }

    private string _breakdown = "";
    public string Breakdown
    {
        get => _breakdown;
        private set => SetProperty(ref _breakdown, value, affectsTotal: false);
    }

    protected override string NoneSelectedText => "Consumable (none selected)";

    public string SelectedText =>
        Type switch
        {
            ConsumableType.BatchOfPotions  => $"{SelectedName} (difficulty {SelectedValue})",
            ConsumableType.MagicalScroll   => $"{SelectedName} (MP {SelectedValue})",
            ConsumableType.SpiritualScroll => $"{SelectedName} (SP {SelectedValue})",
            ConsumableType.DruidicTalisman => $"{SelectedName} (EP {SelectedValue})",
            ConsumableType.NeuronicShard   => $"{SelectedName} (TBLPcost {SelectedValue})",
            _                              => "(none)"
        };

    public void ApplyEntry(ConsumableEntry e)
    {
        SelectedName = e.Name;
        SelectedValue = e.Value;
    }

    protected override int ExtraTotal()
    {
        int total = 0;
        var sb = new StringBuilder();

        if (Type != ConsumableType.None && SelectedValue > 0)
        {
            int baseCost = Type switch
            {
                ConsumableType.BatchOfPotions  => SelectedValue,
                ConsumableType.MagicalScroll   => SelectedValue,
                ConsumableType.SpiritualScroll => SelectedValue,
                ConsumableType.DruidicTalisman => SelectedValue,
                ConsumableType.NeuronicShard   => SelectedValue,
                _                              => 0
            };

            total += baseCost;
            sb.AppendLine($"Selected: {SelectedText} = {baseCost}");
        }
        else
        {
            if (Type != ConsumableType.None)
                sb.AppendLine("Selected: (missing search result)");
        }

        if (FocussingCrystals > 0)
        {
            int c = 2 * FocussingCrystals;
            total += c;
            sb.AppendLine($"+ Focussing crystals: 2 × {FocussingCrystals} = {c}");
        }

        if (Batches500Grulls > 0)
        {
            int c = Batches500Grulls;
            total += c;
            sb.AppendLine($"+ 500 grulls: 1 × {Batches500Grulls} = {c}");
        }

        Breakdown = sb.ToString().TrimEnd();
        return total;
    }
}
