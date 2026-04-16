namespace labyItems.Helpers;

public static class ScrollTextFormatter
{
    private const string SpiritThGlyph = "[";
    private const string SpiritNgGlyph = "}";
    private const string SpiritChGlyph = "-";

    public static bool CanUseAsScroll(ScrollSourceKind sourceKind)
        => sourceKind is ScrollSourceKind.Spell or ScrollSourceKind.Miracle;

    public static string BuildOutputText(
        string baseText,
        ScrollSourceKind sourceKind,
        bool asScroll,
        string? sourceName)
    {
        var normalizedBase = NormalizeMultilineText(baseText);
        if (!CanUseAsScroll(sourceKind) || !asScroll)
            return normalizedBase;

        var normalizedName = NormalizeMultilineText(sourceName);
        return string.IsNullOrWhiteSpace(normalizedName)
            ? normalizedBase
            : $"{normalizedBase}\nScroll do thy work\n{normalizedName}";
    }

    public static string NormalizeMultilineText(string? text)
    {
        var normalized = (text ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();

        return normalized;
    }

    public static string NormalizeForGlyphDisplay(string? text, ScrollLanguage language = ScrollLanguage.ManaGlyphs)
    {
        var normalized = NormalizeMultilineText(text);
        if (normalized.Length == 0)
            return string.Empty;

        var buffer = new System.Text.StringBuilder(normalized.Length);
        var previousWasSpace = false;

        foreach (var ch in normalized)
        {
            if (ch == '\n')
            {
                if (buffer.Length > 0 && buffer[^1] == ' ')
                    buffer.Length--;

                if (buffer.Length == 0 || buffer[^1] != '\n')
                    buffer.Append('\n');

                previousWasSpace = false;
                continue;
            }

            if (char.IsLetterOrDigit(ch))
            {
                buffer.Append(char.ToLowerInvariant(ch));
                previousWasSpace = false;
                continue;
            }

            if (char.IsWhiteSpace(ch))
            {
                if (!previousWasSpace)
                {
                    buffer.Append(' ');
                    previousWasSpace = true;
                }
            }
        }

        return ApplyLanguageSpecificMappings(buffer.ToString().Trim(), language);
    }

    public static string MapCribGlyphToken(string token, ScrollLanguage language)
    {
        var normalized = NormalizeMultilineText(token).ToLowerInvariant();
        return ApplyLanguageSpecificMappings(normalized, language);
    }

    private static string ApplyLanguageSpecificMappings(string text, ScrollLanguage language)
    {
        if (text.Length == 0)
            return string.Empty;

        return language switch
        {
            ScrollLanguage.SpiritRunes => text
                .Replace("ng", SpiritNgGlyph, StringComparison.Ordinal)
                .Replace("ch", SpiritChGlyph, StringComparison.Ordinal)
                .Replace("th", SpiritThGlyph, StringComparison.Ordinal),
            _ => text
        };
    }
}
