namespace labyItems.Services;

public interface IBattleboardDocumentService
{
    Task<string> ConvertExcelToPdfAsync(string excelPath, string? outputFileName = null, CancellationToken ct = default);
}
