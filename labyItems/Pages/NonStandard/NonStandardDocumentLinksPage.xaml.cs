using System.Collections.ObjectModel;
using labyItems.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

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
    private static readonly FilePickerFileType AllowedDocumentTypes = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        [DevicePlatform.iOS] = ["public.image", "com.adobe.pdf"],
        [DevicePlatform.MacCatalyst] = ["public.image", "com.adobe.pdf"],
        [DevicePlatform.Android] = ["image/*", "application/pdf"],
        [DevicePlatform.WinUI] = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".pdf"],
        [DevicePlatform.Tizen] = ["image/*", "application/pdf"]
    });

    private readonly NonStandardWalletEntry _entry;

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

            var suggestedName = Path.GetFileNameWithoutExtension(file.FileName);
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
                file.FullPath ?? file.FileName,
                file.ContentType);

            await ReloadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Upload failed", ex.Message, "OK");
        }
    }

    private async Task<FileResult?> PickDocumentAsync()
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
            "Choose file" => await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select an image or PDF",
                FileTypes = AllowedDocumentTypes
            }),
            _ => null
        };
    }

    private async Task<FileResult?> CapturePhotoAsync()
    {
        if (!MediaPicker.Default.IsCaptureSupported)
        {
            await DisplayAlert("Camera unavailable", "This device does not support taking photos from the app.", "OK");
            return null;
        }

        var permission = await Permissions.RequestAsync<Permissions.Camera>();
        if (permission != PermissionStatus.Granted)
        {
            await DisplayAlert("Camera permission needed", "Allow camera access to attach a photo to this creation.", "OK");
            return null;
        }

        try
        {
            return await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
            {
                Title = $"Creation photo {DateTime.Now:yyyyMMdd_HHmmss}"
            });
        }
        catch (FeatureNotSupportedException)
        {
            await DisplayAlert("Camera unavailable", "This device does not support taking photos from the app.", "OK");
            return null;
        }
        catch (PermissionException)
        {
            await DisplayAlert("Camera permission needed", "Allow camera access to attach a photo to this creation.", "OK");
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
            if (!File.Exists(vm.Document.FilePath))
            {
                await DisplayAlert("Missing file", "That file is no longer available on this device.", "OK");
                return;
            }

            var launcher = ServiceHelper.ResolveService<ILauncherService>() ?? new MauiLauncherService();
            await launcher.OpenFileAsync(vm.Document.FilePath);
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
