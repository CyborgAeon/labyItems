using Microsoft.Maui.Graphics;

namespace labyItems.Helpers;

public static class ClassBracketIconHelper
{
    private const string DefaultIcon = "\U0001F6E1\uFE0F";
    private const string DefaultCategory = "warrior";

    public static (string Icon, string Category, IReadOnlyList<string> Tags) ParseBrackets(IReadOnlyList<string>? brackets)
    {
        if (brackets == null || brackets.Count == 0)
            return (DefaultIcon, DefaultCategory, Array.Empty<string>());

        var parsed = new List<(string Icon, string Category, string Tag)>();
        foreach (var raw in brackets)
        {
            var entry = ParseBracketToken(raw);
            if (string.IsNullOrWhiteSpace(entry.Tag))
                continue;

            parsed.Add(entry);
        }

        if (parsed.Count == 0)
            return (DefaultIcon, DefaultCategory, Array.Empty<string>());

        var primary = parsed[0];
        var tags = parsed
            .Select(p => p.Tag)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return (primary.Icon, primary.Category, tags);
    }

    public static IReadOnlyList<string> BuildBracketLabels(IEnumerable<string>? brackets)
        => (brackets ?? Enumerable.Empty<string>())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(GetBracketLabel)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static string BuildBracketSubheading(IEnumerable<string>? brackets, string? fallbackCategory)
    {
        var labels = BuildBracketLabels(brackets);
        return labels.Count == 0
            ? (fallbackCategory ?? string.Empty).Trim()
            : string.Join(" / ", labels);
    }

    public static Color GetBracketColor(string? bracket)
    {
        var label = GetBracketLabel(bracket);

        if (label.Equals("Neuro", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb("#E9D5FF");
        if (label.Equals("Wizard", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb("#D8E2DC");
        if (label.Equals("Warrior", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb("#FEC5BB");
        if (label.Equals("Priest", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb("#FAE1DD");
        if (label.Equals("Druid", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb("#DED6CE");
        if (label.Equals("Scout", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb("#F5EBE0");

        return Color.FromArgb("#F3F4F6");
    }

    public static Color GetColorOrDefault(string? value, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        try
        {
            return Color.FromArgb(value.Trim());
        }
        catch
        {
            return fallback;
        }
    }

    public static string GetBracketGlyph(string? bracket)
    {
        var (glyph, label) = ParseBracketParts(bracket);
        if (!string.IsNullOrWhiteSpace(glyph))
            return glyph;

        if (label.Equals("Neuro", StringComparison.OrdinalIgnoreCase)) return "\U0001F9E0";
        if (label.Equals("Wizard", StringComparison.OrdinalIgnoreCase)) return "\U0001FA84";
        if (label.Equals("Warrior", StringComparison.OrdinalIgnoreCase)) return "\u2694\uFE0F";
        if (label.Equals("Priest", StringComparison.OrdinalIgnoreCase)) return "\u2728";
        if (label.Equals("Druid", StringComparison.OrdinalIgnoreCase)) return "\U0001F33F";
        if (label.Equals("Scout", StringComparison.OrdinalIgnoreCase)) return DefaultIcon;

        return "\u2754";
    }

    public static string GetBracketLabel(string? bracket)
    {
        var (_, label) = ParseBracketParts(bracket);
        return string.IsNullOrWhiteSpace(label) ? string.Empty : label;
    }

    public static (string Glyph, string Label) ParseBracketParts(string? bracket)
    {
        var text = (bracket ?? string.Empty).Trim();
        if (text.Length == 0)
            return (string.Empty, string.Empty);

        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1 && LooksLikeIcon(parts[0]))
            return (parts[0], string.Join(" ", parts.Skip(1)));

        return (string.Empty, text);
    }

    private static (string Icon, string Category, string Tag) ParseBracketToken(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return (DefaultIcon, DefaultCategory, string.Empty);

        var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return (DefaultIcon, DefaultCategory, string.Empty);

        if (parts.Length == 1)
        {
            var token = parts[0].Trim();
            return (DefaultIcon, token, token);
        }

        var icon = parts[0];
        var category = string.Join(" ", parts.Skip(1));
        var tag = $"{icon} {category}".Trim();

        return (string.IsNullOrWhiteSpace(icon) ? DefaultIcon : icon, category, tag);
    }

    private static bool LooksLikeIcon(string token)
    {
        foreach (var ch in token)
        {
            if (!char.IsLetterOrDigit(ch))
                return true;
        }

        return false;
    }
}
