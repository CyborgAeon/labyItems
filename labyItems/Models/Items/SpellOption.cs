using labyItems.Models.Enums;

namespace labyItems.Models.Items;

public readonly record struct SpellOption(string Name, int Level, bool IsAdvanced);
