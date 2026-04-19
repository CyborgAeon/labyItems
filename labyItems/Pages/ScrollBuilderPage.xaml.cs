using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using labyItems.Helpers;
using labyItems.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.Communication;

namespace labyItems.Pages;

public partial class ScrollBuilderPage : ContentPage
{
    private readonly ScrollBuilderVm _vm;
    private readonly IScrollDocumentService _scrollDocumentService;
    private readonly IExportService _exportService;
    private bool _loaded;

    public ScrollBuilderPage(
        IScrollDocumentService? scrollDocumentService = null,
        IExportService? exportService = null)
    {
        InitializeComponent();

        _scrollDocumentService = scrollDocumentService
            ?? ServiceHelper.ResolveService<IScrollDocumentService>()
            ?? new ScrollDocumentService();
        _exportService = exportService
            ?? ServiceHelper.ResolveService<IExportService>()
            ?? new ExportService(
                new MauiClipboardService(),
                new MauiLauncherService(),
                new MauiShareService());

        _vm = new ScrollBuilderVm();
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded)
            return;

        _loaded = true;
        try
        {
            await _vm.LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Scroll Builder", $"Failed to load the spell catalog: {ex.Message}", "OK");
        }
    }

    private async void OnPrintClicked(object sender, EventArgs e)
    {
        try
        {
            var draft = _vm.BuildDraft();
            RuntimeLog.Write("SCROLL_UI", $"Print clicked. Document='{draft.DocumentName}', language={draft.Language}, manaVariant={draft.ManaGlyphVariant}, isCribSheet={draft.IsCribSheet}.");

            var pdf = draft.IsCribSheet
                ? await _scrollDocumentService.CreateCribSheetPdfAsync(new ScrollCribSheetPdfRequest(
                    draft.Language,
                    draft.ManaGlyphVariant,
                    draft.DocumentName,
                    draft.CribSheetTitle ?? string.Empty,
                    draft.CribSheetEntries,
                    draft.CribSheetNote))
                : await _scrollDocumentService.CreatePdfAsync(
                    new ScrollPdfRequest(draft.Text, draft.Language, draft.ManaGlyphVariant, draft.DocumentName));

            var action = await DisplayActionSheet(
                "Print scroll",
                "Cancel",
                null,
                "Save",
                "Email to desk");

            if (string.Equals(action, "Save", StringComparison.Ordinal))
            {
                await _exportService.ShareFileAsync("Save scroll PDF", pdf.Path);
                return;
            }

            if (string.Equals(action, "Email to desk", StringComparison.Ordinal))
            {
                if (!Email.Default.IsComposeSupported)
                    throw new NotSupportedException("Email composition is not supported on this device.");

                var subject = $"{ScrollDocumentService.GetLanguageLabel(draft.Language)} print for player {pdf.PageCount}";
                var message = new EmailMessage
                {
                    To = new List<string> { "print@labyrinthe.co.uk" },
                    Subject = subject,
                    Body = string.Empty,
                    BodyFormat = EmailBodyFormat.PlainText,
                    Attachments = new List<EmailAttachment> { new(pdf.Path) }
                };

                await Email.Default.ComposeAsync(message);
            }
        }
        catch (Exception ex)
        {
            RuntimeLog.Write("SCROLL_UI", "Print failed from UI handler.", ex);
            await DisplayAlert("Print failed", ex.Message, "OK");
        }
    }

    private sealed class ScrollBuilderVm : INotifyPropertyChanged
    {
        private readonly List<ScrollCatalogEntryVm> _allEntries = new();
        private string _selectedMode = "Catalog";
        private string _searchText = string.Empty;
        private ScrollCatalogEntryVm? _selectedEntry;
        private string _selectedLanguageLabel = "Mana Glyphs";
        private string _selectedCribSheetLabel = "Magic";
        private string _customText = string.Empty;
        private bool _useAdvancedManaGlyphs = true;
        private bool _asScroll;
        private string _previewText = "Select a spell, miracle, or evocation to preview its verbal.";
        private string _previewFontFamily = "LibreCaslonTextRegular";

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<string> ModeOptions { get; } = new() { "Catalog", "Custom", "Crib Sheet" };
        public ObservableCollection<string> LanguageOptions { get; } = new() { "Mana Glyphs", "Spirit Runes", "Ogham" };
        public ObservableCollection<string> CribSheetOptions { get; } = new() { "Magic", "Spirit", "Ogham" };
        public ObservableCollection<ScrollCatalogEntryVm> FilteredEntries { get; } = new();
        public ObservableCollection<CribSheetPreviewEntryVm> CribSheetPreviewEntries { get; } = new();

        public string SelectedMode
        {
            get => _selectedMode;
            set
            {
                if (!Set(ref _selectedMode, value))
                    return;

                Raise(nameof(IsCatalogMode));
                Raise(nameof(IsCustomMode));
                Raise(nameof(IsCribSheetMode));
                Raise(nameof(ShowOptionsRow));
                Raise(nameof(ShowTextPreview));
                Raise(nameof(ShowCribSheetPreview));
                UpdateScrollAvailability();
                RefreshPreview();
            }
        }

        public bool IsCatalogMode => string.Equals(SelectedMode, "Catalog", StringComparison.OrdinalIgnoreCase);
        public bool IsCustomMode => string.Equals(SelectedMode, "Custom", StringComparison.OrdinalIgnoreCase);
        public bool IsCribSheetMode => string.Equals(SelectedMode, "Crib Sheet", StringComparison.OrdinalIgnoreCase);
        public bool ShowOptionsRow => IsManaGlyphToggleVisible || ShowAsScrollOption;
        public bool ShowAsScrollOption => !IsCribSheetMode;
        public bool ShowTextPreview => !IsCribSheetMode;
        public bool ShowCribSheetPreview => IsCribSheetMode;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (!Set(ref _searchText, value))
                    return;

                ApplyFilter();
            }
        }

        public ScrollCatalogEntryVm? SelectedEntry
        {
            get => _selectedEntry;
            set
            {
                if (!Set(ref _selectedEntry, value))
                    return;

                if (value != null)
                    SelectedLanguageLabel = value.LanguageLabel;

                UpdateScrollAvailability();
                Raise(nameof(IsManaGlyphToggleVisible));
                RefreshPreview();
            }
        }

        public string SelectedLanguageLabel
        {
            get => _selectedLanguageLabel;
            set
            {
                if (!Set(ref _selectedLanguageLabel, value))
                    return;

                Raise(nameof(IsManaGlyphToggleVisible));
                RefreshPreview();
            }
        }

        public string CustomText
        {
            get => _customText;
            set
            {
                if (!Set(ref _customText, value))
                    return;

                RefreshPreview();
            }
        }

        public string SelectedCribSheetLabel
        {
            get => _selectedCribSheetLabel;
            set
            {
                if (!Set(ref _selectedCribSheetLabel, value))
                    return;

                Raise(nameof(IsManaGlyphToggleVisible));
                RefreshPreview();
            }
        }

        public bool UseAdvancedManaGlyphs
        {
            get => _useAdvancedManaGlyphs;
            set
            {
                if (!Set(ref _useAdvancedManaGlyphs, value))
                    return;

                Raise(nameof(ManaGlyphToggleLabel));
                RefreshPreview();
            }
        }

        public bool AsScroll
        {
            get => _asScroll;
            set
            {
                if (!Set(ref _asScroll, value))
                    return;

                RefreshPreview();
            }
        }

        public bool IsManaGlyphToggleVisible => ResolveLanguage() == ScrollLanguage.ManaGlyphs;
        public string ManaGlyphToggleLabel => UseAdvancedManaGlyphs ? "Advanced" : "Basic";
        public bool CanUseAsScroll => ResolveSourceKind() is ScrollSourceKind.Spell or ScrollSourceKind.Miracle;
        public double AsScrollLabelOpacity => CanUseAsScroll ? 1.0 : 0.45;
        public string CribSheetNote => IsCribSheetMode ? ScrollCribSheetCatalog.GetNote(ResolveLanguage()) : string.Empty;
        public bool HasCribSheetNote => CribSheetNote.Length > 0;

        public string PreviewText
        {
            get => _previewText;
            private set => Set(ref _previewText, value);
        }

        public string PreviewFontFamily
        {
            get => _previewFontFamily;
            private set => Set(ref _previewFontFamily, value);
        }

        public async Task LoadAsync()
        {
            var spellsTask = SpellService.GetAllAsync();
            var miraclesTask = MiracleService.GetAllAsync();
            var evocationsTask = DruidEvocationService.GetAllAsync();

            await Task.WhenAll(spellsTask, miraclesTask, evocationsTask);

            _allEntries.Clear();
            _allEntries.AddRange(spellsTask.Result.Select(spell => new ScrollCatalogEntryVm(
                spell.name,
                ScrollSourceKind.Spell,
                $"Spell L{Math.Max(1, spell.level)}",
                spell.verbal,
                ScrollLanguage.ManaGlyphs)));
            _allEntries.AddRange(miraclesTask.Result.Select(miracle => new ScrollCatalogEntryVm(
                miracle.name,
                ScrollSourceKind.Miracle,
                $"Miracle P{Math.Max(1, miracle.power)}",
                miracle.verbal,
                ScrollLanguage.SpiritRunes)));
            _allEntries.AddRange(evocationsTask.Result.Select(evocation => new ScrollCatalogEntryVm(
                evocation.name,
                ScrollSourceKind.Evocation,
                $"Evocation P{Math.Max(1, evocation.power)}",
                evocation.verbal,
                ScrollLanguage.Ogham)));

            ApplyFilter();
            RefreshPreview();
        }

        public ScrollDraft BuildDraft()
        {
            var language = ResolveLanguage();
            var manaGlyphVariant = UseAdvancedManaGlyphs ? ManaGlyphVariant.Advanced : ManaGlyphVariant.Basic;
            var previewFont = ResolvePreviewFontFamily(language, manaGlyphVariant);

            if (IsCribSheetMode)
            {
                return new ScrollDraft(
                    Text: string.Empty,
                    Language: language,
                    ManaGlyphVariant: manaGlyphVariant,
                    DocumentName: $"{SelectedCribSheetLabel.ToLowerInvariant()}-crib-sheet",
                    PreviewFontFamily: previewFont,
                    IsCribSheet: true,
                    CribSheetTitle: ScrollCribSheetCatalog.GetTitle(language),
                    CribSheetEntries: ScrollCribSheetCatalog.GetEntries(language),
                    CribSheetNote: ScrollCribSheetCatalog.GetNote(language));
            }

            string text;
            string documentName;

            if (IsCatalogMode)
            {
                var entry = SelectedEntry ?? throw new InvalidOperationException("Select a spell, miracle, or evocation first.");
                var outputText = ScrollTextFormatter.BuildOutputText(entry.Verbal, entry.Kind, AsScroll, entry.Name);
                text = ScrollTextFormatter.NormalizeForGlyphDisplay(
                    outputText,
                    language);
                documentName = entry.Name;
            }
            else
            {
                var custom = ScrollTextFormatter.NormalizeMultilineText(CustomText);
                if (custom.Length == 0)
                    throw new InvalidOperationException("Enter some custom text first.");

                text = custom;
                documentName = "custom-scroll";
            }

            return new ScrollDraft(text, language, manaGlyphVariant, documentName, previewFont, false, null, Array.Empty<ScrollCribSheetEntry>(), null);
        }

        public void ApplyPreview(string text, string fontFamily)
        {
            PreviewText = text;
            PreviewFontFamily = fontFamily;
        }

        private void RefreshPreview()
        {
            try
            {
                var draft = BuildDraft();
                ApplyPreview(draft.Text, draft.PreviewFontFamily);
                RefreshCribSheetPreview(draft);
            }
            catch
            {
                PreviewFontFamily = "LibreCaslonTextRegular";
                PreviewText = IsCatalogMode
                    ? "Select a spell, miracle, or evocation to preview its verbal."
                    : IsCustomMode
                        ? "Enter custom text to preview it here."
                        : "Select a crib sheet to preview it here.";
                CribSheetPreviewEntries.Clear();
                Raise(nameof(CribSheetNote));
                Raise(nameof(HasCribSheetNote));
            }
        }

        private void RefreshCribSheetPreview(ScrollDraft draft)
        {
            CribSheetPreviewEntries.Clear();
            if (!draft.IsCribSheet)
            {
                Raise(nameof(CribSheetNote));
                Raise(nameof(HasCribSheetNote));
                return;
            }

            foreach (var entry in draft.CribSheetEntries)
            {
                CribSheetPreviewEntries.Add(new CribSheetPreviewEntryVm(
                    entry.GlyphText,
                    entry.Translation,
                    draft.PreviewFontFamily));
            }

            Raise(nameof(CribSheetNote));
            Raise(nameof(HasCribSheetNote));
        }

        private void ApplyFilter()
        {
            var query = (SearchText ?? string.Empty).Trim();
            var filtered = _allEntries
                .Where(entry =>
                    query.Length == 0
                    || entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || entry.KindLabel.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.KindLabel, StringComparer.OrdinalIgnoreCase)
                .ToList();

            FilteredEntries.Clear();
            foreach (var entry in filtered)
                FilteredEntries.Add(entry);

            if (SelectedEntry != null && !FilteredEntries.Contains(SelectedEntry))
                SelectedEntry = null;
        }

        private void UpdateScrollAvailability()
        {
            if (!CanUseAsScroll)
                AsScroll = false;

            Raise(nameof(CanUseAsScroll));
            Raise(nameof(AsScrollLabelOpacity));
            Raise(nameof(IsManaGlyphToggleVisible));
            Raise(nameof(ShowOptionsRow));
        }

        private ScrollSourceKind ResolveSourceKind()
            => IsCatalogMode ? SelectedEntry?.Kind ?? ScrollSourceKind.Custom : ScrollSourceKind.Custom;

        private ScrollLanguage ResolveLanguage()
        {
            if (IsCatalogMode)
                return SelectedEntry?.Language ?? ScrollLanguage.ManaGlyphs;

            if (IsCribSheetMode)
            {
                return SelectedCribSheetLabel switch
                {
                    "Spirit" => ScrollLanguage.SpiritRunes,
                    "Ogham" => ScrollLanguage.Ogham,
                    _ => ScrollLanguage.ManaGlyphs
                };
            }

            return SelectedLanguageLabel switch
            {
                "Spirit Runes" => ScrollLanguage.SpiritRunes,
                "Ogham" => ScrollLanguage.Ogham,
                _ => ScrollLanguage.ManaGlyphs
            };
        }

        private static string ResolvePreviewFontFamily(ScrollLanguage language, ManaGlyphVariant manaGlyphVariant)
            => language switch
            {
                ScrollLanguage.ManaGlyphs => manaGlyphVariant == ManaGlyphVariant.Basic ? "ManaGlyphsBasic" : "ManaGlyphs",
                ScrollLanguage.SpiritRunes => "SpiritRunes",
                ScrollLanguage.Ogham => "OghamFont",
                _ => "LibreCaslonTextRegular"
            };

        private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            Raise(propertyName);
            return true;
        }

        private void Raise([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed record ScrollCatalogEntryVm(
        string Name,
        ScrollSourceKind Kind,
        string Summary,
        string Verbal,
        ScrollLanguage Language)
    {
        public string KindLabel => Kind switch
        {
            ScrollSourceKind.Spell => "Spell",
            ScrollSourceKind.Miracle => "Miracle",
            ScrollSourceKind.Evocation => "Evocation",
            _ => "Custom"
        };

        public string LanguageLabel => ScrollDocumentService.GetLanguageLabel(Language);
    }

    private sealed record ScrollDraft(
        string Text,
        ScrollLanguage Language,
        ManaGlyphVariant ManaGlyphVariant,
        string DocumentName,
        string PreviewFontFamily,
        bool IsCribSheet,
        string? CribSheetTitle,
        IReadOnlyList<ScrollCribSheetEntry> CribSheetEntries,
        string? CribSheetNote);

    private sealed record CribSheetPreviewEntryVm(
        string GlyphText,
        string Translation,
        string GlyphFontFamily);
}
