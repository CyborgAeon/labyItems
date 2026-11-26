using labyItems.Helpers;
using labyItems.Models;
using labyItems.Pages.Characters;
using labyItems.Pages.Characters.ViewModels;
using labyItems.Services;

namespace labyItems.Pages.Battleboard;

internal static class SavedBattleboardPreparation
{
    public static async Task OpenAsync(Page host, Character character)
    {
        var prepared = await PrepareAsync(host, character, "opening the battleboard");
        if (prepared != null)
            await host.Navigation.PushAsync(new BattleboardPage(prepared.Draft));
    }

    public static async Task<WizardVm?> PrepareAsync(Page host, Character character, string action)
    {
        var session = LabyrintheSession.Shared;
        var version = session.Version;
        try
        {
            var draft = LiteDbService.ToDraft(character);
            if (draft == null)
                return null;

            var wizard = new WizardVm(draft, runBuilderStartupPipeline: false, runInitialSync: false);
            await wizard.RefreshReviewAsync();
            session.RequireVersion(version);

            var completed = await GuildBenefitChoicePromptHelper.EnsureChoicesCompletedAsync(
                host,
                wizard.GuildsVm,
                refreshAfterSelection: () => wizard.RefreshReviewAsync(),
                actionLabel: action);
            session.RequireVersion(version);
            return completed ? wizard : null;
        }
        catch (InvalidOperationException) when (session.Version != version)
        {
            return null;
        }
        catch (Exception exception)
        {
            RuntimeLog.Write("SAVED_BATTLEBOARD_PREPARATION", "Could not prepare the saved character for the battleboard.", exception);
            if (session.Version == version)
                await host.DisplayAlert("Battleboard unavailable", "The saved character could not be prepared.", "OK");
            return null;
        }
    }
}
