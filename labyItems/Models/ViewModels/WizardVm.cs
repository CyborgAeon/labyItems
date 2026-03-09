using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using labyItems.Controls;
using labyItems.Helpers;
using labyItems.Models.Characters;
using labyItems.Pages.Characters;
using labyItems.Services;
using Microsoft.Maui.ApplicationModel;

namespace labyItems.Pages.Characters.ViewModels;

public sealed class WizardVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }

    public CharacterDraft Draft { get; }
    private readonly IBattleboardExportService _battleboardExportService;
    private readonly IExportService _exportService;
    private readonly ICharacterDraftStore _draftStore;
    private readonly ICharacterAdvancementDomainService _domainService;
    private readonly ICharacterCreationDataService _creationDataService;
    private readonly Func<Task>? _onFinished;
    private readonly WizardFlowStateMachine _flow;

    public ObservableCollection<StepItem> StepSteps { get; } = new();

    public ObservableCollection<int?> ArmourLayers { get; } = new() { 0 };

    private int _currentStep = -1;
    public int CurrentStep
    {
        get => _currentStep;
        private set
        {
            if (_currentStep == value) return;
            _currentStep = value;
            UpdateStepView();
            Raise(nameof(CurrentStep));
            Raise(nameof(CanGoBack));
            Raise(nameof(CanGoNext));
            Raise(nameof(NextButtonText));
            Raise(nameof(ShowContinueToAdvancement));
        }
    }

    private View? _currentStepView;
    public View? CurrentStepView
    {
        get => _currentStepView;
        private set => Set(ref _currentStepView, value);
    }

    public bool CanGoBack
    {
        get
        {
            if (IsRaceClassStep && CharacterBuilderVm.IsRaceTabSelected)
                return true;

            return CurrentStep > 0 || CanExitWizard();
        }
    }

    public bool CanGoNext
    {
        get
        {
            if (IsRaceClassStep)
            {
                if (CharacterBuilderVm.IsClassTabSelected)
                    return CharacterBuilderVm.CanSelectRace;

                if (CurrentStep >= StepSteps.Count - 1)
                    return true;

                return _flow.CanEnter(CurrentStep + 1);
            }

            if (CurrentStep >= StepSteps.Count - 1) return true;
            return _flow.CanEnter(CurrentStep + 1);
        }
    }

    public string NextButtonText => CurrentStep == StepSteps.Count - 1 ? "Save" : "Next";
    public bool ShowContinueToAdvancement => CurrentStep == StepSteps.Count - 1;
    public ICommand BackCommand { get; }
    public ICommand NextCommand { get; }
    public Command<int> StepClickCommand { get; }
    public Command ExportToBattleboardCommand { get; }
    public Command ExportToExcelCommand { get; }
    public Command SaveToWalletCommand { get; }
    public ICommand ContinueToAdvancementCommand { get; }
    public ICommand ToggleAdvancementExpandedCommand { get; }
    public CharacterBuilderVm CharacterBuilderVm { get; }
    public GuildsVm GuildsVm { get; }
    private int _armourMaxBasePac;
    private int _armourMaxTotalPac;
    private int _wornArmourPac;
    private bool _isArmourSelectionEnabled = true;
    private List<int> _armourPacSteps = new();
    private IDictionary<string, int> _armourPacItems = new Dictionary<string, int>();
    private IList<string> _armourPacLabels = new List<string>();
    private bool _isAdvancementExpanded;
    private int _advancementPointsSpent;
    private bool _isBackNavigationInProgress;
    private readonly Dictionary<string, int> _abilityCostIndex = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, EvolutionService.AbilityResult> _specialisationAbilityLookup =
        new Dictionary<string, EvolutionService.AbilityResult>(StringComparer.OrdinalIgnoreCase);
    private bool _isLoadingSpecialisationAbilityLookup;
    private IReadOnlyList<SpecialisationSummaryLineVm> _specialisationSummaryLines = Array.Empty<SpecialisationSummaryLineVm>();

    public ObservableCollection<AbilitySpendLine> AdvancementAbilityLines { get; } = new();

    private bool IsRaceClassStep => CurrentStep == 0;

    public bool IsAdvancementExpanded
    {
        get => _isAdvancementExpanded;
        set => Set(ref _isAdvancementExpanded, value);
    }

    public int AdvancementPointsSpent
    {
        get => _advancementPointsSpent;
        private set => Set(ref _advancementPointsSpent, value);
    }

    public WizardVm(
        CharacterDraft? draft = null,
        Func<Task>? onFinished = null,
        IBattleboardExportService? battleboardExportService = null,
        IExportService? exportService = null,
        ICharacterDraftStore? draftStore = null,
        ICharacterAdvancementDomainService? domainService = null,
        ICharacterCreationDataService? creationDataService = null)
    {
        var resolvedDraft = draftStore?.Draft ?? draft ?? new CharacterDraft();
        _draftStore = draftStore ?? new CharacterDraftStore(resolvedDraft);
        Draft = _draftStore.Draft;
        _onFinished = onFinished;
        _battleboardExportService = battleboardExportService
            ?? ServiceHelper.ResolveService<IBattleboardExportService>()
            ?? new BattleboardExportService();
        _exportService = exportService
            ?? ServiceHelper.ResolveService<IExportService>()
            ?? new ExportService(
                new MauiClipboardService(),
                new MauiLauncherService(),
                new MauiShareService());
        _domainService = domainService
            ?? ServiceHelper.ResolveService<ICharacterAdvancementDomainService>()
            ?? new CharacterAdvancementDomainService();
        _creationDataService = creationDataService
            ?? ServiceHelper.ResolveService<ICharacterCreationDataService>()
            ?? new CharacterCreationDataService();
        BackCommand = new Command(async () => await OnBackAsync());
        NextCommand = new Command(async () => await OnNextAsync());
        StepClickCommand = new Command<int>(async i => await TryGoToStepAsync(i));
        ExportToBattleboardCommand = new Command(async () => await ExportBattleboardAsync(), () => Draft.IsRaceAndClassSelected);
        ExportToExcelCommand = new Command(async () => await ExportBattleboardToExcelAsync(), () => Draft.IsRaceAndClassSelected);
        SaveToWalletCommand = new Command(SaveToWallet, () => Draft.IsRaceAndClassSelected);
        ContinueToAdvancementCommand = new Command(async () => await ContinueToAdvancementAsync());
        ToggleAdvancementExpandedCommand = new Command(() => IsAdvancementExpanded = !IsAdvancementExpanded);
        CharacterBuilderVm = new CharacterBuilderVm(Draft, NotifyGatingChanged, _creationDataService);

        GuildsVm = new GuildsVm(
            Draft,
            NotifyGatingChanged,
            CharacterBuilderVm.GetNonGuildAlignmentRules,
            CharacterBuilderVm.RefreshDraftAbilitiesAsync,
            _creationDataService,
            useMultiTypeFilters: true);

        _flow = new WizardFlowStateMachine(BuildSteps());
        foreach (var step in _flow.Steps)
            StepSteps.Add(step.StepItem);

        UpdateArmourUiFromDraft();

        CurrentStep = _flow.CurrentStep;
        RefreshSpecialisationSummaryLines();
        _ = EnsureSpecialisationAbilityLookupLoadedAsync();

        MainThread.BeginInvokeOnMainThread(async () => await SyncDraftStateAsync(allowBackground: true));
    }

    private IReadOnlyList<WizardStepDefinition> BuildSteps()
        => new List<WizardStepDefinition>
        {
            new(
                index: 0,
                label: "Race/Class",
                canEnter: () => true,
                createView: () => new CharacterBuilder(CharacterBuilderVm)),
            new(
                index: 1,
                label: "Specialise",
                canEnter: () => WizardStepRules.CanEnterSpecialisation(Draft),
                createView: () => new CharacterSpecialisation(CharacterBuilderVm)),
            new(
                index: 2,
                label: "Guilds",
                canEnter: () => WizardStepRules.CanEnterGuilds(Draft, CharacterBuilderVm.SpecialisationVm),
                createView: () => new Guilds(GuildsVm)
                {
                    UseTypePills = true
                },
                onEnterAsync: async () => await GuildsVm.ReloadAsync()),
            new(
                index: 3,
                label: "Details",
                canEnter: () => WizardStepRules.CanEnterDetails(Draft, CharacterBuilderVm.SpecialisationVm, GuildsVm),
                createView: () => new CharacterInfoView(this),
                onEnterAsync: () =>
                {
                    UpdateArmourUiFromDraft();
                    return Task.CompletedTask;
                }),
            new(
                index: 4,
                label: "Review",
                canEnter: () => WizardStepRules.CanEnterReview(Draft, CharacterBuilderVm.SpecialisationVm, GuildsVm),
                createView: () => new CharacterReviewView(this))
        };

    public async Task RefreshReviewAsync()
    {
        await SyncDraftStateAsync();
        RaiseReviewProperties();
    }

    public string PlayerName
    {
        get => Draft.PlayerName;
        set
        {
            if (Draft.PlayerName == value) return;
            Draft.PlayerName = value;
            Raise(nameof(PlayerName));
            RaiseReviewProperties();
        }
    }

    public string CharacterName
    {
        get => Draft.Name;
        set
        {
            if (Draft.Name == value) return;
            Draft.Name = value;
            Raise(nameof(CharacterName));
            RaiseReviewProperties();
        }
    }

    public string NotesText
    {
        get => Draft.Notes;
        set
        {
            if (Draft.Notes == value) return;
            Draft.Notes = value;
            Raise(nameof(NotesText));
            RaiseReviewProperties();
        }
    }

    public IEnumerable<Alignment> AlignmentOptions => Draft.AvailableAlignments;

    public Alignment? SelectedAlignment
    {
        get => Draft.Alignment;
        set
        {
            if (Draft.Alignment == value) return;
            Draft.Alignment = value;
            Raise(nameof(SelectedAlignment));
            RaiseReviewProperties();
        }
    }

    public string PlayerNameSummary => string.IsNullOrWhiteSpace(Draft.PlayerName)
        ? "Player: not set"
        : $"Player: {Draft.PlayerName}";
    public string CharacterNameSummary => string.IsNullOrWhiteSpace(Draft.Name)
        ? "Character: not set"
        : $"Character: {Draft.Name}";
    public string RaceSummary => string.IsNullOrWhiteSpace(Draft.Race)
        ? "Race: not selected"
        : $"Race: {Draft.Race}";
    public string RaceSubtypeSummary
    {
        get
        {
            var subtype = Draft.RaceSubtypeValue ?? Draft.RaceSubtype;
            return string.IsNullOrWhiteSpace(subtype)
                ? "Subtype: none selected"
                : $"Subtype: {subtype}";
        }
    }

    public string ClassSummary => string.IsNullOrWhiteSpace(Draft.Class)
        ? "Class: not selected"
        : $"Class: {Draft.Class}";

    public string GuildSummary => Draft.Guilds.Count == 0
        ? "Guilds: none selected"
        : $"Guilds: {string.Join(", ", Draft.Guilds)}";

    public IReadOnlyList<SpecialisationSummaryLineVm> SpecialisationSummaryLines => _specialisationSummaryLines;

    public string SpecialisationSummaryHeader => SpecialisationSummaryLines.Count > 0
        ? "Chosen options:"
        : "No specialisations selected.";

    public string NotesSummary => string.IsNullOrWhiteSpace(Draft.Notes)
        ? "No notes provided."
        : Draft.Notes;

    public string AdvancementPointsSummary => $"Spent {AdvancementPointsSpent} / {Draft.Points} Pts";
    public string AdvancementPointsAccruedSummary => $"Points accrued: {Draft.Points}";
    public string AdvancementVitaeSummary => Draft.HasSetCurrentVitae
        ? $"Current vitae: {Draft.CurrentVitae}%"
        : "Current vitae: not set";
    public string AdvancementItemsSummary => Draft.AdvancementItems.Count == 0
        ? "Items: none."
        : $"Items: {string.Join(", ", Draft.AdvancementItems)}";
    public string AdvancementNotesSummary => string.IsNullOrWhiteSpace(Draft.Notes)
        ? "Notes: none."
        : $"Notes: {Draft.Notes}";
    public string AdvancementAbilitiesHeader => AdvancementAbilityLines.Count == 0
        ? "Abilities: none."
        : "Abilities";

    public string AlignmentSummary => Draft.Alignment.HasValue
        ? $"Alignment: {Draft.Alignment}"
        : "Alignment: not selected";
    public int ArmourMaxBasePac
    {
        get => _armourMaxBasePac;
        private set => Set(ref _armourMaxBasePac, value);
    }

    public int ArmourMaxTotalPac
    {
        get => _armourMaxTotalPac;
        private set => Set(ref _armourMaxTotalPac, value);
    }

    public int WornArmourPac
    {
        get => _wornArmourPac;
        set
        {
            var clamped = ClampWornArmour(value);
            if (!Set(ref _wornArmourPac, clamped)) return;
            Raise(nameof(ArmourSelectionSummary));
            Raise(nameof(ArmourSliderIndex));
        }
    }

    public bool IsArmourSelectionEnabled
    {
        get => _isArmourSelectionEnabled;
        private set => Set(ref _isArmourSelectionEnabled, value);
    }

    public string ArmourAvailabilityText => BuildArmourAvailabilityText();
    public string ArmourBaseSummary => BuildArmourBaseSummary();
    public string ArmourSelectionSummary => $"Worn armour PAC: {WornArmourPac}";

    public IDictionary<string, int> ArmourPacItems
    {
        get => _armourPacItems;
        private set => Set(ref _armourPacItems, value);
    }

    public IList<string> ArmourPacLabels
    {
        get => _armourPacLabels;
        private set => Set(ref _armourPacLabels, value);
    }

    public int ArmourSliderIndex
    {
        get => ResolveArmourSliderIndex();
        set => ApplyArmourSliderIndex(value);
    }

    public void NotifyGatingChanged()
    {
        UpdateArmourUiFromDraft();
        Raise(nameof(CanGoNext));
        Raise(nameof(NextButtonText));
        Raise(nameof(AlignmentOptions));
        Raise(nameof(SelectedAlignment));
        Raise(nameof(ArmourAvailabilityText));
        Raise(nameof(ArmourBaseSummary));
        Raise(nameof(ArmourSelectionSummary));
        RaiseReviewProperties();
        ExportToBattleboardCommand?.ChangeCanExecute();
        ExportToExcelCommand?.ChangeCanExecute();
        SaveToWalletCommand?.ChangeCanExecute();
    }

    private async Task OnBackAsync()
    {
        if (_isBackNavigationInProgress)
            return;

        _isBackNavigationInProgress = true;
        try
        {
            if (IsRaceClassStep && CharacterBuilderVm.IsRaceTabSelected)
            {
                if (CharacterBuilderVm.TryMoveToClassSelection())
                {
                    Raise(nameof(CanGoBack));
                    Raise(nameof(CanGoNext));
                }

                return;
            }

            if (CurrentStep > 0)
            {
                await TryGoToStepAsync(CurrentStep - 1);
                return;
            }

            await TryExitWizardAsync();
        }
        finally
        {
            _isBackNavigationInProgress = false;
        }
    }

    private async Task OnNextAsync()
    {
        if (IsRaceClassStep && CharacterBuilderVm.IsClassTabSelected)
        {
            if (CharacterBuilderVm.TryMoveToRaceSelection())
            {
                Raise(nameof(CanGoBack));
                Raise(nameof(CanGoNext));
            }

            return;
        }

        if (CurrentStep == StepSteps.Count - 1)
        {
            await SyncDraftStateAsync();
            SaveToWallet();
            if (_onFinished != null)
                await _onFinished();
            return;
        }

        await TryGoToStepAsync(CurrentStep + 1);
    }

    private bool CanExitWizard()
    {
        var nav = ResolveNavigation();
        return nav?.NavigationStack?.Count > 1;
    }

    private async Task<bool> TryExitWizardAsync()
    {
        var nav = ResolveNavigation();
        if (nav?.NavigationStack?.Count > 1)
        {
            await nav.PopAsync();
            return true;
        }

        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync("..");
            return true;
        }

        return false;
    }

    private static INavigation? ResolveNavigation()
    {
        if (Shell.Current != null)
            return Shell.Current.Navigation;

        return Application.Current?.MainPage?.Navigation;
    }

    private async Task ContinueToAdvancementAsync()
    {
        if (CurrentStep != StepSteps.Count - 1)
            return;

        await SyncDraftStateAsync();
        SaveToWallet();
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var nav = Application.Current?.MainPage?.Navigation;
            if (nav == null)
                return;
            await nav.PushAsync(new AdvanceCharacterPage(Draft));
        });
    }

    private async Task SyncDraftStateAsync(bool allowBackground = false)
    {
        ClampWornArmourPac();
        Draft.WornArmour = ClampWornArmour(WornArmourPac);

        if (allowBackground)
        {
            await Task.Run(async () =>
            {
                await CharacterBuilderVm.SyncDraftLifeAsync().ConfigureAwait(false);
                await CharacterBuilderVm.RefreshDraftAbilitiesAsync().ConfigureAwait(false);
                await EnsureAbilityCostIndexAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(ApplyAdvancementSummary);
            return;
        }

        await CharacterBuilderVm.SyncDraftLifeAsync();
        await CharacterBuilderVm.RefreshDraftAbilitiesAsync();
        await EnsureAbilityCostIndexAsync();
        if (MainThread.IsMainThread)
            ApplyAdvancementSummary();
        else
            await MainThread.InvokeOnMainThreadAsync(ApplyAdvancementSummary);
    }

    private async Task TryGoToStepAsync(int targetIndex)
    {
        if (targetIndex == CurrentStep) return;

        var movingForward = targetIndex > CurrentStep;
        var moved = await _flow.TryTransitionAsync(targetIndex, deferEnter: movingForward);
        if (!moved) return;

        CurrentStep = _flow.CurrentStep;

        if (movingForward)
        {
            var deferredEnter = _flow.ConsumePendingEnter();
            _ = RunPostTransitionSyncAsync(deferredEnter);
        }
    }

    private async Task RunPostTransitionSyncAsync(Func<Task>? deferredEnter)
    {
        try
        {
            await Task.Yield();
            await Task.Delay(220);
            await SyncDraftStateAsync(allowBackground: true);
            if (deferredEnter != null)
                await deferredEnter();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Wizard post-transition sync failed: {ex}");
        }
    }

    private void UpdateStepView()
    {
        if (CurrentStep < 0 || CurrentStep >= _flow.Steps.Count)
        {
            CurrentStepView = BuildPlaceholder("Unknown step");
            return;
        }

        CurrentStepView = _flow.Steps[CurrentStep].CreateView();
    }

    private static View BuildPlaceholder(string text)
        => new ContentView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16,
                Children =
                {
                    new Label { Text = text, FontAttributes = FontAttributes.Bold, FontSize = 18 },
                    new Label { Text = "Wire this step into the wizard the same way as CharacterBuilder.", Opacity = 0.7 }
                }
            }
        };

    private IReadOnlyList<SpecialisationSummaryLineVm> BuildSpecialisationSummary()
    {
        var lines = new List<SpecialisationSummaryLineVm>();
        var includedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var subtype = Draft.RaceSubtypeValue ?? Draft.RaceSubtype;
        if (!string.IsNullOrWhiteSpace(subtype))
            lines.Add(new SpecialisationSummaryLineVm($"Subtype: {subtype}", null));

        var specVm = CharacterBuilderVm?.SpecialisationVm;
        if (specVm != null)
        {
            foreach (var (group, slot) in LoopHelper.Flatten(specVm.Groups, g => g.Slots))
            {
                if (group == null || slot == null || !slot.HasSelection)
                    continue;

                var selection = FormatSlotSelection(slot);
                if (string.IsNullOrWhiteSpace(selection))
                    continue;

                var multipleSlots = group.Slots.Count > 1;
                var lineText = multipleSlots
                    ? $"{group.Title} ({slot.LevelLabel}): {selection}"
                    : $"{group.Title}: {selection}";
                lines.Add(CreateSpecialisationSummaryLine(lineText, selection));
                includedKeys.Add(group.Title);
            }

            foreach (var mapped in specVm.MappedSpecialisations)
            {
                if (mapped == null)
                    continue;

                var picked = (mapped.SelectedOption ?? string.Empty).Trim();
                if (picked.Length == 0)
                    continue;

                lines.Add(CreateSpecialisationSummaryLine($"{mapped.Title}: {picked}", picked));
                includedKeys.Add(mapped.Key);
            }
        }

        foreach (var kvp in Draft.SpecialisationSelections.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (includedKeys.Contains(kvp.Key))
                continue;

            lines.Add(CreateSpecialisationSummaryLine($"{kvp.Key}: {kvp.Value}", kvp.Value));
        }

        return lines;
    }

    private SpecialisationSummaryLineVm CreateSpecialisationSummaryLine(string text, string? potentialAbilityIndex)
    {
        EvolutionService.AbilityResult? ability = null;
        if (_specialisationAbilityLookup.Count > 0)
            ability = AbilityDetailsLookupService.FindByIndex(_specialisationAbilityLookup, potentialAbilityIndex);

        return new SpecialisationSummaryLineVm(text, ability);
    }

    private void RefreshSpecialisationSummaryLines()
    {
        _specialisationSummaryLines = BuildSpecialisationSummary();
        Raise(nameof(SpecialisationSummaryLines));
        Raise(nameof(SpecialisationSummaryHeader));
    }

    private async Task EnsureSpecialisationAbilityLookupLoadedAsync()
    {
        if (_specialisationAbilityLookup.Count > 0 || _isLoadingSpecialisationAbilityLookup)
            return;

        _isLoadingSpecialisationAbilityLookup = true;
        try
        {
            var lookup = await AbilityDetailsLookupService.GetLookupAsync();
            if (lookup.Count == 0)
                return;

            _specialisationAbilityLookup = lookup;

            if (MainThread.IsMainThread)
                RefreshSpecialisationSummaryLines();
            else
                await MainThread.InvokeOnMainThreadAsync(RefreshSpecialisationSummaryLines);
        }
        catch
        {
            // Ignore lookup failures and keep showing text-only lines.
        }
        finally
        {
            _isLoadingSpecialisationAbilityLookup = false;
        }
    }

    private static string FormatSlotSelection(SpecialisationSlotVm slot)
    {
        var baseName = slot.IsLocked
            ? (slot.ForcedAbilityDefinition?.Name ?? slot.LockedDisplayText)
            : (slot.SelectedOption ?? string.Empty).Trim();

        var custom = (slot.CustomisationValue ?? string.Empty).Trim();
        if (custom.Length == 0)
            return baseName;

        if (baseName.Length == 0)
            return custom;

        if (baseName.Contains(custom, StringComparison.OrdinalIgnoreCase))
            return baseName;

        return $"{baseName} ({custom})";
    }

    private void UpdateArmourUiFromDraft()
    {
        var tier = GetArmourTierFromDraft();
        var basePac = GetMaxPacForTier(tier);
        ArmourMaxBasePac = basePac;
        ArmourMaxTotalPac = basePac > 0 ? GetMaxTotalPacForTier(tier) : 0;
        IsArmourSelectionEnabled = basePac > 0 && tier != ArmourTier.None;

        EnsureArmourLayersInitialized();
        ClampArmourLayers(tier);
        ClampWornArmourPac();
        if (!IsArmourSelectionEnabled)
        {
            if (ArmourLayers.Count > 0)
                ArmourLayers[0] = 0;
            WornArmourPac = 0;
        }
        UpdateArmourSliderOptions(tier);
        Raise(nameof(ArmourSelectionSummary));
    }

    private void EnsureArmourLayersInitialized()
    {
        if (ArmourLayers.Count == 0)
            ArmourLayers.Add(0);
    }

    private void ClampArmourLayers(ArmourTier tier)
    {
        var allowedLayers = GetMaxLayersForTier(tier);

        while (ArmourLayers.Count > allowedLayers)
            ArmourLayers.RemoveAt(ArmourLayers.Count - 1);

        var maxPac = ArmourMaxBasePac;
        for (int i = 0; i < ArmourLayers.Count; i++)
        {
            if (ArmourLayers[i].HasValue && ArmourLayers[i]!.Value > maxPac)
                ArmourLayers[i] = maxPac;
        }
    }

    private int ClampWornArmour(int value)
    {
        if (_armourPacSteps.Count > 0)
        {
            var sorted = _armourPacSteps;
            if (sorted.Contains(value))
                return value;

            var lower = sorted.Where(v => v <= value).DefaultIfEmpty(sorted[0]).Max();
            return lower;
        }

        var max = Math.Max(0, ArmourMaxTotalPac);
        return Math.Max(0, Math.Min(value, max));
    }

    private void ClampWornArmourPac()
    {
        var clamped = ClampWornArmour(_wornArmourPac);
        if (_wornArmourPac != clamped)
        {
            _wornArmourPac = clamped;
            Raise(nameof(WornArmourPac));
            Raise(nameof(ArmourSelectionSummary));
            Raise(nameof(ArmourSliderIndex));
        }
    }

    private int ResolveArmourSliderIndex()
    {
        if (_armourPacSteps.Count == 0)
            return 0;

        var idx = _armourPacSteps.IndexOf(WornArmourPac);
        return idx >= 0 ? idx : 0;
    }

    private void ApplyArmourSliderIndex(int index)
    {
        if (_armourPacSteps.Count == 0)
            return;

        var clamped = Math.Clamp(index, 0, _armourPacSteps.Count - 1);
        var pac = _armourPacSteps[clamped];
        if (WornArmourPac != pac)
            WornArmourPac = pac;

        Raise(nameof(ArmourSliderIndex));
    }

    private void UpdateArmourSliderOptions(ArmourTier tier)
    {
        var maxPac = GetMaxPacForTier(tier);
        var steps = new List<int> { 0 };

        if (maxPac >= 3)
        {
            for (int pac = 3; pac <= maxPac; pac++)
                steps.Add(pac);
        }

        _armourPacSteps = steps;
        ArmourPacItems = steps.ToDictionary(p => p.ToString(), p => p);
        ArmourPacLabels = steps.Select(BuildArmourLabel).ToList();

        if (!_armourPacSteps.Contains(WornArmourPac))
            WornArmourPac = _armourPacSteps.LastOrDefault();

        Raise(nameof(ArmourSliderIndex));
    }

    private static string BuildArmourLabel(int pac)
        => pac switch
        {
            0 => "None",
            3 => "3 PAC, Leather",
            4 => "4 PAC, stiff leather",
            5 => "5 PAC, studded leather",
            6 => "6 PAC, Loose Chain",
            7 => "7 PAC, Tight Chain",
            8 => "8 PAC, Plate mail",
            _ => $"{pac} PAC"
        };

    private string BuildArmourAvailabilityText()
    {
        var tier = GetArmourTierFromDraft();
        if (tier == ArmourTier.None)
            return "Armour: none allowed.";

        var layers = GetMaxLayersForTier(tier);
        return $"Armour: {tier}";
    }

    private string BuildArmourBaseSummary()
    {
        var pac = Draft.ClassRaceArmour;
        var dac = Draft.DAC;
        var mac = Draft.MAC ?? 0;
        var sac = Draft.SAC ?? 0;

        if (pac == 0 && dac == 0 && mac == 0 && sac == 0)
            return "Base armour from class/race/spec: none.";

        return $"Base armour from class/race/spec: PAC {pac}, DAC {dac}, MAC {mac}, SAC {sac}";
    }

    private void RaiseReviewProperties()
    {
        Raise(nameof(PlayerNameSummary));
        Raise(nameof(CharacterNameSummary));
        Raise(nameof(RaceSummary));
        Raise(nameof(RaceSubtypeSummary));
        Raise(nameof(ClassSummary));
        Raise(nameof(GuildSummary));
        RefreshSpecialisationSummaryLines();
        _ = EnsureSpecialisationAbilityLookupLoadedAsync();
        Raise(nameof(NotesSummary));
        Raise(nameof(AlignmentSummary));
        Raise(nameof(AdvancementPointsSummary));
        Raise(nameof(AdvancementPointsAccruedSummary));
        Raise(nameof(AdvancementVitaeSummary));
        Raise(nameof(AdvancementItemsSummary));
        Raise(nameof(AdvancementNotesSummary));
        Raise(nameof(AdvancementAbilitiesHeader));
    }

    private ArmourTier GetArmourTierFromDraft()
    {
        var text = (Draft.ArmourAvailability ?? string.Empty).Trim();
        if (text.Length > 0 && Enum.TryParse<ArmourTier>(text, ignoreCase: true, out var parsed))
            return parsed;

        return ArmourTier.None;
    }

    private static int GetMaxPacForTier(ArmourTier tier)
        => tier switch
        {
            ArmourTier.Light => 4,
            ArmourTier.Medium => 6,
            ArmourTier.Heavy => 8,
            _ => 0
        };

    private static int GetMaxTotalPacForTier(ArmourTier tier)
        => tier switch
        {
            ArmourTier.Light => GetMaxPacForTier(tier),
            ArmourTier.Medium => GetMaxPacForTier(tier) + 1,
            ArmourTier.Heavy => GetMaxPacForTier(tier) + 3,
            _ => 0
        };

    private static int GetMaxLayersForTier(ArmourTier tier)
        => tier switch
        {
            ArmourTier.Light => 1,
            ArmourTier.Medium => 2,
            ArmourTier.Heavy => 3,
            _ => 1
        };

    private async Task ExportBattleboardAsync()
    {
        await SyncDraftStateAsync();
        var path = await _battleboardExportService.ExportAsync(Draft);
        await _exportService.ShareFileAsync($"{Draft.Name}'s battleboard", path);
    }

    private async Task ExportBattleboardToExcelAsync()
    {
        await SyncDraftStateAsync();
        var path = await _battleboardExportService.ExportAsync(Draft);
        await _exportService.OpenFileAsync(path);
    }

    private void SaveToWallet()
    {
        _draftStore.Save();
        RaiseReviewProperties();
    }

    private async Task EnsureAbilityCostIndexAsync()
    {
        if (_abilityCostIndex.Count > 0)
            return;

        var abilities = await ManuAbilityService.GetAllAsync();
        foreach (var entry in abilities)
        {
            var name = (entry.name ?? string.Empty).Trim();
            if (name.Length == 0)
                continue;
            if (!_abilityCostIndex.ContainsKey(name))
                _abilityCostIndex[name] = entry.cost;
        }
    }

    private void ApplyAdvancementSummary()
    {
        AdvancementAbilityLines.Clear();
        var lines = _domainService.BuildAbilityPointSpendLines(
            Draft.AdvancementAbilities ?? new List<string>(),
            _abilityCostIndex);
        foreach (var line in lines)
        {
            AdvancementAbilityLines.Add(new AbilitySpendLine(
                line.Name,
                line.Cost,
                line.RunningTotal));
        }

        AdvancementPointsSpent = _domainService.ComputeAbilityPointsSpent(
            Draft.AdvancementAbilities ?? new List<string>(),
            _abilityCostIndex);
        RaiseReviewProperties();
    }

    private async Task RefreshAdvancementSummaryAsync()
    {
        await EnsureAbilityCostIndexAsync();
        if (MainThread.IsMainThread)
            ApplyAdvancementSummary();
        else
            await MainThread.InvokeOnMainThreadAsync(ApplyAdvancementSummary);
    }

    public sealed class AbilitySpendLine
    {
        public string Name { get; }
        public int Cost { get; }
        public int RunningTotal { get; }
        public string NameWithCost => $"{Name} ({Cost})";
        public string RunningTotalText => $"{RunningTotal}";

        public AbilitySpendLine(string name, int cost, int runningTotal)
        {
            Name = name;
            Cost = cost;
            RunningTotal = runningTotal;
        }
    }

    public sealed class SpecialisationSummaryLineVm
    {
        public string Text { get; }
        public EvolutionService.AbilityResult? Ability { get; }
        public bool HasAbilityDetails => Ability != null;

        public SpecialisationSummaryLineVm(string text, EvolutionService.AbilityResult? ability)
        {
            Text = text;
            Ability = ability;
        }
    }

    private enum ArmourTier
    {
        None = 0,
        Light = 1,
        Medium = 2,
        Heavy = 3
    }
}
