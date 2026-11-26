using labyItems.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace labyItems.Platforms.Android;

public sealed class LoginEntryHandler : EntryHandler
{
    protected override MauiAppCompatEditText CreatePlatformView()
        => new LoginAutofillEditText(Context)
        {
            Autofilled = () => (VirtualView as LoginEntry)?.NotifyAutofilled()
        };
}
