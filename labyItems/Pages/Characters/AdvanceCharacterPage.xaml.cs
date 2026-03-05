using labyItems.Models;
using labyItems.Models.Characters;
using labyItems.Pages.Characters.ViewModels;
using labyItems.Services;
using System.Linq;
using System.Diagnostics;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific;
using AndroidConfig = Microsoft.Maui.Controls.PlatformConfiguration.Android;
using SpellCardPage = labyItems.Pages.SpellCard.SpellCard;
using MiracleCardPage = labyItems.Pages.MiracleCard.MiracleCard;
namespace labyItems.Pages.Characters;

public partial class AdvanceCharacterPage : Microsoft.Maui.Controls.TabbedPage
{
    private readonly AdvanceCharacterVm _vm;

    public AdvanceCharacterPage(Character character)
        : this(LiteDbService.ToDraft(character) ?? new CharacterDraft())
    {
        Title = string.IsNullOrWhiteSpace(character?.Name) ? "Advance Character" : $"Advance {character.Name}";
    }

    public AdvanceCharacterPage(CharacterDraft draft)
    {
        InitializeComponent();
        this.On<AndroidConfig>().SetIsSwipePagingEnabled(false);
        _vm = new AdvanceCharacterVm(draft);
        BindingContext = _vm;
        foreach (var child in Children)
            child.BindingContext = _vm;

        AbilitySearch.RemoteSearchProvider = _vm.SearchAbilityOptionsAsync;
        ApplyTabVisibility();

        ToolbarItems.Add(new ToolbarItem
        {
            Text = "Save",
            Command = _vm.SaveCommand
        });

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await _vm.InitializeAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ADVANCE][INIT] InitializeAsync failed: {ex}");
            }
            finally
            {
                ApplyTabVisibility();
            }
        });
    }

    private void ApplyTabVisibility()
    {
        var desiredTabs = new List<Page> { DetailsTab };
        if (_vm.ShowSpellsTab)
            desiredTabs.Add(SpellsTab);
        if (_vm.ShowMiraclesTab)
            desiredTabs.Add(MiraclesTab);
        if (_vm.ShowEvocationsTab)
            desiredTabs.Add(EvocsTab);

        foreach (var page in Children.ToList())
        {
            if (!desiredTabs.Contains(page))
                Children.Remove(page);
        }

        for (var i = 0; i < desiredTabs.Count; i++)
        {
            var page = desiredTabs[i];
            if (!Children.Contains(page))
            {
                Children.Insert(i, page);
                page.BindingContext = _vm;
                continue;
            }

            var currentIndex = Children.IndexOf(page);
            if (currentIndex != i)
            {
                Children.Remove(page);
                Children.Insert(i, page);
            }
        }

        if (Children.Contains(DetailsTab))
            CurrentPage = DetailsTab;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.PersistDraft();
    }

    private async void OnViewSpellDetailsClicked(object sender, EventArgs e)
    {
        if (sender is not Microsoft.Maui.Controls.ImageButton button)
            return;

        if (button.CommandParameter is not SpellEntryVm entry)
            return;

        var spell = _vm.FindSpellByName(entry.Draft.Name);
        if (spell == null)
            return;

        await Navigation.PushAsync(new SpellCardPage(spell));
    }

    private async void OnViewMiracleDetailsClicked(object sender, EventArgs e)
    {
        if (sender is not Microsoft.Maui.Controls.ImageButton button)
            return;

        if (button.CommandParameter is not MiracleEntryVm entry)
            return;

        var miracle = _vm.FindMiracleByName(entry.Draft.Name);
        if (miracle == null)
            return;

        await Navigation.PushAsync(new MiracleCardPage(miracle));
    }
}
