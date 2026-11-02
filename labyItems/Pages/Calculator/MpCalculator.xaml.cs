using System.Text.Json;
using labyItems.Models;
using labyItems.Helpers;
using labyItems.Services;
using labyItems.Models.Items;
namespace labyItems.Pages.Calculator;

public partial class MpCalculator : ContentPage
{
    private readonly Func<MpSubmissionPayload, Task>? _onCharacterItemSubmit;

    private readonly Item? _existingItem;
    private bool _openedEditor;

    partial void ApplyPlatformTabLayoutTweaks();

    public MpCalculator(Func<MpSubmissionPayload, Task>? onCharacterItemSubmit = null, Item? existingItem = null)
    {
        InitializeComponent();
        _onCharacterItemSubmit = onCharacterItemSubmit;
        _existingItem = existingItem;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_existingItem == null || _openedEditor) return;
        _openedEditor = true;
        var state = ItemEmailService.TryDeserializeItemPayload(_existingItem.PayloadJson)?.Item?.MpEditorState;
        MpCalculatorPageBase page = state?.CalculatorType switch
        {
            nameof(MpCrewPage) => new MpCrewPage(),
            nameof(MpThemedayPage) => new MpThemedayPage(),
            _ => new MpBasicPage()
        };
        await OpenEditorAsync(page);
    }

    private async Task OpenEditorAsync(MpCalculatorPageBase page)
    {
        page.CharacterItemSubmitHandler = _onCharacterItemSubmit;
        try
        {
            if (_existingItem != null)
            {
                var state = ItemEmailService.TryDeserializeItemPayload(_existingItem.PayloadJson)?.Item?.MpEditorState;
                if (state != null) page.RestoreEditorState(state);
                else page = await CreateLegacyEditorAsync(_existingItem);
                page.CharacterItemSubmitHandler = _onCharacterItemSubmit;
            }
            await Navigation.PushAsync(page);
            if (_existingItem != null) Navigation.RemovePage(this);
        }
        catch (Exception ex)
        {
            RuntimeLog.Write("MP_RESTORE", "Could not reopen the saved MP settings.", ex);
            await DisplayAlert("Unable to edit item", ex.Message, "OK");
            if (_existingItem != null) await Navigation.PopAsync();
        }
    }

    private static async Task<MpCalculatorPageBase> CreateLegacyEditorAsync(Item item)
    {
        var storedCost = ItemEmailService.TryDeserializeItemPayload(item.PayloadJson)?.Item?.MonsterPointCost ?? 0;
        if (storedCost <= 0)
        {
            using var document = JsonDocument.Parse(item.PayloadJson);
            var root = document.RootElement;
            if (root.TryGetProperty("mpTotal", out var total) && total.TryGetInt32(out var rawCost))
                storedCost = rawCost;
            else if (root.TryGetProperty("item", out var detail)
                && detail.TryGetProperty("monsterPointCost", out var cost) && cost.TryGetInt32(out rawCost))
                storedCost = rawCost;
        }
        MpCalculatorPageBase? firstRestored = null;
        Exception? restoreError = null;
        foreach (var create in new Func<MpCalculatorPageBase>[] { () => new MpBasicPage(), () => new MpCrewPage(), () => new MpThemedayPage() })
        {
            var candidate = create();
            try
            {
                await candidate.RestoreLegacyItem(item);
                firstRestored ??= candidate;
                if (storedCost <= 0 || candidate.TotalMp == storedCost) return candidate;
            }
            catch (Exception ex) { restoreError = ex; }
        }
        if (firstRestored != null && storedCost > 0)
            throw new InvalidOperationException("The saved settings do not reproduce this older item's MP cost. The original item has not been changed.");
        return firstRestored ?? throw restoreError ?? new InvalidOperationException("The saved item settings could not be restored.");
    }

    private async void OnBasicTapped(object sender, TappedEventArgs e)
        => await OpenEditorAsync(new MpBasicPage());

    private async void OnCrewTapped(object sender, TappedEventArgs e)
        => await OpenEditorAsync(new MpCrewPage());

    private async void OnThemeDayTapped(object sender, TappedEventArgs e)
        => await OpenEditorAsync(new MpThemedayPage());
}
