using labyItems.Services;

namespace labyItems.Pages.Calculator;

public sealed class MiracleSearchOption
{
    public MiracleSearchOption(MiracleService.MiracRaw miracle)
    {
        Miracle = miracle ?? new MiracleService.MiracRaw();
        Name = Miracle.name ?? string.Empty;
        Power = Math.Max(1, Miracle.power);
        IsAdvanced = Miracle.isAdvanced;
        Alignment = Miracle.alignment ?? string.Empty;
        Sphere = Miracle.sphere ?? string.Empty;
    }

    public MiracleService.MiracRaw Miracle { get; }
    public string Name { get; }
    public int Power { get; }
    public bool IsAdvanced { get; }
    public string Alignment { get; }
    public string Sphere { get; }
}
