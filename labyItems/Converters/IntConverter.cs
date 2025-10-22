using System.Globalization;

namespace labyItems.Converters;

/// <summary>
/// This is required for Maui to parse a "string" as an int.
/// </summary>
public sealed class IntConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value?.ToString() ?? "0";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s && int.TryParse(s, NumberStyles.Integer, culture, out var n))
            return n;
        return 0;
    }
}