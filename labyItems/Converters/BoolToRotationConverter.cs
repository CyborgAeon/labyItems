using System.Globalization;

namespace labyItems.Converters
{
    public class BoolToRotationConverter : IValueConverter
    {
        // Rotates the caret when expanded (0° when collapsed, 90° when expanded)
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? 90 : 0;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}