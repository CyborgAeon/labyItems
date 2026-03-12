using System.Collections.Generic;
using System.Linq;

namespace labyItems.Controls;

public sealed record StickyFooterContent(
    IReadOnlyList<ContributionRow> Rows,
    bool HasRows,
    string EmptyMessage);

public static class StickyFooterContentBuilder
{
    public const string DefaultEmptyMessage = "No details selected yet. Add settings to see the breakdown.";

    public static StickyFooterContent Build(
        IEnumerable<ContributionRow>? sourceRows,
        string? emptyMessage = null)
    {
        var rows = sourceRows?
            .Where(row => row != null && !string.IsNullOrWhiteSpace(row.Text))
            .Select(row => new ContributionRow
            {
                Id = row.Id ?? string.Empty,
                Text = row.Text ?? string.Empty,
                RunningTotal = row.RunningTotal
            })
            .ToList()
            ?? new List<ContributionRow>();

        var resolvedEmptyMessage = string.IsNullOrWhiteSpace(emptyMessage)
            ? DefaultEmptyMessage
            : emptyMessage.Trim();

        return new StickyFooterContent(
            Rows: rows,
            HasRows: rows.Count > 0,
            EmptyMessage: resolvedEmptyMessage);
    }
}
