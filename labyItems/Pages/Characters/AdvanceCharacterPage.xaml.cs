using labyItems.Models;
using labyItems.Models.Characters;
using labyItems.Controls;
using labyItems.Helpers;
using labyItems.Pages.Calculator;
using labyItems.Pages.Characters.ViewModels;
using labyItems.Services;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific;
using AndroidConfig = Microsoft.Maui.Controls.PlatformConfiguration.Android;
using AndroidToolbarPlacement = Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.ToolbarPlacement;
using SpellCardPage = labyItems.Pages.SpellCard.SpellCard;
using MiracleCardPage = labyItems.Pages.MiracleCard.MiracleCard;
using EvocationCardPage = labyItems.Pages.EvocationCard.EvocationCard;
using AbilityCardPage = labyItems.Pages.AbilityCard.AbilityCard;
using ItemDetailCardPage = labyItems.Pages.ItemCard.ItemDetailCardPage;
namespace labyItems.Pages.Characters;

public partial class AdvanceCharacterPage : Microsoft.Maui.Controls.TabbedPage
{
    private readonly AdvanceCharacterVm _vm;
    private readonly AdvanceCharacterDetailsTabVm _detailsTabVm;
    private readonly AdvanceCharacterSpellsTabVm _spellsTabVm;
    private readonly AdvanceCharacterMiraclesTabVm _miraclesTabVm;
    private readonly AdvanceCharacterEvocationsTabVm _evocationsTabVm;
    private CancellationTokenSource? _appearingRefreshCts;
    private Page? _lastNonBackTab;
    private bool _isHandlingBackTabSelection;
    private static readonly Regex ContributionCostRegex = new(
        @"=\s*(?<cost>-?\d+)\s*$",
        RegexOptionsCompat.ForRuntime(RegexOptions.Compiled | RegexOptions.CultureInvariant));

    public AdvanceCharacterPage(Character character)
        : this(LiteDbService.ToDraft(character) ?? new CharacterDraft())
    {
        Title = string.Empty;
    }

