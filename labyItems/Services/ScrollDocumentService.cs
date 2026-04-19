using System.Text;
using labyItems.Helpers;
#if ANDROID
using AndroidBitmap = Android.Graphics.Bitmap;
using AndroidCanvas = Android.Graphics.Canvas;
using AndroidColor = Android.Graphics.Color;
using AndroidPaint = Android.Graphics.Paint;
using AndroidPaintFlags = Android.Graphics.PaintFlags;
using AndroidPdfDocument = Android.Graphics.Pdf.PdfDocument;
using AndroidTypeface = Android.Graphics.Typeface;
#endif
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace labyItems.Services;

public sealed class ScrollDocumentService : IScrollDocumentService
{
    private const double Margin = 42;
    private const double FontSize = 24;
    private const double ParagraphSpacing = 8;
    private const double CribHeaderFontSize = 18;
    private const double CribGlyphFontSize = 20;
    private const double CribTextFontSize = 11;
    private static readonly XPdfFontOptions EmbeddedUnicodeFontOptions =
        new(PdfFontEncoding.Unicode);

    public async Task<ScrollPdfResult> CreatePdfAsync(ScrollPdfRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimeLog.Write("SCROLL_PDF", $"CreatePdfAsync start. Document='{request.DocumentName}', language={request.Language}, manaVariant={request.ManaGlyphVariant}.");
#if ANDROID
        return await CreateAndroidScrollPdfAsync(request, cancellationToken).ConfigureAwait(false);
#else
        PdfSharpFontResolverBootstrapper.EnsureInitialized();

        var text = ScrollTextFormatter.NormalizeMultilineText(request.Text);
        if (text.Length == 0)
            throw new InvalidOperationException("Scroll text is required.");

        var outputPath = Path.Combine(
            Microsoft.Maui.Storage.FileSystem.CacheDirectory,
            BuildFileName(request.DocumentName, request.Language));
        RuntimeLog.Write("SCROLL_PDF", $"Normalized text length={text.Length}. Output path='{outputPath}'.");

        try
        {
            var pageCount = await Task.Run(
                () => CreatePdfInternal(outputPath, text, request.Language, request.ManaGlyphVariant, request.DocumentName),
                cancellationToken).ConfigureAwait(false);

            RuntimeLog.Write("SCROLL_PDF", $"CreatePdfAsync complete. Pages={pageCount}, path='{outputPath}'.");
            return new ScrollPdfResult(outputPath, pageCount);
        }
        catch (Exception ex)
        {
            RuntimeLog.Write("SCROLL_PDF", "CreatePdfAsync failed.", ex);
            throw;
        }
#endif
    }

    public async Task<ScrollPdfResult> CreateCribSheetPdfAsync(ScrollCribSheetPdfRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimeLog.Write("SCROLL_PDF", $"CreateCribSheetPdfAsync start. Document='{request.DocumentName}', language={request.Language}, manaVariant={request.ManaGlyphVariant}.");
#if ANDROID
        return await CreateAndroidCribSheetPdfAsync(request, cancellationToken).ConfigureAwait(false);
#else
        PdfSharpFontResolverBootstrapper.EnsureInitialized();

        var outputPath = Path.Combine(
            Microsoft.Maui.Storage.FileSystem.CacheDirectory,
            BuildFileName(request.DocumentName, request.Language));

        try
        {
            var pageCount = await Task.Run(
                () => CreateCribSheetPdfInternal(outputPath, request),
                cancellationToken).ConfigureAwait(false);

            RuntimeLog.Write("SCROLL_PDF", $"CreateCribSheetPdfAsync complete. Pages={pageCount}, path='{outputPath}'.");
            return new ScrollPdfResult(outputPath, pageCount);
        }
        catch (Exception ex)
        {
            RuntimeLog.Write("SCROLL_PDF", "CreateCribSheetPdfAsync failed.", ex);
            throw;
        }
#endif
    }

#if ANDROID
    private static async Task<ScrollPdfResult> CreateAndroidScrollPdfAsync(ScrollPdfRequest request, CancellationToken cancellationToken)
    {
        var text = ScrollTextFormatter.NormalizeMultilineText(request.Text);
        if (text.Length == 0)
            throw new InvalidOperationException("Scroll text is required.");

        var outputPath = System.IO.Path.Combine(
            Microsoft.Maui.Storage.FileSystem.CacheDirectory,
            BuildFileName(request.DocumentName, request.Language));

        var typeface = await LoadAndroidTypefaceAsync(request.Language, request.ManaGlyphVariant).ConfigureAwait(false);
        var pageCount = await Task.Run(() => CreateAndroidTextPdfInternal(outputPath, text, request.DocumentName, typeface), cancellationToken).ConfigureAwait(false);
        return new ScrollPdfResult(outputPath, pageCount);
    }

