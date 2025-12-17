using System.Collections.Generic;
using System.Linq;

namespace labyItems.Helpers;

public static class CostStringHelper
{
    public static string JoinSelections<T>(IEnumerable<T?>? items) where T : struct
    {
        if (items is null)
            return string.Empty;

        return string.Join(", ", items.Where(x => x.HasValue).Select(x => x!.Value.ToString()));
    }

    public static string FormatAdjustedText(string joinedSelections, double factor, int adjusted)
    {
        var vsPart = string.IsNullOrWhiteSpace(joinedSelections) ? string.Empty : $"vs {joinedSelections} ";
        return $"{vsPart}(×{factor:0.##}) = {adjusted}";
    }

    public static string FormatAcTableLine(string label, int ac, int cost) =>
        $"{label}: {ac} AC → {cost}";
}
