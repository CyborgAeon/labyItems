using System.Collections.Generic;

namespace labyItems.Models;

public static class SupernaturalTypes
{
    public const string Magic = "🪄 Magic";
    public const string Spirit = "⽰ Spirit";
    public const string Mantic = "🍥 Mantic";
    public const string Earthpower = "🌍 Earthpower";
    public const string PureMagic = "🪄 Pure Magic";
    public const string PureSpirit = "⽰ Pure Spirit";

    public static readonly IReadOnlyList<string> All = new[] { Magic, Spirit, Mantic, Earthpower };
    public static readonly IReadOnlyList<string> PureWeaponOptions = new[] { PureMagic, PureSpirit, Mantic };
}
