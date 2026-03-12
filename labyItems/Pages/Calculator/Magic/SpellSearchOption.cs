using labyItems.Services;

namespace labyItems.Pages.Calculator;

public sealed class SpellSearchOption
{
    public SpellSearchOption(SpellService.SpellRaw spell)
    {
        Spell = spell ?? new SpellService.SpellRaw();
        Name = Spell.name ?? string.Empty;
        Power = Math.Max(1, Spell.level);
        Colour = Spell.colour ?? string.Empty;
        IsAdvanced = Spell.isAdvanced ?? false;
    }

    public SpellService.SpellRaw Spell { get; }
    public string Name { get; }
    public int Power { get; }
    public string Colour { get; }
    public bool IsAdvanced { get; }
}
