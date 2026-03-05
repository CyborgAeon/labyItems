using System.Threading.Tasks;

namespace labyItems.Services;

public interface IClipboardService
{
    Task SetTextAsync(string text);
}