    private static async Task<ScrollPdfResult> CreateAndroidCribSheetPdfAsync(ScrollCribSheetPdfRequest request, CancellationToken cancellationToken)
    {
        var outputPath = System.IO.Path.Combine(
            Microsoft.Maui.Storage.FileSystem.CacheDirectory,
            BuildFileName(request.DocumentName, request.Language));

        var typeface = await LoadAndroidTypefaceAsync(request.Language, request.ManaGlyphVariant).ConfigureAwait(false);
        var pageCount = await Task.Run(() => CreateAndroidCribPdfInternal(outputPath, request, typeface), cancellationToken).ConfigureAwait(false);
        return new ScrollPdfResult(outputPath, pageCount);
    }

    private static int CreateAndroidTextPdfInternal(string outputPath, string text, string documentName, AndroidTypeface typeface)
    {
        using var document = new AndroidPdfDocument();
        var pageInfo = new AndroidPdfDocument.PageInfo.Builder(595, 842, 1).Create();
        using var page = document.StartPage(pageInfo);
        using var bitmap = AndroidBitmap.CreateBitmap(pageInfo.PageWidth, pageInfo.PageHeight, AndroidBitmap.Config.Argb8888!);
        using var bitmapCanvas = new AndroidCanvas(bitmap);
        bitmapCanvas.DrawColor(AndroidColor.White);
        using var glyphPaint = new AndroidPaint(AndroidPaintFlags.AntiAlias)
        {
            Color = AndroidColor.Black,
            TextAlign = AndroidPaint.Align.Center,
            TextSize = (float)FontSize
        };
        glyphPaint.SetTypeface(typeface);

        var contentWidth = pageInfo.PageWidth - ((float)Margin * 2f);
        var centerX = pageInfo.PageWidth / 2f;
        var lineHeight = glyphPaint.FontSpacing * 1.2f;
        var y = (float)Margin + lineHeight;

        foreach (var paragraph in text.Split('\n'))
        {
            var lines = WrapParagraph(paragraph, word => glyphPaint.MeasureText(word), contentWidth);
            if (lines.Count == 0)
                lines.Add(string.Empty);

            foreach (var line in lines)
            {
                bitmapCanvas.DrawText(line, centerX, y, glyphPaint);
                y += lineHeight;
            }

            y += (float)ParagraphSpacing;
        }

        page.Canvas.DrawBitmap(bitmap, 0, 0, null);
        document.FinishPage(page);
        using var stream = File.Create(outputPath);
        document.WriteTo(stream);
        return 1;
    }

