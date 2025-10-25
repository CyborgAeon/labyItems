using labyItems.Pages.Configs;
namespace labyItems.Services;


public static class ConsumableService
{
    // Displays a search/picker UI for the given type and returns a selection.
    // Implement similarly to your Evocation().PickAsync.
    public static Task<ConsumableEntry?> PickAsync(INavigation nav, ConsumableType type)
    {
        // TODO: present a search page that queries the right doc based on `type`
        // and returns a ConsumableEntry(name, value).
        // For now, throw to remind implementation is pending.
        throw new NotImplementedException("Wire this to your four search docs and return name + integer value.");
    }
}
