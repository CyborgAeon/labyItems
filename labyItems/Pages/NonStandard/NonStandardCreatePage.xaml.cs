using labyItems.Services;

namespace labyItems.Pages.NonStandard;

public partial class NonStandardCreatePage : TabbedPage
{
    public NonStandardCreatePage()
    {
        InitializeComponent();
        WalletTab.EditRequested += OnWalletEditRequested;
    }

    private async void OnWalletEditRequested(NonStandardWalletEntry entry)
    {
        if (entry.EntityType == NonStandardEntityType.CharacterClass)
        {
            CurrentPage = ClassTab;
            await ClassTab.LoadFromWalletEntryAsync(entry);
            return;
        }

        CurrentPage = LegacyTab;
        await LegacyTab.LoadFromWalletEntryAsync(entry);
    }
}
