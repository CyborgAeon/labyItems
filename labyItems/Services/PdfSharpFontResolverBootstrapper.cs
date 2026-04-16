using System.Collections.Concurrent;
using labyItems.Helpers;
using PdfSharpCore.Fonts;

namespace labyItems.Services;

public static class PdfSharpFontResolverBootstrapper
{
    private static readonly object Sync = new();
    private static bool _initialized;

    public static void EnsureInitialized()
    {
        if (_initialized)
            return;

        lock (Sync)
        {
            if (_initialized)
                return;

            RuntimeLog.Write("PDFSHARP_FONT", "EnsureInitialized start.");
            try
            {
                GlobalFontSettings.FontResolver = new AppPdfSharpFontResolver();
                RuntimeLog.Write("PDFSHARP_FONT", "Assigned AppPdfSharpFontResolver to GlobalFontSettings.FontResolver.");
            }
            catch (Exception ex)
            {
                RuntimeLog.Write("PDFSHARP_FONT", "Failed assigning AppPdfSharpFontResolver to GlobalFontSettings.FontResolver.", ex);
                throw;
            }

            _initialized = true;
            RuntimeLog.Write("PDFSHARP_FONT", "EnsureInitialized complete.");
        }
    }

    private sealed class AppPdfSharpFontResolver : IFontResolver
    {
        private const string ManaGlyphsAdvancedFace = "ManaGlyphsAdvancedPdf";
        private const string ManaGlyphsBasicFace = "ManaGlyphsBasicPdf";
        private const string SpiritRunesFace = "SpiritRunesPdf";
        private const string OghamFace = "OghamPdf";
        private const string OpenSansRegularFace = "OpenSansRegularPdf";
        private const string OpenSansSemiboldFace = "OpenSansSemiboldPdf";

        private readonly ConcurrentDictionary<string, byte[]> _fontBytes = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, FontResolverInfo> _cache = new(StringComparer.OrdinalIgnoreCase);

        public string DefaultFontName => OpenSansRegularFace;

        public byte[]? GetFont(string faceName)
        {
            var fileName = MapFaceNameToFile(faceName);
            if (fileName == null)
            {
                RuntimeLog.Write("PDFSHARP_FONT", $"GetFont requested unknown face '{faceName}'.");
                return null;
            }

            return _fontBytes.GetOrAdd(faceName, _ =>
            {
                RuntimeLog.Write("PDFSHARP_FONT", $"Loading font bytes for face '{faceName}' from '{fileName}'.");
                return ReadFont(fileName);
            });
        }

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            var key = $"{familyName}|{isBold}|{isItalic}";
            var mapped = MapTypeface(familyName, isBold, isItalic);
            RuntimeLog.Write("PDFSHARP_FONT", $"ResolveTypeface family='{familyName}', bold={isBold}, italic={isItalic} => '{mapped}'.");
            return _cache.GetOrAdd(key, _ => new FontResolverInfo(mapped));
        }

        public static string GetFaceName(string familyName, bool isBold = false)
            => MapTypeface(familyName, isBold, isItalic: false);

        private static string MapTypeface(string familyName, bool isBold, bool isItalic)
        {
            var name = (familyName ?? string.Empty).Trim();
            if (name.Equals("ManaGlyphsAdvanced", StringComparison.OrdinalIgnoreCase)
                || name.Equals("ManaGlyphsPdf", StringComparison.OrdinalIgnoreCase))
            {
                return ManaGlyphsAdvancedFace;
            }

            if (name.Equals("ManaGlyphsBasic", StringComparison.OrdinalIgnoreCase))
                return ManaGlyphsBasicFace;

            if (name.Equals("SpiritRunes", StringComparison.OrdinalIgnoreCase))
                return SpiritRunesFace;

            if (name.Equals("OghamFont", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Ogham", StringComparison.OrdinalIgnoreCase))
            {
                return OghamFace;
            }

            if (name.Equals("OpenSansSemibold", StringComparison.OrdinalIgnoreCase))
                return OpenSansSemiboldFace;

            if (name.Equals("Helvetica", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Arial", StringComparison.OrdinalIgnoreCase)
                || name.Equals("OpenSansRegular", StringComparison.OrdinalIgnoreCase)
                || name.Equals("OpenSans", StringComparison.OrdinalIgnoreCase))
            {
                return isBold || isItalic ? OpenSansSemiboldFace : OpenSansRegularFace;
            }

            return isBold || isItalic ? OpenSansSemiboldFace : OpenSansRegularFace;
        }

        private static byte[] ReadFont(string fileName)
        {
            try
            {
                using var stream = Microsoft.Maui.Storage.FileSystem
                    .OpenAppPackageFileAsync(fileName)
                    .GetAwaiter()
                    .GetResult();
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                RuntimeLog.Write("PDFSHARP_FONT", $"Loaded font asset '{fileName}' ({buffer.Length} bytes).");
                return buffer.ToArray();
            }
            catch (Exception ex)
            {
                RuntimeLog.Write("PDFSHARP_FONT", $"Failed reading font asset '{fileName}'.", ex);
                throw;
            }
        }

        private static string? MapFaceNameToFile(string faceName)
            => faceName switch
            {
                ManaGlyphsAdvancedFace => "Mana_Glyphs_smart_v2.ttf",
                ManaGlyphsBasicFace => "Mana_Glyphs_handwritten_style.ttf",
                SpiritRunesFace => "Spirit_Runes.ttf",
                OghamFace => "Ogham.ttf",
                OpenSansRegularFace => "OpenSans-Regular.ttf",
                OpenSansSemiboldFace => "OpenSans-Semibold.ttf",
                _ => null
            };
    }

    public static string GetScrollFaceName(labyItems.Helpers.ScrollLanguage language, labyItems.Helpers.ManaGlyphVariant manaGlyphVariant)
        => language switch
        {
            labyItems.Helpers.ScrollLanguage.ManaGlyphs => manaGlyphVariant == labyItems.Helpers.ManaGlyphVariant.Basic
                ? AppPdfSharpFontResolver.GetFaceName("ManaGlyphsBasic")
                : AppPdfSharpFontResolver.GetFaceName("ManaGlyphsAdvanced"),
            labyItems.Helpers.ScrollLanguage.SpiritRunes => AppPdfSharpFontResolver.GetFaceName("SpiritRunes"),
            labyItems.Helpers.ScrollLanguage.Ogham => AppPdfSharpFontResolver.GetFaceName("OghamFont"),
            _ => AppPdfSharpFontResolver.GetFaceName("OpenSansRegular")
        };

    public static string GetStandardFaceName(string familyName, bool isBold = false)
        => AppPdfSharpFontResolver.GetFaceName(familyName, isBold);
}
