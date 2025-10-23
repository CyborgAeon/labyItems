using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace labyItems.Pages.Configs;

public class SpellConfig : INotifyPropertyChanged
{
    private string _SpellName = "";
    private int _power;
    private int _basicPerDay;
    private int _advancedPerDay;
    private bool _addBasic;
    private bool _addAdvanced;
    private bool _addPrep;
    private int _makeSpellUnder7thMantic;
    private int _makeBasicSpellMantic;
    private int _makeAdvancedSpellMantic;
    private bool _isMantic;
    private int _additionalPower;
    private (int, string) _additionalPowerOfColour;
    private bool _powerStoreRegenerates;
    private bool _isTeachingScroll;
    public string SpellName
    {
        get => _SpellName;
        set { if (_SpellName == value) return; _SpellName = value; OnPropertyChanged(); OnPropertyChanged(nameof(Title)); OnPropertyChanged(nameof(Total)); }
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
    public int MakeSpellUnder7thMantic
    {
        get => _makeSpellUnder7thMantic;
        set { if (_makeSpellUnder7thMantic == value) return; _makeSpellUnder7thMantic = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); }
    }
    public int MakeBasicSpellMantic
    {
        get => _makeBasicSpellMantic;
        set { if (_makeBasicSpellMantic == value) return; _makeBasicSpellMantic = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); }
    }
    public int MakeAdvancedSpellMantic
    {
        get => _makeAdvancedSpellMantic;
        set { if (_makeAdvancedSpellMantic == value) return; _makeAdvancedSpellMantic = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); }
    }
    
    public bool InnateIsMantic
    {
        get => _isMantic;
        set { if (_isMantic == value) return; _isMantic = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); }
    }
    public int AdditionalPower
    {
        get => _additionalPower;
        set { if (_additionalPower == value) return; _additionalPower = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); }
    }
    public (int, string) AdditionalPowerOfColour
    {
        get => _additionalPowerOfColour;
        set { if (_additionalPowerOfColour == value) return; _additionalPowerOfColour = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); }
    }
    public bool PowerStoreRegenerates
    {
        get => _powerStoreRegenerates;
        set { if (_powerStoreRegenerates == value) return; _powerStoreRegenerates = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); }
    }
    public bool IsTeachingScroll
    {
        get => _isTeachingScroll;
        set { if (_isTeachingScroll == value) return; _isTeachingScroll = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); }
    }
    
    public string Title => string.IsNullOrWhiteSpace(SpellName) ? "Spell (none selected)" : SpellName;

    public int Total
    {
        get
        {
            double t = 0;
            t += 2  * Power * BasicPerDay;
            t += 3  * Power * AdvancedPerDay;
            t += 4  * AdditionalPower;
            t += 3  * AdditionalPowerOfColour.Item1;
            t += 40 * MakeSpellUnder7thMantic;
            t += 50 * MakeBasicSpellMantic;
            t += 80 * MakeAdvancedSpellMantic;
            if (AddBasic) t += 15;
            if (AddAdvanced) t += 18;
            if (AddPrep) t *= 1.5;
            if (InnateIsMantic) t *= 4;
            if (PowerStoreRegenerates) t += 15;
            return (int)Math.Round(t);
        }
    }

    public void ApplySpell(Spell.Result picked)
    {
        SpellName = picked.Name;
        Power = picked.Power;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
