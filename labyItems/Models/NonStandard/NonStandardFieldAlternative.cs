using labyItems.Services;

namespace labyItems.Models.NonStandard;

public sealed record NonStandardFieldAlternative(
    string SourceName,
    string ValueJson,
    string DisplayText);
