namespace labyItems.Models.Characters;

public sealed class CharacterSpendEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int Position { get; set; }
    public CharacterSpendEntryType Type { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Cost { get; set; }
    public int? PurchaseLevel { get; set; }
    public int? CharacterLevel { get; set; }
    public bool IsCharacterCreationChoice { get; set; }
    public Dictionary<string, string> ChoiceSelections { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}
