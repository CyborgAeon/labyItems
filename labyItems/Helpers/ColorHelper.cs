namespace labyItems.Helpers;

public static class ColorHelper
{
    public static Color GetColorByKey(this string id) => (Color)Application.Current.Resources[id];
}
