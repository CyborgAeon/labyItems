using labyItems.Models.Enums;

namespace labyItems.Models.Items;

public readonly record struct NeuroOption(string Name, int Power, NeuroOptionType Type);
