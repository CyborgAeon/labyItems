using System.Globalization;
using labyItems.Helpers;

namespace labyItems.Controls;

public sealed class BooleanToChevronConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => FontAwesomeGlyphs.Chevron;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
