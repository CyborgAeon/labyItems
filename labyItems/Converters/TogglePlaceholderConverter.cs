using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace labyItems.Converters
{
    public class TogglePlaceholderConverter : IValueConverter
    {
        private static string Capitalize(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return char.ToUpper(text[0]) + text[1..];
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // parameter format:
            //   "option1|option2|prefix"
            // Examples:
            //   "Minor|Major|Prayer" => Minor/Major Prayer
            //   "Repel|Attract|group" => Repel/Attract group
            var parameterParts = (parameter as string)?.Split(
                '|',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            ) ?? Array.Empty<string>();

            var hasOptionPair = parameterParts.Length >= 2;
            var leftOption = hasOptionPair ? parameterParts[0] : null;
            var rightOption = hasOptionPair ? parameterParts[1] : null;
            var prefix = string.Empty;

            if (hasOptionPair)
            {
                prefix = parameterParts.Length > 2 ? parameterParts[2] : string.Empty;
            }
            else if (parameterParts.Length == 1)
            {
                prefix = parameterParts[0];
            }

            if (value is string option && !string.IsNullOrWhiteSpace(option))
            {
                return $"{Capitalize(option)} {prefix}".Trim();
            }

            if (hasOptionPair && !string.IsNullOrWhiteSpace(leftOption) && !string.IsNullOrWhiteSpace(rightOption))
            {
                return $"{Capitalize(leftOption)}/{Capitalize(rightOption)} {prefix}".Trim();
            }

            // Default fallback text
            return prefix;
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture
        ) => throw new NotImplementedException();
    }
}
