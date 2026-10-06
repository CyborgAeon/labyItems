using labyItems.Pages.AbilityCard;
using labyItems.Services;

namespace labyItems.Helpers;

public static class ImmunityNavigationHelper
{
    public static bool HasIndex(IEnumerable<string>? indices) =>
        indices?.Any(index => !string.IsNullOrWhiteSpace(index)) == true;

    public static async Task OpenFirstAvailableAsync(INavigation? navigation, IEnumerable<string>? indices)
    {
        if (navigation == null || indices == null)
            return;

        foreach (var index in indices.Where(index => !string.IsNullOrWhiteSpace(index)))
        {
            var ability = await AbilityDetailsLookupService.FindByIndexAsync(index);
            if (ability == null)
                continue;

            await navigation.PushAsync(new AbilityCard(ability));
            return;
        }
    }
}
