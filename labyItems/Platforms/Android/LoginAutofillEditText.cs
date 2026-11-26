using Android.Content;
using Android.Views.Autofill;
using Microsoft.Maui.Platform;

namespace labyItems.Platforms.Android;

public sealed class LoginAutofillEditText(Context context) : MauiAppCompatEditText(context)
{
    public Action? Autofilled { get; set; }

    public override void Autofill(AutofillValue? value)
    {
        base.Autofill(value);
        if (value?.IsText == true) Autofilled?.Invoke();
    }
}
