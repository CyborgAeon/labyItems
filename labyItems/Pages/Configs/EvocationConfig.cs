// Pages/Configs/EvocationConfig.cs
using System.ComponentModel;
using System.Runtime.CompilerServices;
using labyItems.Services;

namespace labyItems.Pages.Configs;

public class EvocationConfig : INotifyPropertyChanged
{
    private string _evocationName = "";
    private int _power;
    private int _basicPerDay;
    private int _advancedPerDay;
    private bool _addBasic;
    private bool _addAdvanced;
    private bool _addPrep;
    private int _drawOnEpPerDay;
    public string EvocationName
    {
        get => _evocationName;
        set { if (_evocationName == value) return; _evocationName = value; OnPropertyChanged(); OnPropertyChanged(nameof(Title)); OnPropertyChanged(nameof(Total)); }
    }

    public int Power
    {
        get => _power;
        set { if (_power == value) return; _power = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); }
    }

    public int BasicPerDay
    {
        get => _basicPerDay;
        set { if (_basicPerDay == value) return; _basicPerDay = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); }
    }
    public int AdvancedPerDay
    {
        get => _advancedPerDay;
        set { if (_advancedPerDay == value) return; _advancedPerDay = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); }
    }
    public bool AddBasic
    {
        get => _addBasic;
        set { if (_addBasic == value) return; _addBasic = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); }
    }
    public bool AddAdvanced
    {
        get => _addAdvanced;
        set { if (_addAdvanced == value) return; _addAdvanced = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); }
    }
    public bool AddPrep
    {
        get => _addPrep;
        set { if (_addPrep == value) return; _addPrep = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); }
    }

    public string Title => string.IsNullOrWhiteSpace(EvocationName) ? "Evocation (none selected)" : EvocationName;

    public int Total
    {
        get
        {
            double t = 0;
            t += 2 * Power * BasicPerDay;
            t += 3 * Power * AdvancedPerDay;
            t += 16 * _drawOnEpPerDay;
            if (AddBasic) t += 15;
            if (AddAdvanced) t += 18;
            if (AddPrep) t *= 1.5;
            return (int)Math.Round(t);
        }
    }

    public void ApplyEvocation(Evocation.Result picked)
    {
        EvocationName = picked.Name;
        Power = picked.Power;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
