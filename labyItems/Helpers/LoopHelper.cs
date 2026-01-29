using System;
using System.Collections.Generic;

namespace labyItems.Helpers;

public static class LoopHelper
{
    public static IEnumerable<(TOuter Outer, TInner Inner)> Flatten<TOuter, TInner>(
        IEnumerable<TOuter>? outer,
        Func<TOuter, IEnumerable<TInner>?> innerSelector)
    {
        if (outer == null)
            yield break;

        foreach (var outerItem in outer)
        {
            var inner = innerSelector(outerItem);
            if (inner == null)
                continue;

            foreach (var innerItem in inner)
                yield return (outerItem, innerItem);
        }
    }
}
