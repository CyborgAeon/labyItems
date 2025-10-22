
using System.ComponentModel;
using System.Runtime.CompilerServices;
using labyItems.Services;
namespace labyItems.Pages.Configs;

public class EvocationConfig : INotifyPropertyChanged
{
    public EarthPowerService.EvocEntry BaseEvocation { get; }
    public EvocationConfig(EarthPowerService.EvocEntry baseEvoc)
    {
        BaseEvocation = baseEvoc;
    }

    private int _basicPerDay;
    private int _advancedPerDay;
    private bool _addBasic;
    private bool _addAdvanced;
    private bool _addPrep;

    public int BasicPerDay
    {
        get => _basicPerDay;
        set { _basicPerDay = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); }
    }
    public int AdvancedPerDay
    {
        get => _advancedPerDay;
        set { _advancedPerDay = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); }
    }

    public bool AddBasic
    {
        get => _addBasic;
        set { _addBasic = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); }
    }
    public bool AddAdvanced
    {
        get => _addAdvanced;
        set { _addAdvanced = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); }
    }
    public bool AddPrep
    {
        get => _addPrep;
        set { _addPrep = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); }
    }

    public int Total
    {
        get
        {
            var p = BaseEvocation.Power;
            double total = 0;

            total += 2 * p * BasicPerDay;
            total += 3 * p * AdvancedPerDay;

            if (AddBasic) total += 15;
            if (AddAdvanced) total += 18;

            if (AddPrep)
                total *= 1.5;

            return (int)Math.Round(total);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
