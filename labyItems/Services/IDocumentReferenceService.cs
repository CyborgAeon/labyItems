using labyItems.Models.Documents;
using labyItems.Models.NonStandard;
namespace labyItems.Services;

public interface IDocumentReferenceService
{
    Task<DocumentReferenceCapture?> PickDocumentAsync();
    Task<DocumentReferenceCapture?> PickImageAsync();
    Task<DocumentReferenceCapture?> CapturePhotoAsync();
    Task<bool> IsAvailableAsync(NonStandardDocumentLink document);
    Task OpenAsync(NonStandardDocumentLink document);
}
