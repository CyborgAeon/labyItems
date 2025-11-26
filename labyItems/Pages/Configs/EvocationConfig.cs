using labyItems.Models.Enums;
using labyItems.Pages.Calculator;
using labyItems.Services;

namespace labyItems.Pages.Configs;

public class EvocationConfig : ConfigBase
{
    public EvocationConfig()
    {
        PropertyChanged += (_, e) =>
        {
            if (
                e.PropertyName == nameof(IsAdvanced)
                || e.PropertyName == nameof(EvocationName)
                || e.PropertyName == nameof(Name)
            )
            {
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(ShowBasicPerDay));
                OnPropertyChanged(nameof(ShowAdvancedPerDay));
            }
        };
    }

    private int _drawOnEpPerDay;
    public int DrawOnEpPerDay
    {
        get => _drawOnEpPerDay;
        set => SetProperty(ref _drawOnEpPerDay, Math.Max(0, value), affectsTotal: true);
    }

    private int _generalEpStore;
    public int GeneralEpStore
    {
        get => _generalEpStore;
        set =>
            SetProperty(
                ref _generalEpStore,
                Math.Clamp(value, 0, 12),
                affectsTotal: true,
                nameof(AnyEpStore)
            );
    }

    private int _fieldEpStore;
    public int FieldEpStore
    {
        get => _fieldEpStore;
        set =>
            SetProperty(
                ref _fieldEpStore,
                Math.Clamp(value, 0, 12),
                affectsTotal: SelectedField.HasValue,
                nameof(AnyEpStore)
            );
    }

    private EvocationFields? _selectedField;
    public EvocationFields? SelectedField
    {
        get => _selectedField;
        set => SetProperty(ref _selectedField, value, affectsTotal: FieldEpStore > 0);
    }

    public string EvocationName { get; set; } = "";
    protected override string NoneSelectedText => "Evocation (none selected)";
    public bool HasSelection => !string.IsNullOrWhiteSpace(EvocationName);
    public bool ShowBasicPerDay => HasSelection && IsAdvanced == false;
    public bool ShowAdvancedPerDay => HasSelection && IsAdvanced == true;

    public bool AnyEpStore => (GeneralEpStore > 0) || (FieldEpStore > 0);

    protected override int ExtraTotal()
    {
        int total = 0;
        total += DrawOnEpPerDay * 16;
        total += 4 * GeneralEpStore;
        total += 3 * FieldEpStore;
        return total;
    }

    public void ApplyEvocation(Evocation.Result picked)
    {
        Name = $"{picked.Name} ({picked.Power} EP)";
        EvocationName = picked.Name;
        Power = picked.Power;
        IsAdvanced = picked.IsAdvanced;
    }
}
