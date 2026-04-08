using labyItems.Pages.Characters;

namespace labyItems.Helpers;

public static class WizardSlotSelectionFormatter
{
    public static string FormatSelection(SpecialisationSlotVm slot)
    {
        if (slot == null)
            return string.Empty;

        var baseName = slot.IsLocked
            ? (slot.ForcedAbilityDefinition?.Name ?? slot.LockedDisplayText)
            : (slot.SelectedOption ?? string.Empty).Trim();

        var custom = (slot.CustomisationValue ?? string.Empty).Trim();
        if (custom.Length == 0)
            return baseName;

        if (baseName.Length == 0)
            return custom;

        if (baseName.Contains(custom, StringComparison.OrdinalIgnoreCase))
            return baseName;

        return $"{baseName} ({custom})";
    }
}
