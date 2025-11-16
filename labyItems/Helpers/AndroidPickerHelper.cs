using Android.Widget;

namespace labyItems.Helpers;

public static class AndroidPickerHelper
{
    public static void PreventTypingOpeningPicker(Microsoft.Maui.Controls.Picker picker)
    {
#if ANDROID
        if (picker?.Handler?.PlatformView is EditText edit)
        {
            // Make the picker ignore keyboard focus
            edit.Focusable = false;
            edit.FocusableInTouchMode = false;
        }
#endif
    }
}
