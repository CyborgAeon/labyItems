using System.Globalization;

namespace labyItems.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    // If true → visible, false → hidden
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null) return true; // show both if IsAdvanced is null
        if (value is bool b)
        {
            if (parameter?.ToString() == "Invert")
                b = !b;
            return b;
        }
        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
