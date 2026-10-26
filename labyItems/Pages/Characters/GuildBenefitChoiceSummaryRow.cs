namespace labyItems.Pages.Characters;

public sealed class GuildBenefitChoiceSummaryRow
{
    private readonly Action<int?> _applySelection;

    public GuildBenefitChoiceSummaryRow(
        string displayText,
        string selectionKey,
        IReadOnlyList<GuildBenefitChoiceOption> options,
        int? selectedIndex,
        Action<int?> applySelection,
        string? guildName = null,
        string? tier = null,
        int? optionNumber = null,
        bool isTierAvailableNow = true)
    {
        DisplayText = string.IsNullOrWhiteSpace(displayText) ? "Choose one guild benefit" : displayText.Trim();
        SelectionKey = selectionKey?.Trim() ?? string.Empty;
        Options = options ?? Array.Empty<GuildBenefitChoiceOption>();
        SelectedIndex = selectedIndex;
        GuildName = (guildName ?? string.Empty).Trim();
        Tier = (tier ?? string.Empty).Trim();
        OptionNumber = optionNumber;
        IsTierAvailableNow = isTierAvailableNow;
        _applySelection = applySelection ?? throw new ArgumentNullException(nameof(applySelection));
    }

    public string DisplayText { get; }
    public string SelectionKey { get; }
    public IReadOnlyList<GuildBenefitChoiceOption> Options { get; }
    public int? SelectedIndex { get; private set; }
    public string GuildName { get; }
    public string Tier { get; }
    public int? OptionNumber { get; }
    public bool IsTierAvailableNow { get; }

    public void ApplySelection(int? selectedIndex)
    {
        SelectedIndex = selectedIndex;
        _applySelection(selectedIndex);
    }
}
