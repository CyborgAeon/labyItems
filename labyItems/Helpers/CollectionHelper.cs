using System.Collections.Generic;
using System.Linq;

namespace labyItems.Helpers;

public static class CollectionHelper
{
    public static void AddDistinct<T>(ICollection<T> target, IEnumerable<T>? values, IEqualityComparer<T>? comparer = null)
    {
        if (target == null || values == null)
            return;

        if (comparer == null)
        {
            foreach (var value in values)
            {
                if (!target.Contains(value))
                    target.Add(value);
            }

            return;
        }

        foreach (var value in values)
        {
            if (!target.Any(existing => comparer.Equals(existing, value)))
                target.Add(value);
        }
    }
}
