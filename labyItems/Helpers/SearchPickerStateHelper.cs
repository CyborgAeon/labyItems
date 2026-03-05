namespace labyItems.Helpers;

public static class SearchPickerStateHelper
{
    public static void ClearForNextSearch<TOption>(
        Action<TOption?> setSelection,
        Action<string> setSearchText)
        where TOption : struct
    {
        setSelection(default);
        setSearchText(string.Empty);
    }
}
