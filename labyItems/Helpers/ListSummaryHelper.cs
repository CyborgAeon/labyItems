using labyItems.Models.Enums;
using System.Collections.ObjectModel;

namespace labyItems.Helpers;

public static class ListSummaryHelper
{
    public static string JoinWithAnd(IEnumerable<string> items)
    {
        var list = items.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

        return list.Count switch
        {
            0 => "",
            1 => list[0],
            2 => $"{list[0]} and {list[1]}",
            _ => $"{string.Join(", ", list.Take(list.Count - 1))} and {list.Last()}",
        };
    }

    public static string ListOrDefault<T>(ObservableCollection<T?> items, string defaultValue)
    {
        if (items == null)
            return defaultValue;

        var list = items
            .Select(x => x?.ToString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (list.Count == 0)
            return defaultValue;

        return JoinWithAnd(list);
    }

    public static string BuildEmpowerMagicSummary(int count, ObservableCollection<MagicColours?> colours)
    {
        var colourText = ListOrDefault(colours, MagicColours.Grey.ToString());

        return $"+0 {colourText} magic, Empower weapon {count}/day for 5 minutes.";
    }

    public static string BuildEmpowerSpiritSummary(int count, ObservableCollection<Alignments?> alignments)
    {
        var alignmentText = ListOrDefault(alignments, Alignments.Neutral.ToString());

        return $"+0 {alignmentText} spirit, Empower weapon {count}/day for 5 minutes.";
    }

    public static string BuildEmpowerManticSummary(
        int count,
        ObservableCollection<Alignments?> alignments,
        ObservableCollection<MagicColours?> colours
    )
    {
        var alignmentText = ListOrDefault(alignments, Alignments.Neutral.ToString());
        var colourText = ListOrDefault(colours, MagicColours.Grey.ToString());

        return $"+0 {alignmentText}/{colourText} mantic, Empower weapon {count}/day for 5 minutes.";
    }
}
