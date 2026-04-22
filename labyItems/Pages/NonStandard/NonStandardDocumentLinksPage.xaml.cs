using System.Collections.ObjectModel;
using labyItems.Services;
using Microsoft.Maui.ApplicationModel;

namespace labyItems.Pages.NonStandard;

public sealed class NonStandardDocumentLinkVm
{
    public NonStandardDocumentLinkVm(NonStandardDocumentLink document)
    {
        Document = document;
    }

    public NonStandardDocumentLink Document { get; }
    public string DisplayName => Document.DisplayName;
    public string FilePath => Document.FilePath;
    public string MetaText => $"{Document.FileKind} • Updated {FormatRelativeTime(Document.UpdatedAtUtc)}";

    private static string FormatRelativeTime(DateTimeOffset timestamp)
    {
        var span = DateTimeOffset.UtcNow - timestamp;
        if (span.TotalDays >= 2)
            return $"{Math.Floor(span.TotalDays)}d ago";
        if (span.TotalDays >= 1)
            return "1d ago";
        if (span.TotalHours >= 2)
            return $"{Math.Floor(span.TotalHours)}h ago";
        if (span.TotalHours >= 1)
            return "1h ago";
        if (span.TotalMinutes >= 2)
            return $"{Math.Floor(span.TotalMinutes)}m ago";
        return "just now";
    }
}

public partial class NonStandardDocumentLinksPage : ContentPage
{
    private readonly NonStandardWalletEntry _entry;
    private readonly IDocumentReferenceService _documentReferenceService;

    public ObservableCollection<NonStandardDocumentLinkVm> Documents { get; } = new();
    public string CreationTitle => $"{_entry.Name} • {(_entry.Subtitle ?? string.Empty).Replace("â€¢", "•")}";
    public string SummaryText
        => Documents.Count == 0
            ? "No files linked yet."
            : $"{Documents.Count} linked file{(Documents.Count == 1 ? string.Empty : "s")}";
    public bool ShowEmptyUploadButton => Documents.Count == 0;

    public NonStandardDocumentLinksPage(NonStandardWalletEntry entry)
    {
        _entry = entry;
        _documentReferenceService = ServiceHelper.ResolveService<IDocumentReferenceService>() ?? new DocumentReferenceService();
        InitializeComponent();
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        try
        {
            var documents = await NonStandardContentService.GetDocumentLinksAsync(_entry.EntityType, _entry.Name);
            Documents.Clear();
            foreach (var document in documents)
                Documents.Add(new NonStandardDocumentLinkVm(document));

            OnPropertyChanged(nameof(SummaryText));
            OnPropertyChanged(nameof(ShowEmptyUploadButton));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Load failed", ex.Message, "OK");
        }
    }

    private async void OnUploadClicked(object sender, EventArgs e)
    {
        try
        {
            var file = await PickDocumentAsync();

            if (file == null)
                return;

            var suggestedName = Path.GetFileNameWithoutExtension(file.FileName ?? file.DisplayPath ?? "linked-file");
            var displayName = await DisplayPromptAsync(
                "Document name",
                "Enter a name for this linked file.",
                accept: "Save",
                cancel: "Cancel",
                placeholder: "Document name",
                initialValue: suggestedName);

            if (string.IsNullOrWhiteSpace(displayName))
                return;

            await NonStandardContentService.SaveDocumentLinkAsync(
                _entry.EntityType,
                _entry.Name,
                displayName,
                file);

            await ReloadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Upload failed", ex.Message, "OK");
        }
    }

    private async Task<DocumentReferenceCapture?> PickDocumentAsync()
    {
        var action = await DisplayActionSheet(
            "Add a linked file",
            "Cancel",
            null,
            "Take photo",
            "Choose file");

        return action switch
        {
            "Take photo" => await CapturePhotoAsync(),
            "Choose file" => await _documentReferenceService.PickDocumentAsync(),
            _ => null
        };
    }

    private async Task<DocumentReferenceCapture?> CapturePhotoAsync()
    {
        try
        {
            return await _documentReferenceService.CapturePhotoAsync();
        }
        catch (InvalidOperationException ex)
        {
            await DisplayAlert("Camera unavailable", ex.Message, "OK");
            return null;
        }
    }

    private async void OnOpenDocumentClicked(object sender, EventArgs e)
    {
        var vm = ResolveVm(sender);
        if (vm == null)
            return;

        try
        {
            if (!await _documentReferenceService.IsAvailableAsync(vm.Document))
            {
                await DisplayAlert("Missing file", "That file is no longer available on this device.", "OK");
                return;
            }

            await _documentReferenceService.OpenAsync(vm.Document);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Open failed", ex.Message, "OK");
        }
    }

    private async void OnDeleteDocumentClicked(object sender, EventArgs e)
    {
        var vm = ResolveVm(sender);
        if (vm == null)
            return;

        var confirm = await DisplayAlert("Remove link", $"Remove \"{vm.DisplayName}\" from this creation?", "Remove", "Cancel");
        if (!confirm)
            return;

        try
        {
            await NonStandardContentService.DeleteDocumentLinkAsync(vm.Document.Id);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Remove failed", ex.Message, "OK");
        }
    }

    private static NonStandardDocumentLinkVm? ResolveVm(object sender)
    {
        if (sender is not BindableObject bindable)
            return null;

        return (bindable as Button)?.CommandParameter as NonStandardDocumentLinkVm
            ?? bindable.BindingContext as NonStandardDocumentLinkVm;
    }
}
