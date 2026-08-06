using labyItems.Models.Enums;
using labyItems.Services;

namespace labyItems.Pages.Calculator;

public sealed class NeuronicSearchOption
{
    public NeuronicSearchOption(NeuronicService.NeuronicRaw neuronic)
    {
        Neuronic = neuronic ?? new NeuronicService.NeuronicRaw();
        Name = Neuronic.name ?? string.Empty;
        Power = Math.Max(1, Neuronic.power);
        Type = Neuronic.Type;
    }

    public NeuronicService.NeuronicRaw Neuronic { get; }
    public string Name { get; }
    public int Power { get; }
    public NeuroOptionType Type { get; }
    public string TypeLabel => Type == NeuroOptionType.None ? "none" : Type.ToString().ToLowerInvariant();
}
