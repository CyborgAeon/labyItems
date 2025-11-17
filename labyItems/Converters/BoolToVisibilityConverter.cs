using System.Globalization;

namespace labyItems.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    // If true → visible, false → hidden
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool visible;

        if (value is bool b)
        {
            visible = b;
        }
        else if (value is string s)
        {
            // string: visible if not null/whitespace
            visible = !string.IsNullOrWhiteSpace(s);
        }
        else if (value == null)
        {
            visible = false;
        }
        else
        {
            // fallback: show
            visible = true;
        }

        if (parameter?.ToString() == "Invert")
            visible = !visible;

        return visible;
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture
    ) => throw new NotImplementedException();
}
