namespace labyItems.Pages.Characters;

public static class GuildBenefitChoicePromptHelper
{
    public static async Task<bool> EditChoicesForGuildAsync(
        Page hostPage,
        GuildsVm guildsVm,
        string guildName,
        Func<Task>? refreshAfterSelection = null)
    {
        if (hostPage == null || guildsVm == null)
            return false;

        var rows = await guildsVm.BuildChoiceRowsForGuildAsync(guildName);
        if (rows.Count == 0)
        {
            await hostPage.DisplayAlert(
                "No guild choices",
                "This guild has no selectable options at the character's current tier.",
                "OK");
            return false;
        }

        var orderedRows = OrderRows(rows);
        var pickerPage = new GuildBenefitSelectionPage(orderedRows, guildName);
        await hostPage.Navigation.PushModalAsync(new NavigationPage(pickerPage));
        await pickerPage.Result;

        if (refreshAfterSelection != null)
            await refreshAfterSelection();

        return true;
    }

    public static async Task<bool> EnsureChoicesCompletedAsync(
        Page hostPage,
        GuildsVm guildsVm,
        Func<Task>? refreshAfterSelection = null,
        string actionLabel = "continue")
    {
        if (hostPage == null || guildsVm == null)
            return true;

        var pending = await guildsVm.BuildIncompleteChoiceRowsAsync();
        if (pending.Count == 0)
            return true;

        var intro = BuildIntroMessage(actionLabel, pending);
        var shouldContinue = await hostPage.DisplayAlert(
            "Guild choices required",
            intro,
            "Set choices",
            "Cancel");

        if (!shouldContinue)
            return false;

        var pendingByGuild = pending
            .GroupBy(row =>
            {
                var guild = (row.GuildName ?? string.Empty).Trim();
                return guild.Length == 0 ? "Guild" : guild;
            }, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var guildGroup in pendingByGuild)
        {
            var rowsForGuild = await guildsVm.BuildChoiceRowsForGuildAsync(guildGroup.Key);
            var rows = rowsForGuild.Count > 0
                ? OrderRows(rowsForGuild)
                : OrderRows(guildGroup.ToList());
            var pickerPage = new GuildBenefitSelectionPage(rows, guildGroup.Key);
            await hostPage.Navigation.PushModalAsync(new NavigationPage(pickerPage));
            await pickerPage.Result;
        }

        if (refreshAfterSelection != null)
            await refreshAfterSelection();

        var remaining = await guildsVm.BuildIncompleteChoiceRowsAsync();
        if (remaining.Count == 0)
            return true;

        await hostPage.DisplayAlert(
            "Guild choices still missing",
            BuildRemainingMessage(remaining),
            "OK");
        return false;
    }

    private static string BuildIntroMessage(string actionLabel, IReadOnlyList<GuildBenefitChoiceSummaryRow> pending)
    {
        var action = string.IsNullOrWhiteSpace(actionLabel) ? "continue" : actionLabel.Trim();
        var summary = SummarizePending(pending);
        return $"Before {action}, choose all required guild benefits.\n\n{summary}";
    }

    private static string BuildRemainingMessage(IReadOnlyList<GuildBenefitChoiceSummaryRow> pending)
    {
        var summary = SummarizePending(pending);
        return $"Some required guild choices are still unset.\n\n{summary}";
    }

    private static string SummarizePending(IReadOnlyList<GuildBenefitChoiceSummaryRow> pending)
    {
        if (pending == null || pending.Count == 0)
            return "No pending guild choices.";

        var grouped = pending
            .GroupBy(row =>
            {
                var guildName = string.IsNullOrWhiteSpace(row.GuildName) ? "Guild" : row.GuildName;
                var tier = string.IsNullOrWhiteSpace(row.Tier) ? "Basic" : row.Tier;
                return $"{guildName} ({tier})";
            }, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var count = group.Count();
                var suffix = count == 1 ? string.Empty : "s";
                return $"- {group.Key}: {count} choice{suffix}";
            });

        return string.Join("\n", grouped);
    }

    private static IReadOnlyList<GuildBenefitChoiceSummaryRow> OrderRows(IReadOnlyList<GuildBenefitChoiceSummaryRow> rows)
    {
        return (rows ?? Array.Empty<GuildBenefitChoiceSummaryRow>())
            .Where(row => row != null)
            .OrderBy(row => ResolveTierSortKey(row.Tier))
            .ThenBy(row => row.OptionNumber ?? int.MaxValue)
            .ThenBy(row => row.DisplayText, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int ResolveTierSortKey(string? tier)
    {
        var normalized = (tier ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "basic" => 0,
            "intermediate" => 1,
            "advanced" => 2,
            _ => 3
        };
    }
}
