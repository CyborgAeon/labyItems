using Microsoft.Maui.Controls;
#if ANDROID
using Android.Widget;
using Android.Text;
#endif

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

            // But don't show or use the soft keyboard when it gets focus
            edit.ShowSoftInputOnFocus = false;
            edit.InputType = InputTypes.Null;
        }
#endif
    }
}