    private static int CreateAndroidCribPdfInternal(string outputPath, ScrollCribSheetPdfRequest request, AndroidTypeface glyphTypeface)
    {
        using var document = new AndroidPdfDocument();
        var pageInfo = new AndroidPdfDocument.PageInfo.Builder(595, 842, 1).Create();
        using var page = document.StartPage(pageInfo);
        using var bitmap = AndroidBitmap.CreateBitmap(pageInfo.PageWidth, pageInfo.PageHeight, AndroidBitmap.Config.Argb8888!);
        using var bitmapCanvas = new AndroidCanvas(bitmap);
        bitmapCanvas.DrawColor(AndroidColor.White);

        using var headerPaint = new AndroidPaint(AndroidPaintFlags.AntiAlias)
        {
            Color = AndroidColor.Black,
            TextAlign = AndroidPaint.Align.Center,
            TextSize = (float)CribHeaderFontSize
        };
        using var glyphPaint = new AndroidPaint(AndroidPaintFlags.AntiAlias)
        {
            Color = AndroidColor.Black,
            TextAlign = AndroidPaint.Align.Center,
            TextSize = (float)CribGlyphFontSize
        };
        glyphPaint.SetTypeface(glyphTypeface);
        using var textPaint = new AndroidPaint(AndroidPaintFlags.AntiAlias)
        {
            Color = AndroidColor.Black,
            TextAlign = AndroidPaint.Align.Left,
            TextSize = (float)CribTextFontSize
        };
        using var borderPaint = new AndroidPaint
        {
            Color = AndroidColor.Rgb(220, 220, 220),
            StrokeWidth = 1f
        };
        borderPaint.SetStyle(AndroidPaint.Style.Stroke);

        var y = (float)Margin;
        var contentWidth = pageInfo.PageWidth - ((float)Margin * 2f);
        bitmapCanvas.DrawText(request.Title, pageInfo.PageWidth / 2f, y + headerPaint.TextSize, headerPaint);
        y += 34f;

        var columnSpacing = 12f;
        var columnWidth = (contentWidth - (columnSpacing * 2f)) / 3f;
        var cellHeight = 30f;
        var labelWidth = Math.Min(52f, columnWidth * 0.42f);

        for (var i = 0; i < request.Entries.Count; i++)
        {
            var entry = request.Entries[i];
            var column = i % 3;
            var row = i / 3;
            var x = (float)Margin + (column * (columnWidth + columnSpacing));
            var cellY = y + (row * cellHeight);

            bitmapCanvas.DrawRect(x, cellY, x + columnWidth, cellY + cellHeight, borderPaint);
            bitmapCanvas.DrawText(entry.GlyphText, x + (labelWidth / 2f) + 6f, cellY + 21f, glyphPaint);
            bitmapCanvas.DrawText(entry.Translation, x + labelWidth + 10f, cellY + 19f, textPaint);
        }

        var totalRows = (int)Math.Ceiling(request.Entries.Count / 3d);
        y += totalRows * cellHeight + 18f;

        var note = ScrollTextFormatter.NormalizeMultilineText(request.Note);
        if (note.Length > 0)
        {
            var noteLines = WrapParagraph(note, word => textPaint.MeasureText(word), contentWidth);
            foreach (var line in noteLines)
            {
                bitmapCanvas.DrawText(line, (float)Margin, y + textPaint.TextSize, textPaint);
                y += 18f;
            }
        }

        page.Canvas.DrawBitmap(bitmap, 0, 0, null);
        document.FinishPage(page);
        using var stream = File.Create(outputPath);
        document.WriteTo(stream);
        return 1;
    }

    private static async Task<AndroidTypeface> LoadAndroidTypefaceAsync(ScrollLanguage language, ManaGlyphVariant manaGlyphVariant)
    {
        var fileName = language switch
        {
            ScrollLanguage.ManaGlyphs when manaGlyphVariant == ManaGlyphVariant.Basic => "Mana_Glyphs_handwritten_style.ttf",
            ScrollLanguage.ManaGlyphs => "Mana_Glyphs_smart_v2.ttf",
            ScrollLanguage.SpiritRunes => "Spirit_Runes.ttf",
            ScrollLanguage.Ogham => "Ogham.ttf",
            _ => "LibreCaslonText-Regular.ttf"
        };

        RuntimeLog.Write("SCROLL_PDF", $"Loading Android typeface '{fileName}' from assets.");
        await using var _ = await Microsoft.Maui.Storage.FileSystem.OpenAppPackageFileAsync(fileName).ConfigureAwait(false);
        return AndroidTypeface.CreateFromAsset(Android.App.Application.Context!.Assets!, fileName)
               ?? throw new InvalidOperationException($"Could not load Android typeface from '{fileName}'.");
    }

    private static List<string> WrapParagraph(string paragraph, Func<string, float> measure, double maxWidth)
    {
        var normalized = paragraph.TrimEnd();
        if (normalized.Length == 0)
            return new List<string>();

        var words = normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
            return new List<string>();

        var lines = new List<string>();
        var current = new StringBuilder();

        foreach (var word in words)
        {
            var candidate = current.Length == 0 ? word : $"{current} {word}";
            if (measure(candidate) <= maxWidth)
            {
                current.Clear();
                current.Append(candidate);
                continue;
            }

            if (current.Length > 0)
            {
                lines.Add(current.ToString());
                current.Clear();
            }

            current.Append(word);
        }

        if (current.Length > 0)
            lines.Add(current.ToString());

        return lines;
    }
#endif

