namespace labyItems.Helpers;

public static class ScrollCribSheetCatalog
{
    public static IReadOnlyList<ScrollCribSheetEntry> GetEntries(ScrollLanguage language)
        => language switch
        {
            ScrollLanguage.ManaGlyphs => MagicEntries,
            ScrollLanguage.SpiritRunes => SpiritEntries,
            ScrollLanguage.Ogham => OghamEntries,
            _ => Array.Empty<ScrollCribSheetEntry>()
        };

    public static string GetTitle(ScrollLanguage language)
        => language switch
        {
            ScrollLanguage.ManaGlyphs => "Magic Crib Sheet",
            ScrollLanguage.SpiritRunes => "Spirit Crib Sheet",
            ScrollLanguage.Ogham => "Ogham Crib Sheet",
            _ => "Crib Sheet"
        };

    public static string GetNote(ScrollLanguage language)
        => language == ScrollLanguage.ManaGlyphs
            ? "Vowels may also be shown as dots when they follow a consonant. This is optional and is still considered official by the system."
            : string.Empty;

    private static readonly IReadOnlyList<ScrollCribSheetEntry> MagicEntries = BuildMagicEntries();
    private static readonly IReadOnlyList<ScrollCribSheetEntry> SpiritEntries = BuildSpiritEntries();
    private static readonly IReadOnlyList<ScrollCribSheetEntry> OghamEntries = BuildOghamEntries();

    private static IReadOnlyList<ScrollCribSheetEntry> BuildMagicEntries()
    {
        var entries = BuildAlphabetEntries();
        entries.Add(new ScrollCribSheetEntry(ScrollTextFormatter.MapCribGlyphToken("th", ScrollLanguage.ManaGlyphs), "th"));
        return entries;
    }

    private static IReadOnlyList<ScrollCribSheetEntry> BuildSpiritEntries()
    {
        var entries = BuildAlphabetEntries();
        entries.Add(new ScrollCribSheetEntry(ScrollTextFormatter.MapCribGlyphToken("th", ScrollLanguage.SpiritRunes), "th"));
        entries.Add(new ScrollCribSheetEntry(ScrollTextFormatter.MapCribGlyphToken("ng", ScrollLanguage.SpiritRunes), "ng"));
        entries.Add(new ScrollCribSheetEntry(ScrollTextFormatter.MapCribGlyphToken("ch", ScrollLanguage.SpiritRunes), "ch"));

        for (var i = 0; i <= 9; i++)
            entries.Add(new ScrollCribSheetEntry(i.ToString(), i.ToString()));

        entries.Add(new ScrollCribSheetEntry(":", "full stop (.)"));
        return entries;
    }

    private static IReadOnlyList<ScrollCribSheetEntry> BuildOghamEntries()
        => BuildAlphabetEntries();

    private static List<ScrollCribSheetEntry> BuildAlphabetEntries()
    {
        var entries = new List<ScrollCribSheetEntry>();
        for (var ch = 'a'; ch <= 'z'; ch++)
            entries.Add(new ScrollCribSheetEntry(ch.ToString(), ch.ToString()));

        return entries;
    }
}

public sealed record ScrollCribSheetEntry(string GlyphText, string Translation);
