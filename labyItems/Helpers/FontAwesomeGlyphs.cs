namespace labyItems.Helpers;

public static class FontAwesomeGlyphs
{
    public const string SolidFamily = "FASolid";
    public const string InfoCircle = "\uf05a";

    public static string GetSphereIcon(string? sphere)
    {
        var token = NormalizeSphereToken(sphere);
        return token switch
        {
            "dismissal" => "\uf05e",        // ban
            "healing" => "\uf004",          // heart
            "universal" => "\uf0ac",        // globe
            "warding" => "\uf132",          // shield
            "causing" => "\uf1e2",          // bomb
            "dominion" => "\uf0e3",         // gavel
            "balance" => "\uf24e",          // balance scale
            "benediction" => "\uf005",      // star
            "communication" => "\uf086",    // comments
            "control" => "\uf1de",          // sliders
            "force" => "\uf0e7",            // bolt
            "necromancy" => "\uf54c",       // skull
            "shamanic" => "\uf06c",         // leaf
            "wayfinder" => "\uf14e",        // compass
            "goodlywitchcraft" => "\uf185", // sun
            "evilwitchcraft" => "\uf186",   // moon
            _ => "\uf059",                  // question-circle
        };
    }

    private static string NormalizeSphereToken(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var chars = raw
            .Trim()
            .Where(char.IsLetterOrDigit)
            .ToArray();

        return new string(chars).ToLowerInvariant();
    }
}