    private static int CreatePdfInternal(
        string outputPath,
        string text,
        ScrollLanguage language,
        ManaGlyphVariant manaGlyphVariant,
        string documentName)
    {
        RuntimeLog.Write("SCROLL_PDF", $"CreatePdfInternal start. Document='{documentName}'.");
        using var document = new PdfDocument();
        document.Info.Title = documentName;
        document.Version = 14;

        var faceName = PdfSharpFontResolverBootstrapper.GetScrollFaceName(language, manaGlyphVariant);
        RuntimeLog.Write("SCROLL_PDF", $"Resolved face name '{faceName}'. Constructing XFont.");
        var font = new XFont(faceName, FontSize, XFontStyle.Regular, EmbeddedUnicodeFontOptions);
        RuntimeLog.Write("SCROLL_PDF", $"Constructed XFont '{faceName}' size={FontSize} with embedded Unicode font options.");
        var brush = XBrushes.Black;

        PdfPage? page = null;
        XGraphics? gfx = null;
        double y = 0;
        double lineHeight = 0;
        double contentWidth = 0;

        void StartPage()
        {
            RuntimeLog.Write("SCROLL_PDF", "Adding PDF page.");
            page = document.AddPage();
            page.Size = PdfSharpCore.PageSize.A4;
            page.Orientation = PdfSharpCore.PageOrientation.Portrait;
            RuntimeLog.Write("SCROLL_PDF", "Creating XGraphics from PDF page.");
            gfx = XGraphics.FromPdfPage(page);
            lineHeight = Math.Max(gfx.MeasureString("Ag", font).Height * 1.25, FontSize * 1.35);
            contentWidth = page.Width - (Margin * 2);
            y = Margin;
            RuntimeLog.Write("SCROLL_PDF", $"Page ready. lineHeight={lineHeight}, contentWidth={contentWidth}.");
        }

        StartPage();

        foreach (var paragraph in text.Split('\n'))
        {
            var lines = WrapParagraph(gfx!, font, paragraph, contentWidth);
            if (lines.Count == 0)
                lines.Add(string.Empty);

            foreach (var line in lines)
            {
                if (y + lineHeight > page!.Height - Margin)
                    StartPage();

                gfx!.DrawString(
                    line,
                    font,
                    brush,
                    new XRect(Margin, y, contentWidth, lineHeight),
                    XStringFormats.TopCenter);

                y += lineHeight;
            }

            y += ParagraphSpacing;
        }

        document.Save(outputPath);
        RuntimeLog.Write("SCROLL_PDF", $"PDF saved to '{outputPath}'. PageCount={document.PageCount}.");
        return document.PageCount;
    }

