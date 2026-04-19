using System.Text;
using ClosedXML.Excel;
using PdfSharpCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using labyItems.Helpers;

namespace labyItems.Services;

public sealed class BattleboardDocumentService : IBattleboardDocumentService
{
    private const double PageMargin = 20;
    private const double HeaderHeight = 18;
    private const double RowHeight = 13;

    public async Task<string> ConvertExcelToPdfAsync(string excelPath, string? outputFileName = null, CancellationToken ct = default)
    {
        try
        {
            var inputPath = (excelPath ?? string.Empty).Trim();
            if (inputPath.Length == 0)
                throw new ArgumentException("Excel path is required.", nameof(excelPath));

            if (!File.Exists(inputPath))
                throw new FileNotFoundException("Battleboard spreadsheet not found.", inputPath);

            var outputName = BuildOutputFileName(inputPath, outputFileName);
            var outputPath = Path.Combine(FileSystem.CacheDirectory, outputName);

            await Task.Run(() => ConvertInternal(inputPath, outputPath), ct).ConfigureAwait(false);
            return outputPath;
        }
        catch (Exception ex)
        {
            RuntimeLog.Write("BATTLEBOARD_PDF", "Battleboard PDF conversion failed.", ex);
            throw;
        }
    }

    private static void ConvertInternal(string inputPath, string outputPath)
    {
        PdfSharpFontResolverBootstrapper.EnsureInitialized();

        using var workbook = new XLWorkbook(inputPath);
        using var document = new PdfDocument();
        document.Version = 14;
        document.Info.Title = Path.GetFileNameWithoutExtension(outputPath);

        var headerFont = new XFont(PdfSharpFontResolverBootstrapper.GetStandardFaceName("LibreCaslonTextBold", isBold: true), 9, XFontStyle.Bold);
        var cellFont = new XFont(PdfSharpFontResolverBootstrapper.GetStandardFaceName("LibreCaslonTextRegular"), 6, XFontStyle.Regular);
        var borderPen = new XPen(XColor.FromArgb(220, 220, 220), 0.4);

        foreach (var worksheet in workbook.Worksheets)
        {
            var used = worksheet.RangeUsed(XLCellsUsedOptions.AllContents);
            if (used == null)
                continue;

            var firstRow = used.RangeAddress.FirstAddress.RowNumber;
            var lastRow = used.RangeAddress.LastAddress.RowNumber;
            var firstColumn = used.RangeAddress.FirstAddress.ColumnNumber;
            var lastColumn = used.RangeAddress.LastAddress.ColumnNumber;

            if (firstRow > lastRow || firstColumn > lastColumn)
                continue;

            var columnCount = Math.Max(1, lastColumn - firstColumn + 1);
            var width = 842 - (PageMargin * 2);   // A4 landscape width in points
            var height = 595 - (PageMargin * 2) - HeaderHeight; // A4 landscape height in points

            var columnWidth = width / columnCount;
            var rowsPerPage = Math.Max(1, (int)Math.Floor(height / RowHeight));

            for (var rowStart = firstRow; rowStart <= lastRow; rowStart += rowsPerPage)
            {
                var rowEnd = Math.Min(lastRow, rowStart + rowsPerPage - 1);
                var page = document.AddPage();
                page.Size = PageSize.A4;
                page.Orientation = PageOrientation.Landscape;

                using var gfx = XGraphics.FromPdfPage(page);
                gfx.DrawString(
                    $"{worksheet.Name} (rows {rowStart}-{rowEnd})",
                    headerFont,
                    XBrushes.Black,
                    new XRect(PageMargin, PageMargin, page.Width - (PageMargin * 2), HeaderHeight),
                    XStringFormats.TopLeft);

                for (var row = rowStart; row <= rowEnd; row++)
                {
                    var y = PageMargin + HeaderHeight + ((row - rowStart) * RowHeight);
                    for (var col = firstColumn; col <= lastColumn; col++)
                    {
                        var x = PageMargin + ((col - firstColumn) * columnWidth);
                        var rect = new XRect(x, y, columnWidth, RowHeight);

                        gfx.DrawRectangle(borderPen, rect);

                        var text = NormalizeCellText(worksheet.Cell(row, col).GetFormattedString());
                        if (text.Length == 0)
                            continue;

                        var clipped = ClipText(text, columnWidth);
                        if (clipped.Length == 0)
                            continue;

                        gfx.DrawString(
                            clipped,
                            cellFont,
                            XBrushes.Black,
                            new XRect(x + 1, y + 1, Math.Max(0, columnWidth - 2), Math.Max(0, RowHeight - 2)),
                            XStringFormats.TopLeft);
                    }
                }
            }
        }

        document.Save(outputPath);
        ValidatePdfFile(outputPath);
    }

    private static string BuildOutputFileName(string inputPath, string? requestedOutputFileName)
    {
        var requested = (requestedOutputFileName ?? string.Empty).Trim();
        if (requested.Length == 0)
            requested = Path.GetFileNameWithoutExtension(inputPath);

        requested = SanitizeFileName(requested);
        if (requested.Length == 0)
            requested = "Battleboard";

        if (!requested.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            requested += ".pdf";

        return requested;
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var buffer = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (invalid.Contains(ch))
                continue;

            buffer.Append(ch);
        }

        return buffer.ToString().Trim();
    }

    private static string NormalizeCellText(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
            return string.Empty;

        text = System.Text.RegularExpressions.Regex.Replace(text, "\\s+", " ");
        return text;
    }

    private static string ClipText(string text, double columnWidth)
    {
        if (text.Length == 0 || columnWidth <= 3)
            return string.Empty;

        var maxChars = Math.Max(1, (int)Math.Floor((columnWidth - 2) / 3.2));
        if (text.Length <= maxChars)
            return text;

        if (maxChars <= 1)
            return text[..1];

        if (maxChars <= 3)
            return "...";

        return text[..(maxChars - 3)] + "...";
    }
    private static void ValidatePdfFile(string outputPath)
    {
        using var stream = File.OpenRead(outputPath);
        if (stream.Length < 8)
            throw new InvalidOperationException("Generated PDF is unexpectedly small.");

        Span<byte> header = stackalloc byte[5];
        var read = stream.Read(header);
        if (read != 5
            || header[0] != (byte)'%'
            || header[1] != (byte)'P'
            || header[2] != (byte)'D'
            || header[3] != (byte)'F'
            || header[4] != (byte)'-')
        {
            throw new InvalidOperationException("Generated output is not a valid PDF header.");
        }

        stream.Seek(Math.Max(0, stream.Length - 2048), SeekOrigin.Begin);
        using var reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, leaveOpen: false);
        var tail = reader.ReadToEnd();
        if (!tail.Contains("%%EOF", StringComparison.Ordinal))
            throw new InvalidOperationException("Generated PDF is missing EOF marker.");
    }
}
