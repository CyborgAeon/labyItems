using labyItems.Models.Enums;

namespace labyItems.Models.Items;

public readonly record struct EvocationOption(string Name, int Power, bool IsAdvanced);
