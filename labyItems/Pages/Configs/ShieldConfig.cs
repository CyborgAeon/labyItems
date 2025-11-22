using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using labyItems.Models.Enums;

namespace labyItems.Pages.Configs;

public class ShieldConfig : INotifyPropertyChanged
{
    private ShieldKind _selectedShield;
    public ShieldKind SelectedShield { get => _selectedShield; set { if (_selectedShield != value) { _selectedShield = value; OnPropertyChanged(); } } }

    private int _baseIsp;
    public int BaseIsp
    {
        get => _baseIsp;
        set
        {
            if (_baseIsp != value)
            {
                _baseIsp = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalWithBase));
            }
        }
    }

    private int _magicalColoursCount;
    public int MagicalColoursCount { get => _magicalColoursCount; set { var v = Math.Max(0, value); if (_magicalColoursCount != v) { _magicalColoursCount = v; OnPropertyChanged(); } } }

    private bool _spiritualNonOpposite;
    public bool SpiritualNonOpposite { get => _spiritualNonOpposite; set { if (_spiritualNonOpposite != value) { _spiritualNonOpposite = value; OnPropertyChanged(); } } }

    // Outputs
    private int _total;
    public int Total
    {
        get => _total;
        private set
        {
            if (_total != value)
            {
                _total = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalWithBase));
            }
        }
    }
    public int TotalWithBase => BaseIsp + Total;

    private string _breakdown = "";
    public string Breakdown { get => _breakdown; private set { if (_breakdown != value) { _breakdown = value; OnPropertyChanged(); } } }

    // Calculation
    public void Recalculate()
    {
        int total = 0;
        var sb = new StringBuilder();

        int baseCost = SelectedShield switch
        {
            ShieldKind.Magical0   => 15,
            ShieldKind.Spiritual0 => 20,
            ShieldKind.Mantic0    => 35,
            _ => 0
        };
        if (baseCost > 0)
        {
            total += baseCost;
            sb.AppendLine($"Shield base: {SelectedShield} = {baseCost}");
        }

        if (SelectedShield == ShieldKind.Magical0 && MagicalColoursCount > 0)
        {
            int c = 2 * MagicalColoursCount;
            total += c;
            sb.AppendLine($"+ Magical colours: 2 × {MagicalColoursCount} = {c}");
        }

        if (SelectedShield == ShieldKind.Spiritual0 && SpiritualNonOpposite)
        {
            total += 3;
            sb.AppendLine("+ Spiritual non-opposite: 3");
        }

        Total = total;
        Breakdown = sb.ToString().TrimEnd();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
