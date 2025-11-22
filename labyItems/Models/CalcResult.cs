using System.Collections.Generic;
using System.Linq;

namespace labyItems.Models;

public class CalcResult
{
    public string AbilityType { get; set; } = string.Empty;
    public string AbilityName { get; set; } = string.Empty;
    public int TotalIsp { get; set; }
    public Dictionary<string, object?> Details { get; set; } = new();

    private string? _summaryOverride;
    public string Summary
    {
        get => string.IsNullOrWhiteSpace(_summaryOverride)
            ? CalcResultFormatter.ToSummary(this)
            : _summaryOverride;
        set => _summaryOverride = value;
    }
}

public static class CalcResultFormatter
{
    public static string ToSummary(CalcResult result)
    {
        var detailParts = result.Details?
            .Where(kv => kv.Value is not null && !string.IsNullOrWhiteSpace(kv.Key))
            .Select(kv => $"{kv.Key}={kv.Value}")
            .ToList() ?? new List<string>();

        var detailText = detailParts.Count > 0 ? string.Join(" | ", detailParts) + " | " : string.Empty;
        var ispText = $"ISP: {result.TotalIsp}";
        return detailText + ispText;
    }
}
