using System.Collections.Generic;

namespace labyItems.Models.Characters;

public sealed class MiracleListDraft
{
    public string Name { get; set; } = "Miracle List";
    public string? SourceName { get; set; }
    public bool IsImported { get; set; }
    public bool IsSaved { get; set; }
    public bool IsMinimized { get; set; }
    public bool IsScriptures { get; set; }
    public string? NeutralAlignmentChoice { get; set; }
    public List<MiracleListEntryDraft> Entries { get; set; } = new();
}

public sealed class MiracleListEntryDraft
{
    public string Name { get; set; } = string.Empty;
    public int Power { get; set; }
    public string Alignment { get; set; } = string.Empty;
    public string Sphere { get; set; } = string.Empty;
    public bool IsAdvanced { get; set; }
}

public sealed class SpellListDraft
{
    public string Name { get; set; } = "Spell List";
    public bool IsBaseList { get; set; }
    public bool IsMinimized { get; set; }
    public List<SpellListEntryDraft> Entries { get; set; } = new();
}

public sealed class SpellListEntryDraft
{
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public string Colour { get; set; } = string.Empty;
    public bool IsAdvanced { get; set; }
}

public sealed class EvocationListDraft
{
    public string Name { get; set; } = "Evocation List";
    public List<EvocationListEntryDraft> Entries { get; set; } = new();
}

public sealed class EvocationListEntryDraft
{
    public string Name { get; set; } = string.Empty;
    public int Power { get; set; }
    public bool IsAdvanced { get; set; }
}