    public AdvanceCharacterPage(CharacterDraft draft)
    {
        InitializeComponent();
        Shell.SetNavBarIsVisible(this, false);
        NavigationPage.SetHasNavigationBar(this, false);
        NavigationPage.SetHasBackButton(this, false);
        Shell.SetBackButtonBehavior(this, CreateHiddenBackButtonBehavior());
        this.On<AndroidConfig>().SetIsSwipePagingEnabled(false);
        this.On<AndroidConfig>().SetToolbarPlacement(AndroidToolbarPlacement.Top);
        Title = string.Empty;
        var serviceProvider = Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services
            ?? throw new InvalidOperationException("Service provider is not available.");
        _vm = new AdvanceCharacterVm(
            draft,
            draftStore: new CharacterDraftStore(draft),
            domainService: serviceProvider.GetRequiredService<ICharacterAdvancementDomainService>(),
            tabVisibilityService: serviceProvider.GetRequiredService<IAdvancementTabVisibilityService>(),
            validationService: serviceProvider.GetRequiredService<IAdvancementValidationService>(),
            exportService: serviceProvider.GetRequiredService<IExportService>(),
            fileService: serviceProvider.GetRequiredService<IFileService>(),
            dataProvider: serviceProvider.GetRequiredService<IAdvanceCharacterDataProvider>(),
            abilityLookupService: serviceProvider.GetRequiredService<IAdvanceAbilityLookupService>(),
            abilityAvailabilityService: serviceProvider.GetRequiredService<IAbilityAvailabilityService>());

        _detailsTabVm = new AdvanceCharacterDetailsTabVm(_vm);
        _spellsTabVm = new AdvanceCharacterSpellsTabVm(_vm);
        _miraclesTabVm = new AdvanceCharacterMiraclesTabVm(_vm);
        _evocationsTabVm = new AdvanceCharacterEvocationsTabVm(_vm);

        BindingContext = _vm;
        BindTabContexts();
        CurrentPageChanged += OnCurrentPageChanged;
        ConfigureTabPageChrome(BackTab);
        ConfigureTabPageChrome(DetailsTab);
        ConfigureTabPageChrome(SpellsTab);
        ConfigureTabPageChrome(MiraclesTab);
        ConfigureTabPageChrome(EvocsTab);
        ApplyTabVisibility();
        QueuePlatformTabLayoutRefresh();

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Yield();
            await _vm.InitializeAsync();
            ApplyTabVisibility();
        });
    }

    private void ApplyTabVisibility()
    {
        var currentBeforeUpdate = CurrentPage;
        var desiredTabs = new List<Page> { BackTab, DetailsTab };
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
            ConfigureTabPageChrome(page);
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

        var fallbackPage = ResolveFallbackPage(currentBeforeUpdate, desiredTabs);
        if (fallbackPage != null && !ReferenceEquals(CurrentPage, fallbackPage))
            CurrentPage = fallbackPage;

        if (CurrentPage != null && !ReferenceEquals(CurrentPage, BackTab))
            _lastNonBackTab = CurrentPage;

        QueuePlatformTabLayoutRefresh();
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
        if (ReferenceEquals(page, BackTab))
            return;

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
        _appearingRefreshCts?.Cancel();
        try
        {
            _vm.PersistDraft();
        }
        catch (Exception ex)
        {
            RuntimeLog.Write("ADVANCE_PERSIST_DRAFT", "Failed persisting advancement draft during OnDisappearing.", ex);
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Shell.SetNavBarIsVisible(this, false);
        NavigationPage.SetHasNavigationBar(this, false);
        NavigationPage.SetHasBackButton(this, false);
        Shell.SetBackButtonBehavior(this, CreateHiddenBackButtonBehavior());

        foreach (var page in Children)
            ConfigureTabPageChrome(page);

        QueuePlatformTabLayoutRefresh();
        _appearingRefreshCts?.Cancel();
        _appearingRefreshCts = new CancellationTokenSource();
        _ = RefreshAfterAppearingAsync(_appearingRefreshCts.Token);
    }

    private async Task RefreshAfterAppearingAsync(CancellationToken token)
    {
        try
        {
            await Task.Yield();
            if (token.IsCancellationRequested)
                return;

            await _vm.RefreshMultiClassesAsync();
            if (token.IsCancellationRequested)
                return;

            await Task.Yield();
            await _vm.RefreshMultiRaceAsync();
            if (token.IsCancellationRequested)
                return;

            await Task.Yield();
            _vm.RefreshItems();
            ApplyTabVisibility();
        }
        catch (Exception ex)
        {
            RuntimeLog.Write("ADVANCE_APPEAR_REFRESH", "Failed to refresh advancement tabs on appearing.", ex);
        }
    }

    private Page? ResolveFallbackPage(Page? currentBeforeUpdate, IReadOnlyCollection<Page> desiredTabs)
    {
        if (currentBeforeUpdate != null
            && desiredTabs.Contains(currentBeforeUpdate)
            && !ReferenceEquals(currentBeforeUpdate, BackTab))
        {
            return currentBeforeUpdate;
        }

        if (_lastNonBackTab != null && desiredTabs.Contains(_lastNonBackTab))
            return _lastNonBackTab;

        if (desiredTabs.Contains(DetailsTab))
            return DetailsTab;

        return desiredTabs.FirstOrDefault(page => !ReferenceEquals(page, BackTab))
               ?? desiredTabs.FirstOrDefault();
    }

    private void OnCurrentPageChanged(object? sender, EventArgs e)
    {
        if (CurrentPage == null)
            return;

        if (ReferenceEquals(CurrentPage, BackTab))
        {
            _ = HandleBackTabSelectionAsync();
            return;
        }

        _lastNonBackTab = CurrentPage;
    }

    private async Task HandleBackTabSelectionAsync()
    {
        if (_isHandlingBackTabSelection)
            return;

        _isHandlingBackTabSelection = true;
        try
        {
            var returnTab = _lastNonBackTab != null && Children.Contains(_lastNonBackTab)
                ? _lastNonBackTab
                : (Children.Contains(DetailsTab) ? DetailsTab : Children.FirstOrDefault());

            if (returnTab != null && !ReferenceEquals(CurrentPage, returnTab))
                CurrentPage = returnTab;

            if (Navigation.NavigationStack.Count > 1)
            {
                await Navigation.PopAsync();
                return;
            }

            if (Shell.Current != null)
                await Shell.Current.GoToAsync("..");
        }
        finally
        {
            _isHandlingBackTabSelection = false;
        }
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

    private async void OnViewAbilityDetailsClicked(object sender, EventArgs e)
    {
        if (sender is not Microsoft.Maui.Controls.Button button)
            return;

        if (button.CommandParameter is not AbilityEntryVm entry)
            return;

        var ability = await _vm.FindAbilityByNameAsync(entry.Name);
        if (ability == null)
            return;

        await Navigation.PushAsync(new AbilityCardPage(ability));
    }

    private async void OnOpenMultiClassWizardClicked(object sender, EventArgs e)
        => await NavigateWithSectionLoaderAsync(
            MultiClassActionRow,
            () => Navigation.PushAsync(new MultiClassWizardPage(_vm.Draft)));

    private async void OnOpenMultiRaceWizardClicked(object sender, EventArgs e)
        => await NavigateWithSectionLoaderAsync(
            MultiRaceActionRow,
            () => Navigation.PushAsync(new MultiRaceWizardPage(_vm.Draft)));

    private async void OnOpenAbilitySearchClicked(object sender, EventArgs e)
        => await NavigateWithSectionLoaderAsync(
            AbilitiesActionRow,
            () => Navigation.PushAsync(new AdvanceAbilitySearchPage(_vm)));

    private async void OnOpenIspItemCalculatorClicked(object sender, EventArgs e)
        => await NavigateWithSectionLoaderAsync(
            IspItemActionRow,
            () => OpenIspItemCalculatorAsync());

    private async void OnOpenMonsterPointCalculatorClicked(object sender, EventArgs e)
        => await NavigateWithSectionLoaderAsync(
            MpItemActionRow,
            () => OpenMonsterPointCalculatorAsync());

    private async void OnEditItemClicked(object sender, EventArgs e)
    {
        if (sender is not Microsoft.Maui.Controls.Button button
            || button.CommandParameter is not CharacterItemEntryVm entry
            || entry.Item == null)
        {
            return;
        }

        await NavigateWithSectionLoaderAsync(
            IspItemActionRow,
            () => OpenIspItemCalculatorAsync(entry.Item));
    }

    private async void OnViewItemInfoClicked(object sender, EventArgs e)
    {
        if (sender is not Microsoft.Maui.Controls.Button button
            || button.CommandParameter is not CharacterItemEntryVm entry
            || entry.Item == null)
        {
            return;
        }

        await Navigation.PushAsync(new ItemDetailCardPage(entry.Item));
    }

    private async void OnEditMultiRaceClicked(object sender, EventArgs e)
    {
        if (sender is not Microsoft.Maui.Controls.Button button
            || button.CommandParameter is not MultiRaceEntryVm entry)
        {
            return;
        }

        await Navigation.PushAsync(new MultiRaceWizardPage(
            _vm.Draft,
            preselectedMultiRaceKey: entry.Key,
            openDetailStep: true));
    }

    private async void OnRemoveMultiRaceClicked(object sender, EventArgs e)
    {
        if (sender is not Microsoft.Maui.Controls.Button button
            || button.CommandParameter is not MultiRaceEntryVm entry)
        {
            return;
        }

        await _vm.RemoveMultiRaceAsync(entry);
    }

    private async void OnViewMultiRaceInfoClicked(object sender, EventArgs e)
    {
        if (sender is not Microsoft.Maui.Controls.Button button
            || button.CommandParameter is not MultiRaceEntryVm entry)
        {
            return;
        }

        var options = _vm.GetMultiRaceAbilityDetails(entry)
            .Where(option => option.IsValid)
            .ToList();
        if (options.Count == 0)
            return;

        MultiClassAbilityLinkVm? selected = null;
        if (options.Count == 1)
        {
            selected = options[0];
        }
        else
        {
            var labels = options.Select(option => option.DisplayName).ToArray();
            var picked = await DisplayActionSheet(entry.Name, "Cancel", null, labels);
            if (string.IsNullOrWhiteSpace(picked) || picked.Equals("Cancel", StringComparison.OrdinalIgnoreCase))
                return;

            selected = options.FirstOrDefault(option =>
                option.DisplayName.Equals(picked, StringComparison.OrdinalIgnoreCase));
        }

        if (selected == null)
            return;

        var ability = await ResolveAbilityAsync(selected.LookupKey);
        if (ability == null
            && !string.Equals(selected.LookupKey, selected.DisplayName, StringComparison.OrdinalIgnoreCase))
        {
            ability = await ResolveAbilityAsync(selected.DisplayName);
        }

        if (ability == null)
        {
            await DisplayAlert("No Ability Card", $"Could not find a detail card for \"{selected.DisplayName}\".", "OK");
            return;
        }

        await Navigation.PushAsync(new AbilityCardPage(ability));
    }

    private async void OnEditMultiClassClicked(object sender, EventArgs e)
    {
        if (sender is not Microsoft.Maui.Controls.Button button
            || button.CommandParameter is not MultiClassEntryVm entry)
        {
            return;
        }

        await Navigation.PushAsync(new MultiClassWizardPage(
            _vm.Draft,
            preselectedMultiClassKey: entry.Key,
            openDetailStep: true));
    }

    private async void OnRemoveMultiClassClicked(object sender, EventArgs e)
    {
        if (sender is not Microsoft.Maui.Controls.Button button
            || button.CommandParameter is not MultiClassEntryVm entry)
        {
            return;
        }

        await _vm.RemoveMultiClassAsync(entry);
    }

    private async void OnViewMultiClassInfoClicked(object sender, EventArgs e)
    {
        if (sender is not Microsoft.Maui.Controls.Button button
            || button.CommandParameter is not MultiClassEntryVm entry)
        {
            return;
        }

        var options = _vm.GetMultiClassAbilityDetails(entry)
            .Where(option => option.IsValid)
            .ToList();
        if (options.Count == 0)
            return;

        MultiClassAbilityLinkVm? selected = null;
        if (options.Count == 1)
        {
            selected = options[0];
        }
        else
        {
            var labels = options.Select(option => option.DisplayName).ToArray();
            var picked = await DisplayActionSheet(entry.Name, "Cancel", null, labels);
            if (string.IsNullOrWhiteSpace(picked) || picked.Equals("Cancel", StringComparison.OrdinalIgnoreCase))
                return;

            selected = options.FirstOrDefault(option =>
                option.DisplayName.Equals(picked, StringComparison.OrdinalIgnoreCase));
        }

        if (selected == null)
            return;

        var ability = await ResolveAbilityAsync(selected.LookupKey);
        if (ability == null
            && !string.Equals(selected.LookupKey, selected.DisplayName, StringComparison.OrdinalIgnoreCase))
        {
            ability = await ResolveAbilityAsync(selected.DisplayName);
        }

        if (ability == null)
        {
            await DisplayAlert("No Ability Card", $"Could not find a detail card for \"{selected.DisplayName}\".", "OK");
            return;
        }

        await Navigation.PushAsync(new AbilityCardPage(ability));
    }

    private async Task OpenIspItemCalculatorAsync(Item? existingItem = null)
    {
        var existingAbilities = existingItem == null
            ? null
            : ItemDisplayHelper.GetAbilities(existingItem);

        var calculator = new IspCalculator(
            baseTotal: 0,
            existingAbilities: existingAbilities,
            onSave: async result => await SaveIspItemForCharacterAsync(result, existingItem));
        await Navigation.PushAsync(calculator);
    }

    private async Task OpenMonsterPointCalculatorAsync(Item? existingItem = null)
    {
        var calculator = new MpCalculator(async payload =>
            await SaveMonsterPointItemForCharacterAsync(payload, existingItem));
        await Navigation.PushAsync(calculator);
    }

    private async Task SaveIspItemForCharacterAsync(IspCalculationResult result, Item? existingItem)
    {
        var abilities = (result.Abilities ?? new List<CalcResult>())
            .Where(ability => ability != null)
            .ToList();

        await SaveAssignedItemAsync(
            existingItem: existingItem,
            abilities: abilities,
            totalIsp: Math.Max(0, result.TotalIsp),
            sourceFlow: "isp",
            monsterPointCost: 0,
            mpPayload: null);
    }

    private async Task SaveMonsterPointItemForCharacterAsync(MpSubmissionPayload payload, Item? existingItem)
    {
        var abilities = BuildMonsterPointAbilityList(payload);
        await SaveAssignedItemAsync(
            existingItem: existingItem,
            abilities: abilities,
            totalIsp: Math.Max(0, payload.TotalIsp),
            sourceFlow: "monster-point",
            monsterPointCost: Math.Max(0, payload.TotalMp),
            mpPayload: payload);
    }

    private async Task SaveAssignedItemAsync(
        Item? existingItem,
        List<CalcResult> abilities,
        int totalIsp,
        string sourceFlow,
        int monsterPointCost,
        MpSubmissionPayload? mpPayload)
    {
        var item = existingItem ?? new Item
        {
            CreatedDate = DateTime.Now
        };

        var autoPhysicalRep = ItemDisplayHelper.GetAutoPhysicalRep(abilities);
        var physicalRep = autoPhysicalRep.Length > 0
            ? autoPhysicalRep
            : ItemDisplayHelper.GetStoredPhysicalRep(item);

        ApplyAssignedItemState(item, abilities, totalIsp, sourceFlow, monsterPointCost, physicalRep, mpPayload);
        UpsertItem(item);

        if (ItemDisplayHelper.RequiresPhysicalRepPrompt(abilities))
        {
            var enteredPhysicalRep = await DisplayPromptAsync(
                "Physical representation",
                "Add a phys-rep for this item (for example belt, ring, pendant).",
                "Save",
                "Skip",
                initialValue: physicalRep);

            var trimmed = (enteredPhysicalRep ?? string.Empty).Trim();
            if (trimmed.Length > 0 && !trimmed.Equals(physicalRep, StringComparison.OrdinalIgnoreCase))
            {
                physicalRep = trimmed;
                ApplyAssignedItemState(item, abilities, totalIsp, sourceFlow, monsterPointCost, physicalRep, mpPayload);
                UpsertItem(item);
            }
        }

        _vm.RefreshItems();
    }

    private void ApplyAssignedItemState(
        Item item,
        IReadOnlyList<CalcResult> abilities,
        int totalIsp,
        string sourceFlow,
        int monsterPointCost,
        string physicalRep,
        MpSubmissionPayload? mpPayload)
    {
        var displayName = ItemDisplayHelper.BuildDisplayName(abilities, physicalRep);

        item.ItemType = ResolveWalletItemType(abilities);
        item.Maker = BuildMakerCharacter();
        item.Description = BuildAssignedItemDescription(
            displayName: displayName,
            totalIsp: totalIsp,
            abilities: abilities,
            sourceFlow: sourceFlow,
            monsterPointCost: monsterPointCost,
            mpPayload: mpPayload);
        item.Isp = totalIsp;
        item.CreatedDate = item.CreatedDate == default ? DateTime.Now : item.CreatedDate;
        item.AssignedCharacterId = (_vm.Draft.CharacterRecordId ?? string.Empty).Trim();
        item.AssignedCharacterName = (_vm.Draft.Name ?? string.Empty).Trim();
        item.AssignedCharacterPlayerName = (_vm.Draft.PlayerName ?? string.Empty).Trim();
        item.RecipientPlayerName = item.AssignedCharacterPlayerName;
        item.RecipientCharacterName = item.AssignedCharacterName;
        item.RecipientCharacterClass = (_vm.Draft.Class ?? string.Empty).Trim();

        var payload = ItemEmailService.BuildItemPayload(item, abilities);
        payload.Item.DisplayName = displayName;
        payload.Item.PhysicalRepresentation = (physicalRep ?? string.Empty).Trim();
        payload.Item.SourceFlow = (sourceFlow ?? string.Empty).Trim();
        payload.Item.MonsterPointCost = Math.Max(0, monsterPointCost);
        item.PayloadJson = ItemEmailService.SerializeItemPayload(payload);
    }

    private Character BuildMakerCharacter()
    {
        var maker = new Character
        {
            Name = (_vm.Draft.Name ?? string.Empty).Trim(),
            PlayerName = (_vm.Draft.PlayerName ?? string.Empty).Trim(),
            Class = (_vm.Draft.Class ?? string.Empty).Trim(),
            Race = (_vm.Draft.Race ?? string.Empty).Trim()
        };

        maker.Id = (_vm.Draft.CharacterRecordId ?? string.Empty).Trim();

        return maker;
    }

    private static void UpsertItem(Item item)
    {
        if (!LiteDbService.UpdateItem(item))
            LiteDbService.InsertItem(item);
    }

    private static string BuildAssignedItemDescription(
        string displayName,
        int totalIsp,
        IReadOnlyList<CalcResult> abilities,
        string sourceFlow,
        int monsterPointCost,
        MpSubmissionPayload? mpPayload)
    {
        var lines = new List<string>
        {
            $"Name: {displayName}",
            $"Source: {ResolveSourceLabel(sourceFlow)}"
        };

        if (monsterPointCost > 0)
            lines.Add($"MP cost: {monsterPointCost}");

        lines.Add($"ISP total: {totalIsp}");
        lines.Add("ISP breakdown:");
        var abilityLines = abilities
            .Select(ability => (ability?.Summary ?? string.Empty).Trim())
            .Where(text => text.Length > 0)
            .ToList();
        if (abilityLines.Count == 0)
            lines.Add($"Manual ISP entry = {totalIsp}");
        else
            lines.AddRange(abilityLines);

        if (mpPayload?.Breakdown.Count > 0)
        {
            lines.Add("MP breakdown:");
            lines.AddRange(mpPayload.Breakdown
                .Select(row => (row?.Text ?? string.Empty).Trim())
                .Where(text => text.Length > 0));
        }

        return string.Join("\n", lines);
    }

    private static string ResolveSourceLabel(string sourceFlow)
    {
        return (sourceFlow ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "monster-point" => "Monster point builder",
            "isp" => "ISP calculator",
            _ => "Item builder"
        };
    }

    private static ItemTypeEnum ResolveWalletItemType(IEnumerable<CalcResult>? abilities)
    {
        var list = (abilities ?? Enumerable.Empty<CalcResult>()).ToList();
        if (list.Count == 0)
            return ItemTypeEnum.None;

        if (list.Any(ability =>
                (ability.AbilityType ?? string.Empty).Equals("Weapon", StringComparison.OrdinalIgnoreCase)
                || (ability.AbilityType ?? string.Empty).Equals("Shield", StringComparison.OrdinalIgnoreCase)
                || (ability.AbilityType ?? string.Empty).Equals("Armour", StringComparison.OrdinalIgnoreCase)
                || (ability.AbilityType ?? string.Empty).Equals("Armor", StringComparison.OrdinalIgnoreCase)
                || (ability.AbilityType ?? string.Empty).Equals("Physical", StringComparison.OrdinalIgnoreCase)))
        {
            return ItemTypeEnum.Physical;
        }

        if (list.Any(ability =>
                (ability.AbilityType ?? string.Empty).Equals("Spell", StringComparison.OrdinalIgnoreCase)
                || (ability.AbilityType ?? string.Empty).Equals("Magic", StringComparison.OrdinalIgnoreCase)))
        {
            return ItemTypeEnum.Magic;
        }

        if (list.Any(ability =>
                (ability.AbilityType ?? string.Empty).Equals("Miracle", StringComparison.OrdinalIgnoreCase)
                || (ability.AbilityType ?? string.Empty).Equals("Spirit", StringComparison.OrdinalIgnoreCase)))
        {
            return ItemTypeEnum.Spirit;
        }

        if (list.Any(ability =>
                (ability.AbilityType ?? string.Empty).Equals("Evocation", StringComparison.OrdinalIgnoreCase)
                || (ability.AbilityType ?? string.Empty).Equals("Earthpower", StringComparison.OrdinalIgnoreCase)))
        {
            return ItemTypeEnum.EarthPower;
        }

        if (list.Any(ability =>
                (ability.AbilityType ?? string.Empty).Equals("Neuronic", StringComparison.OrdinalIgnoreCase)))
        {
            return ItemTypeEnum.Neuronic;
        }

        return ItemTypeEnum.Other;
    }

    private static List<CalcResult> BuildMonsterPointAbilityList(MpSubmissionPayload payload)
    {
        var abilities = new List<CalcResult>();
        foreach (var row in payload.IspBreakdown ?? new List<ContributionRow>())
        {
            var line = (row?.Text ?? string.Empty).Trim();
            if (line.Length == 0)
                continue;

            abilities.Add(BuildAbilityFromContribution(line));
        }

        if (abilities.Count == 0 && payload.TotalIsp > 0)
        {
            abilities.Add(new CalcResult
            {
                AbilityType = "Base",
                AbilityName = "Monster point item",
                TotalIsp = payload.TotalIsp,
                Summary = $"ISP total: {payload.TotalIsp}",
                Details = new Dictionary<string, object?>
                {
                    ["source"] = "monster-point"
                }
            });
        }

        return abilities;
    }

    private static CalcResult BuildAbilityFromContribution(string line)
    {
        var summary = (line ?? string.Empty).Trim();
        var label = summary;
        var equalsIndex = summary.IndexOf('=');
        if (equalsIndex > 0)
            label = summary[..equalsIndex].Trim();

        var abilityType = "MonsterPoint";
        var abilityName = label;
        if (label.StartsWith("Life ", StringComparison.OrdinalIgnoreCase))
        {
            abilityType = "Life";
            abilityName = label[5..].Trim();
        }
        else if (label.Contains("shield", StringComparison.OrdinalIgnoreCase))
        {
            abilityType = "Shield";
            abilityName = "Shield";
        }
        else if (label.Contains("armour", StringComparison.OrdinalIgnoreCase)
                 || label.Contains("armor", StringComparison.OrdinalIgnoreCase))
        {
            abilityType = "Armour";
            abilityName = "Armour";
        }
        else if (label.Contains("weapon", StringComparison.OrdinalIgnoreCase))
        {
            abilityType = "Weapon";
            abilityName = "Weapon";
        }

        return new CalcResult
        {
            AbilityType = abilityType,
            AbilityName = abilityName,
            TotalIsp = ParseContributionCost(summary),
            Summary = summary,
            Details = new Dictionary<string, object?>
            {
                ["source"] = "monster-point"
            }
        };
    }

    private static int ParseContributionCost(string line)
    {
        var match = ContributionCostRegex.Match(line ?? string.Empty);
        if (!match.Success)
            return 0;

        return int.TryParse(match.Groups["cost"].Value, out var cost) ? cost : 0;
    }

    private static async Task NavigateWithSectionLoaderAsync(
        AdvanceSectionActionRow? row,
        Func<Task> navigationAction)
    {
        if (row != null)
        {
            row.IsLoading = true;
            await Task.Yield();
            await Task.Delay(1);
        }

        try
        {
            await navigationAction();
        }
        finally
        {
            if (row != null)
                row.IsLoading = false;
        }
    }

    private static async Task<EvolutionService.AbilityResult?> ResolveAbilityAsync(string key)
    {
        var byIndex = await AbilityDetailsLookupService.FindByIndexAsync(key);
        if (byIndex != null)
            return byIndex;

        var fromSpecialisation = await DetailCardLookupService.FindSpecialisationAbilityAsync(key);
        if (fromSpecialisation.Ability == null)
            return null;

        return ToAbilityResult(fromSpecialisation.Ability, fromSpecialisation.Key, key);
    }

    private static EvolutionService.AbilityResult ToAbilityResult(
        AbilityDefinition source,
        string resolvedKey,
        string requestedKey)
    {
        var name = (source.Name ?? string.Empty).Trim();
        if (name.Length == 0)
            name = (resolvedKey ?? string.Empty).Trim();
        if (name.Length == 0)
            name = (requestedKey ?? string.Empty).Trim();
        if (name.Length == 0)
            name = "Ability";

        return new EvolutionService.AbilityResult
        {
            Index = name,
            Description = source.Effect ?? string.Empty,
            Cost = 0,
            Table = 0,
            Available = source.Source ?? "ALL",
            CanBuyMultiple = false,
            PreReqs = source.PreReqs is { Count: > 0 } preReqs
                ? preReqs
                : Array.Empty<string>(),
            MaxAvailable = source.Count
        };
    }

    private static void ConfigureTabPageChrome(Page page)
    {
        Shell.SetNavBarIsVisible(page, false);
        NavigationPage.SetHasNavigationBar(page, false);
        NavigationPage.SetHasBackButton(page, false);
        Shell.SetBackButtonBehavior(page, CreateHiddenBackButtonBehavior());
    }

    private static BackButtonBehavior CreateHiddenBackButtonBehavior()
        => new() { IsVisible = false };

    private void QueuePlatformTabLayoutRefresh()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(10);
            ApplyPlatformTabLayoutTweaks();
            await Task.Delay(60);
            ApplyPlatformTabLayoutTweaks();
        });
    }

    partial void ApplyPlatformTabLayoutTweaks();
}
