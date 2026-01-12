using System;
using System.Collections.Generic;
using System.Text;

namespace labyItems.Controls;

internal static class EnumDisplayFormatter
{
    public static string Format<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        return FormatName(value.ToString());
    }

    public static string FormatName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        if (TryGetClassPrefix(name, out var prefix, out var remainder))
        {
            var remainderWords = SplitPascalWords(remainder);
            var remainderText = JoinWords(remainderWords, capitalizeFirstWord: false);
            return $"{prefix} class {remainderText}";
        }

        var words = SplitPascalWords(name);
        return JoinWords(words, capitalizeFirstWord: true);
    }

    private static bool TryGetClassPrefix(string name, out string prefix, out string remainder)
    {
        prefix = string.Empty;
        remainder = string.Empty;

        if (name.Length >= 3 && char.IsUpper(name[0]) && char.IsUpper(name[1]) && char.IsLower(name[2]))
        {
            prefix = name[0].ToString();
            remainder = name[1..];
            return true;
        }

        return false;
    }

    private static List<string> SplitPascalWords(string input)
    {
        var words = new List<string>();
        if (string.IsNullOrEmpty(input))
            return words;

        var current = new StringBuilder();

        for (int i = 0; i < input.Length; i++)
        {
            var c = input[i];
            var prev = i > 0 ? input[i - 1] : '\0';
            var next = i + 1 < input.Length ? input[i + 1] : '\0';

            var isBoundary = i > 0 && char.IsUpper(c) &&
                             (char.IsLower(prev) || (char.IsUpper(prev) && char.IsLower(next)));

            if (isBoundary && current.Length > 0)
            {
                words.Add(current.ToString());
                current.Clear();
            }

            current.Append(c);
        }

        if (current.Length > 0)
            words.Add(current.ToString());

        return words;
    }

    private static string JoinWords(IReadOnlyList<string> words, bool capitalizeFirstWord)
    {
        if (words.Count == 0)
            return string.Empty;

        var formatted = new List<string>(words.Count);

        for (int i = 0; i < words.Count; i++)
        {
            var word = words[i];
            if (string.IsNullOrEmpty(word))
                continue;

            if (i == 0 && capitalizeFirstWord)
            {
                formatted.Add(Capitalize(word));
            }
            else
            {
                formatted.Add(word.ToLowerInvariant());
            }
        }

        return string.Join(' ', formatted);
    }

    private static string Capitalize(string word)
    {
        if (string.IsNullOrEmpty(word))
            return string.Empty;

        if (word.Length == 1)
            return word.ToUpperInvariant();

        return char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant();
    }
}
