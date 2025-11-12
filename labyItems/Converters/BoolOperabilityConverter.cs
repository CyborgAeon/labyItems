// File: Converters/BoolConverters.cs
using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace labyItems.Converters
{
    public sealed class BoolIsTrueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b;
    }

    public sealed class BoolIsFalseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : true;   // null -> true (treat null as false)
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : false;
    }
}
