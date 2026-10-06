namespace labyItems.Services;

public static class SourceBookPdfCatalog
{
    private static readonly IReadOnlyDictionary<string, string> Documents =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Advanced Grimoire"] = "grimoire/Wizard-Grimoire-16.3.pdf",
            ["At the Sharp End"] = "at_the_sharp_end/At-The-Sharp-End-16.3.pdf",
            ["Classes"] = "evolution_classes/Classes-16.3.pdf",
            ["Collated.pdf"] = "evolution_classes/Classes-16.3.pdf",
            ["Druid's Way"] = "druids_way/Druid-16.3.pdf",
            ["Druids Way"] = "druids_way/Druid-16.3.pdf",
            ["Engarde"] = "Engarde/Engarde.pdf",
            ["evolution-classes"] = "evolution_classes/Classes-16.3.pdf",
            ["ISP Tables"] = "Manufacturers_guide/ISP-Tables.pdf",
            ["Manufacturers Guide"] = "Manufacturers_guide/The-Manufacturers-Guide.pdf",
            ["Oraculum Insight"] = "o-insight/O-Insight-16.3.pdf",
            ["Races"] = "people/Races-16.3.pdf",
            ["Wizard Grimoire"] = "grimoire/Wizard-Grimoire-16.3.pdf",
            ["Words From Above"] = "words_from_above/Words-From-above-16.3.pdf",
            ["Divine Guidance"] = "words_from_above/Divine_Guidance.pdf"
        };

    public static bool TryResolve(string? sourceBookName, out string assetPath)
    {
        var normalized = (sourceBookName ?? string.Empty).Trim();
        return Documents.TryGetValue(normalized, out assetPath!);
    }
}
