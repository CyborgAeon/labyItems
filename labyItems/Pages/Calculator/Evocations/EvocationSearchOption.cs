using labyItems.Services;

namespace labyItems.Pages.Calculator;

public sealed class EvocationSearchOption
{
    public EvocationSearchOption(DruidEvocationService.EvocRaw evocation)
    {
        Evocation = evocation ?? new DruidEvocationService.EvocRaw();
        Name = Evocation.name ?? string.Empty;
        Power = Math.Max(1, Evocation.power);
        IsAdvanced = Evocation.isAdvanced;
        Fields = (Evocation.fields ?? new List<string>())
            .Where(field => !string.IsNullOrWhiteSpace(field))
            .ToList();
    }

    public DruidEvocationService.EvocRaw Evocation { get; }
    public string Name { get; }
    public int Power { get; }
    public bool IsAdvanced { get; }
    public IReadOnlyList<string> Fields { get; }
    public string FieldsSummary => Fields.Count == 0 ? string.Empty : string.Join(", ", Fields);
}
