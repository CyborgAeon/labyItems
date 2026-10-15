namespace labyItems.Services;

/// <summary>
/// Case-insensitive directory of source-book names and their packaged PDF assets.
/// Keep PDF paths in sync with Resources/Raw.
/// </summary>
public static class BookDirectory
{
    private static readonly IReadOnlyDictionary<string, string> Documents = BuildDocuments();

    public static IEnumerable<string> Names => Documents.Keys;
    public static IEnumerable<string> AssetPaths => Documents.Values.Distinct(StringComparer.OrdinalIgnoreCase);

    public static bool TryResolve(string? bookName, out string assetPath)
    {
        var normalized = (bookName ?? string.Empty).Trim();
        return Documents.TryGetValue(normalized, out assetPath!);
    }

    private static IReadOnlyDictionary<string, string> BuildDocuments()
    {
        var documents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        Add(documents, "Engarde/Engarde.pdf", "Engarde");
        Add(documents, "Manufacturers_guide/ISP-Tables.pdf", "ISP Tables");
        Add(documents, "Manufacturers_guide/The-Manufacturers-Guide.pdf", "Manufacturers Guide", "The Manufacturers Guide");
        Add(documents, "at_the_sharp_end/At-The-Sharp-End.pdf", "At the Sharp End");
        Add(documents, "druids_way/Druid's_way.pdf", "Druid's Way", "Druids Way");
        Add(documents, "evolution_classes/Classes.pdf", "Classes", "evolution-classes");
        Add(documents, "grimoire/Wizard-Grimoire.pdf", "Advanced Grimoire", "Wizard Grimoire", "Wizard's Grimoire");
        Add(documents, "o-insight/Oraculum-Insight.pdf", "Oraculum Insight");
        Add(documents, "people/Almanac.pdf", "Almanac");
        Add(documents, "people/Collated.pdf", "Collated", "Collated.pdf");
        Add(documents, "people/Races.pdf", "Races");
        Add(documents, "words_from_above/Divine_Guidance.pdf", "Divine Guidance");
        Add(documents, "words_from_above/Words-From-above.pdf", "Words From Above");

        return documents;
    }

    private static void Add(Dictionary<string, string> documents, string assetPath, params string[] names)
    {
        foreach (var name in names)
            documents.Add(name, assetPath);
    }
}
