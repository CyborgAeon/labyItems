namespace labyItems.Pages.Characters;

public sealed class GuildBenefitChoiceOption
{
    public GuildBenefitChoiceOption(string label, IReadOnlyList<string> lines)
    {
        Label = label ?? string.Empty;
        Lines = lines ?? Array.Empty<string>();
    }

    public string Label { get; }
    public IReadOnlyList<string> Lines { get; }
}
