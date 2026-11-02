using labyItems.Services;

namespace labyItems.Models.NonStandard;

public sealed record NonStandardTemplate(
    string Name,
    string Json,
    string Subtitle);
