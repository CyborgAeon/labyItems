namespace labyItems.Models.Enums;

public enum DamType
{
    Missile,
    Blast,
    Body,
    Head,
    Chest,
    Abdomen,
    Worst
}

public static class DamTypeParser
{
    public static DamType? ParseOrNull(string? raw)
    {
        var token = (raw ?? string.Empty).Trim();
        if (token.Length == 0)
            return null;

        return Enum.TryParse<DamType>(token, ignoreCase: true, out var parsed)
            ? parsed
            : null;
    }

    public static string NormalizeOrFallback(string? raw, string fallback = "Missile")
    {
        var parsed = ParseOrNull(raw);
        if (parsed.HasValue)
            return parsed.Value.ToString();

        var normalizedFallback = ParseOrNull(fallback);
        return normalizedFallback?.ToString() ?? "Missile";
    }
}