    private static int CreateCribSheetPdfInternal(string outputPath, ScrollCribSheetPdfRequest request)
    {
        using var document = new PdfDocument();
        document.Info.Title = request.DocumentName;
        document.Version = 14;

        var glyphFace = PdfSharpFontResolverBootstrapper.GetScrollFaceName(request.Language, request.ManaGlyphVariant);
        var glyphFont = new XFont(glyphFace, CribGlyphFontSize, XFontStyle.Regular, EmbeddedUnicodeFontOptions);
        var headerFont = new XFont(PdfSharpFontResolverBootstrapper.GetStandardFaceName("LibreCaslonTextBold", isBold: true), CribHeaderFontSize, XFontStyle.Bold, EmbeddedUnicodeFontOptions);
        var textFont = new XFont(PdfSharpFontResolverBootstrapper.GetStandardFaceName("LibreCaslonTextRegular"), CribTextFontSize, XFontStyle.Regular, EmbeddedUnicodeFontOptions);
        var glyphBrush = XBrushes.Black;
        var borderPen = new XPen(XColor.FromArgb(220, 220, 220), 0.5);

        var page = document.AddPage();
        page.Size = PdfSharpCore.PageSize.A4;
        page.Orientation = PdfSharpCore.PageOrientation.Portrait;

        using var gfx = XGraphics.FromPdfPage(page);
        var y = Margin;
        var contentWidth = page.Width - (Margin * 2);

        gfx.DrawString(request.Title, headerFont, glyphBrush, new XRect(Margin, y, contentWidth, 28), XStringFormats.TopCenter);
        y += 34;

        var columnSpacing = 12d;
        var columnWidth = (contentWidth - (columnSpacing * 2)) / 3d;
        var cellHeight = 30d;
        var labelWidth = Math.Min(52d, columnWidth * 0.42);

        for (var i = 0; i < request.Entries.Count; i++)
        {
            var entry = request.Entries[i];
            var column = i % 3;
            var row = i / 3;
            var x = Margin + (column * (columnWidth + columnSpacing));
            var cellY = y + (row * cellHeight);

            gfx.DrawRectangle(borderPen, new XRect(x, cellY, columnWidth, cellHeight));
            gfx.DrawString(entry.GlyphText, glyphFont, glyphBrush, new XRect(x + 6, cellY + 3, labelWidth, cellHeight - 6), XStringFormats.Center);
            gfx.DrawString(entry.Translation, textFont, glyphBrush, new XRect(x + labelWidth + 10, cellY + 3, columnWidth - labelWidth - 14, cellHeight - 6), XStringFormats.CenterLeft);
        }

        var totalRows = (int)Math.Ceiling(request.Entries.Count / 3d);
        y += totalRows * cellHeight + 18;

        var note = ScrollTextFormatter.NormalizeMultilineText(request.Note);
        if (note.Length > 0)
        {
            var noteLines = WrapParagraph(gfx, textFont, note, contentWidth);
            foreach (var line in noteLines)
            {
                gfx.DrawString(line, textFont, glyphBrush, new XRect(Margin, y, contentWidth, 18), XStringFormats.TopLeft);
                y += 18;
            }
        }

        document.Save(outputPath);
        return document.PageCount;
    }

    private static List<string> WrapParagraph(XGraphics gfx, XFont font, string paragraph, double maxWidth)
    {
        var normalized = paragraph.TrimEnd();
        if (normalized.Length == 0)
            return new List<string>();

        var words = normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
            return new List<string>();

        var lines = new List<string>();
        var current = new StringBuilder();

        foreach (var word in words)
        {
            var candidate = current.Length == 0
                ? word
                : $"{current} {word}";

            if (gfx.MeasureString(candidate, font).Width <= maxWidth)
            {
                current.Clear();
                current.Append(candidate);
                continue;
            }

            if (current.Length > 0)
            {
                lines.Add(current.ToString());
                current.Clear();
            }

            if (gfx.MeasureString(word, font).Width <= maxWidth)
            {
                current.Append(word);
                continue;
            }

            foreach (var chunk in BreakLongWord(gfx, font, word, maxWidth))
                lines.Add(chunk);
        }

        if (current.Length > 0)
            lines.Add(current.ToString());

        return lines;
    }

    private static IEnumerable<string> BreakLongWord(XGraphics gfx, XFont font, string word, double maxWidth)
    {
        var current = new StringBuilder();
        foreach (var ch in word)
        {
            var candidate = current.ToString() + ch;
            if (current.Length > 0 && gfx.MeasureString(candidate, font).Width > maxWidth)
            {
                yield return current.ToString();
                current.Clear();
            }

            current.Append(ch);
        }

        if (current.Length > 0)
            yield return current.ToString();
    }

    private static string BuildFileName(string documentName, ScrollLanguage language)
    {
        var baseName = (documentName ?? string.Empty).Trim();
        if (baseName.Length == 0)
            baseName = "scroll";

        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(baseName.Where(ch => !invalid.Contains(ch)).ToArray()).Trim();
        if (sanitized.Length == 0)
            sanitized = "scroll";

        return $"{sanitized}-{GetLanguageLabel(language)}.pdf";
    }

    public static string GetLanguageLabel(ScrollLanguage language)
        => language switch
        {
            ScrollLanguage.ManaGlyphs => "Mana Glyphs",
            ScrollLanguage.SpiritRunes => "Spirit Runes",
            ScrollLanguage.Ogham => "Ogham",
            _ => "Scroll"
        };
}
