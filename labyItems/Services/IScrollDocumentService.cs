using labyItems.Helpers;

namespace labyItems.Services;

public interface IScrollDocumentService
{
    Task<ScrollPdfResult> CreatePdfAsync(ScrollPdfRequest request, CancellationToken cancellationToken = default);
    Task<ScrollPdfResult> CreateCribSheetPdfAsync(ScrollCribSheetPdfRequest request, CancellationToken cancellationToken = default);
}

public sealed record ScrollPdfRequest(
    string Text,
    ScrollLanguage Language,
    ManaGlyphVariant ManaGlyphVariant,
    string DocumentName);

public sealed record ScrollPdfResult(
    string Path,
    int PageCount);

public sealed record ScrollCribSheetPdfRequest(
    ScrollLanguage Language,
    ManaGlyphVariant ManaGlyphVariant,
    string DocumentName,
    string Title,
    IReadOnlyList<ScrollCribSheetEntry> Entries,
    string? Note);
