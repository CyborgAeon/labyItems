using labyItems.Models;
using labyItems.Models.Characters;
using labyItems.Pages.Characters.ViewModels;
using labyItems.Services;
using System.Linq;
using System.Diagnostics;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific;
using AndroidConfig = Microsoft.Maui.Controls.PlatformConfiguration.Android;
using SpellCardPage = labyItems.Pages.SpellCard.SpellCard;
using MiracleCardPage = labyItems.Pages.MiracleCard.MiracleCard;
using EvocationCardPage = labyItems.Pages.EvocationCard.EvocationCard;
namespace labyItems.Pages.Characters;

public partial class AdvanceCharacterPage : Microsoft.Maui.Controls.TabbedPage
{
    private readonly AdvanceCharacterVm _vm;
    private readonly AdvanceCharacterDetailsTabVm _detailsTabVm;
    private readonly AdvanceCharacterSpellsTabVm _spellsTabVm;
    private readonly AdvanceCharacterMiraclesTabVm _miraclesTabVm;
    private readonly AdvanceCharacterEvocationsTabVm _evocationsTabVm;

    public AdvanceCharacterPage(Character character)
        : this(LiteDbService.ToDraft(character) ?? new CharacterDraft())
    {
        Title = string.IsNullOrWhiteSpace(character?.Name) ? "Advance Character" : $"Advance {character.Name}";
    }

    public AdvanceCharacterPage(CharacterDraft draft)
    {
        InitializeComponent();
        this.On<AndroidConfig>().SetIsSwipePagingEnabled(false);
        var serviceProvider = Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services;
        _vm = new AdvanceCharacterVm(
            draft,
            draftStore: new CharacterDraftStore(draft),
            domainService: serviceProvider?.GetService<ICharacterAdvancementDomainService>(),
            tabVisibilityService: serviceProvider?.GetService<IAdvancementTabVisibilityService>(),
            validationService: serviceProvider?.GetService<IAdvancementValidationService>(),
            exportService: serviceProvider?.GetService<IExportService>(),
            fileService: serviceProvider?.GetService<IFileService>());

        _detailsTabVm = new AdvanceCharacterDetailsTabVm(_vm);
        _spellsTabVm = new AdvanceCharacterSpellsTabVm(_vm);
        _miraclesTabVm = new AdvanceCharacterMiraclesTabVm(_vm);
        _evocationsTabVm = new AdvanceCharacterEvocationsTabVm(_vm);

        BindingContext = _vm;
        BindTabContexts();

        AbilitySearch.RemoteSearchProvider = _detailsTabVm.SearchAbilityOptionsAsync;
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
                BindTabContext(page);
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

    private void BindTabContexts()
    {
        BindTabContext(DetailsTab);
        BindTabContext(SpellsTab);
        BindTabContext(MiraclesTab);
        BindTabContext(EvocsTab);
    }

    private void BindTabContext(Page page)
    {
        if (ReferenceEquals(page, DetailsTab))
            page.BindingContext = _detailsTabVm;
        else if (ReferenceEquals(page, SpellsTab))
            page.BindingContext = _spellsTabVm;
        else if (ReferenceEquals(page, MiraclesTab))
            page.BindingContext = _miraclesTabVm;
        else if (ReferenceEquals(page, EvocsTab))
            page.BindingContext = _evocationsTabVm;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.PersistDraft();
    }

    private async void OnViewSpellDetailsClicked(object sender, EventArgs e)
    {
        if (sender is not Microsoft.Maui.Controls.Button button)
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
        if (sender is not Microsoft.Maui.Controls.Button button)
            return;

        if (button.CommandParameter is not MiracleEntryVm entry)
            return;

        var miracle = _vm.FindMiracleByName(entry.Draft.Name);
        if (miracle == null)
            return;

        await Navigation.PushAsync(new MiracleCardPage(miracle));
    }

    private async void OnViewEvocationDetailsClicked(object sender, EventArgs e)
    {
        if (sender is not Microsoft.Maui.Controls.Button button)
            return;

        if (button.CommandParameter is not EvocationEntryVm entry)
            return;

        var evocation = _vm.FindEvocationByName(entry.Draft.Name);
        if (evocation == null)
            return;

        await Navigation.PushAsync(new EvocationCardPage(evocation));
    }
}
