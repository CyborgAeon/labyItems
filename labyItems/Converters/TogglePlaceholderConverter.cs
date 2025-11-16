using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace labyItems.Converters
{
    public class TogglePlaceholderConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string action && !string.IsNullOrWhiteSpace(action))
            {
                return $"Group to {action}";
            }

            return "Group to repel"; // Default
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture
        ) => throw new NotImplementedException();
    }
}
