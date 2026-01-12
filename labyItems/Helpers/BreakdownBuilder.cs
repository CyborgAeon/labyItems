using System;
using System.Collections.Generic;
using labyItems.Controls;

namespace labyItems.Helpers;

public sealed class BreakdownBuilder
{
    private readonly List<ContributionRow> _rows = new();
    private int _running;

    public IReadOnlyList<ContributionRow> Rows => _rows;
    public int Total => _running;

    public void Add(string text, int cost, bool includeWhenZero = false)
    {
        if (!includeWhenZero && cost == 0)
            return;

        if (cost > 0)
            _running += cost;

        _rows.Add(
            new ContributionRow
            {
                Id = Guid.NewGuid().ToString(),
                Text = text,
                RunningTotal = _running,
            }
        );
    }

    public string BuildSummary(string? header, int? totalOverride = null, bool prefixLines = true)
    {
        var lines = new List<string>();
        var total = totalOverride ?? _running;

        if (!string.IsNullOrWhiteSpace(header))
            lines.Add($"{header.Trim()} ({total})");
        else if (_rows.Count > 0)
            lines.Add($"Total ({total})");

        foreach (var row in _rows)
        {
            var prefix = prefixLines ? "| " : string.Empty;
            lines.Add($"{prefix}{row.Text} ({row.RunningTotal})");
        }

        return string.Join("\n", lines);
    }
}
