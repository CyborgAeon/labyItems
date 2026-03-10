using System.Linq;

namespace labyItems.Pages.Characters.ViewModels;

public static class SpecialisationSummaryDedupe
{
    public static string BuildKey(string text, string? specialisationKey, string? selectedOption)
    {
        var keyToken = Normalize(specialisationKey);
        var selectedToken = Normalize(selectedOption);

        if (keyToken.Length == 0)
            return Normalize(text);

        return $"{keyToken}:{selectedToken}";
    }

    private static string Normalize(string? value)
    {
        var chars = (value ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray();

        return new string(chars);
    }
}
