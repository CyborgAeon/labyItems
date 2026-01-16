using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace labyItems.Converters;

public sealed class GreaterThanZeroConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int i) return i > 0;
        if (value is long l) return l > 0;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
