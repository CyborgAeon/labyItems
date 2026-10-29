namespace labyItems.Models.Characters;

public sealed class CharacterSpend
{
    public string Name { get; set; } = string.Empty;
    public List<CharacterSpendEntry> Entries { get; set; } = new();
}
