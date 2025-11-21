using labyItems.Services;
using labyItems.Pages.Calculator;
namespace labyItems.Pages.Configs;

public class EvocationConfig : ConfigBase
{
    private int _drawOnEpPerDay;
    public int DrawOnEpPerDay
    {
        get => _drawOnEpPerDay;
        set => SetProperty(ref _drawOnEpPerDay, value, affectsTotal: true);
    }

    public string EvocationName { get; set; } = "";
    protected override string NoneSelectedText => "Evocation (none selected)";

    protected override int ExtraTotal()
    {
        return DrawOnEpPerDay * 16;
    }

    public void ApplyEvocation(Evocation.Result picked)
    {
        Name = $"{picked.Name} ({picked.Power} EP)";
        EvocationName = picked.Name;
        Power = picked.Power;
        IsAdvanced = picked.IsAdvanced;
    }
}
