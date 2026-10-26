using labyItems.Models.Characters;

namespace labyItems.Services.Specialisations;

public static class SpecialisationSelectionPersistence
{
    public static void Reconcile(
        CharacterDraft draft,
        IEnumerable<string> managedKeys,
        IReadOnlyDictionary<string, string> resolvedSelections)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var keys = (managedKeys ?? Array.Empty<string>())
            .Select(key => (key ?? string.Empty).Trim())
            .Where(key => key.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var key in draft.SpecialisationSelections.Keys.Where(keys.Contains).ToList())
            draft.SpecialisationSelections.Remove(key);

        foreach (var entry in resolvedSelections ?? new Dictionary<string, string>())
        {
            var key = (entry.Key ?? string.Empty).Trim();
            var value = (entry.Value ?? string.Empty).Trim();
            if (key.Length > 0 && value.Length > 0)
                draft.SpecialisationSelections[key] = value;
        }
    }
}
