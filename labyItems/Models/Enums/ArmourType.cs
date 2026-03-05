namespace labyItems.Models.Enums;

public enum ArmourType
{
    PAC,
    DAC,
    MAC,
    SAC,
    NAC
}

public static class ArmourTypeParser
{
    public static ArmourType? ParseOrNull(string? raw)
    {
        var token = (raw ?? string.Empty).Trim();
        if (token.Length == 0)
            return null;

        return Enum.TryParse<ArmourType>(token, ignoreCase: true, out var parsed)
            ? parsed
            : null;
    }

    public static string NormalizeOrEmpty(string? raw)
        => ParseOrNull(raw)?.ToString() ?? string.Empty;
}
