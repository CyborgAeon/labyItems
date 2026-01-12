using System.Collections.Generic;

namespace labyItems.Models;

public static class SupernaturalTypes
{
    public const string Magic = "🪄 Magic";
    public const string Spirit = "⽰ Spirit";
    public const string Mantic = "🍥 Mantic";

    public static readonly IReadOnlyList<string> All = new[] { Magic, Spirit, Mantic };
}
