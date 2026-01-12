using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace labyItems.Converters;

public class EnumEqualsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;

        return value.Equals(parameter);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter != null)
            return parameter;

        return Binding.DoNothing;
    }
}
