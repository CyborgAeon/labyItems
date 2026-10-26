namespace labyItems.Pages.Characters.ViewModels;

public sealed class ChoiceSetAbilityRowVm
{
    private Color _rowBackgroundColor = Colors.Transparent;

    public ChoiceSetAbilityRowVm(string displayName, string lookupKey)
    {
        DisplayName = displayName;
        LookupKey = lookupKey;
    }

    public string DisplayName { get; }
    public string LookupKey { get; }
    public bool IsValid => !string.IsNullOrWhiteSpace(DisplayName) || !string.IsNullOrWhiteSpace(LookupKey);
    public Color RowBackgroundColor
    {
        get => _rowBackgroundColor;
        set => _rowBackgroundColor = value;
    }
}
