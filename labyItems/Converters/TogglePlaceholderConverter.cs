using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace labyItems.Converters
{
    public class TogglePlaceholderConverter : IValueConverter
    {
         public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // if parameter provided, we insert value after it:
            //   Example: parameter = "Prayer"
            //   minor → "Minor Prayer"
            //   major → "Major Prayer"
            if (parameter is string prefix && 
                value is string option && 
                !string.IsNullOrWhiteSpace(option))
            {
                return $"{char.ToUpper(option[0]) + option[1..]} {prefix}";
            }

            // Default fallback text
            return "Enter value";
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture
        ) => throw new NotImplementedException();
    }
}
