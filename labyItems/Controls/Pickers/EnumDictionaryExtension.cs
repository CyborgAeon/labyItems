using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls.Xaml;

namespace labyItems.Controls;

/// <summary>
/// XAML helper to build a dictionary of enum display text -> enum value for DictionarySearchBar.
/// Usage: ItemsSource="{controls:EnumDictionary Type={x:Type enums:MagicColours} Exclude='Grey'}"
/// </summary>
public class EnumDictionaryExtension : IMarkupExtension
{
    /// <summary>
    /// Enum type to materialize. Required.
    /// </summary>
    public Type? Type { get; set; }

    /// <summary>
    /// Comma-separated list of enum names to exclude (case-insensitive). Optional.
    /// </summary>
    public string? Exclude { get; set; }

    public object ProvideValue(IServiceProvider serviceProvider)
    {
        if (Type == null || !Type.IsEnum)
            return new Dictionary<string, object>();

        var exclusions = (Exclude ?? string.Empty)
            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Create a typed Dictionary<string, TEnum> via reflection so binding matches the control's TValue.
        var dictionaryType = typeof(Dictionary<,>).MakeGenericType(typeof(string), Type);
        var dict = (IDictionary)Activator.CreateInstance(dictionaryType)!;

        foreach (var value in Enum.GetValues(Type))
        {
            var name = value?.ToString() ?? string.Empty;
            if (exclusions.Contains(name))
                continue;

            dict[EnumDisplayFormatter.FormatName(name)] = value!;
        }

        return dict;
    }
}
