using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using labyItems.Controls;
using labyItems.Helpers;
using labyItems.Models.Abilities;
using labyItems.Models.Characters;
using labyItems.Models.ViewModels;
using labyItems.Pages.Characters;
using labyItems.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.Communication;

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
    private readonly IBattleboardDocumentService _battleboardDocumentService;
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
                    return true;

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
    private readonly Dictionary<string, string> _abilityDisplayIndex = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, EvolutionService.AbilityResult> _specialisationAbilityLookup =
        new Dictionary<string, EvolutionService.AbilityResult>(StringComparer.OrdinalIgnoreCase);
    private bool _isLoadingSpecialisationAbilityLookup;
    private IReadOnlyList<SpecialisationSummaryLineVm> _specialisationSummaryLines = Array.Empty<SpecialisationSummaryLineVm>();
    private IReadOnlyList<LevelAbilityRowVm> _classLevelAbilityRows = Array.Empty<LevelAbilityRowVm>();
    private IReadOnlyList<LevelAbilityRowVm> _raceLevelAbilityRows = Array.Empty<LevelAbilityRowVm>();
    private static readonly Regex LevelNumberRegex = new(
        "\\d+",
        RegexOptionsCompat.ForRuntime(RegexOptions.Compiled));

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
        IBattleboardDocumentService? battleboardDocumentService = null,
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
        _battleboardDocumentService = battleboardDocumentService
            ?? ServiceHelper.ResolveService<IBattleboardDocumentService>()
            ?? new BattleboardDocumentService();
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
        SaveToWalletCommand = new Command(() => _ = SaveToWallet(), () => Draft.IsRaceAndClassSelected);
        ContinueToAdvancementCommand = new Command(async () => await ContinueToAdvancementAsync());
        ToggleAdvancementExpandedCommand = new Command(() => IsAdvancementExpanded = !IsAdvancementExpanded);
        CharacterBuilderVm = new CharacterBuilderVm(Draft, NotifyGatingChanged, _creationDataService);

        GuildsVm = new GuildsVm(
            Draft,
            NotifyGatingChanged,
            CharacterBuilderVm.GetNonGuildAlignmentRules,
            CharacterBuilderVm.RefreshDraftAbilitiesAsync,
            _creationDataService,
            useMultiTypeFilters: true,
            searchByNameOnly: true,
            autoReload: false);

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
                createView: () => new CharacterReviewView(this)
                {
                    ShowPost8Card = false
                })
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

    public IReadOnlyList<LevelAbilityRowVm> ClassLevelAbilityRows => _classLevelAbilityRows;
    public bool HasClassLevelAbilityRows => ClassLevelAbilityRows.Count > 0;
    public string ClassLevelAbilityHeader => string.IsNullOrWhiteSpace((Draft.Class ?? string.Empty).Trim())
        ? "Class progression"
        : $"{Draft.Class} progression";

    public IReadOnlyList<LevelAbilityRowVm> RaceLevelAbilityRows => _raceLevelAbilityRows;
    public bool HasRaceLevelAbilityRows => RaceLevelAbilityRows.Count > 0;
    public string RaceLevelAbilityHeader => string.IsNullOrWhiteSpace((Draft.Race ?? string.Empty).Trim())
        ? "Race progression"
        : $"{Draft.Race} progression";

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
            var saved = SaveToWallet();
            if (!saved)
                return;

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
        var saved = SaveToWallet();
        if (!saved)
            return;

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
            (IReadOnlyList<LevelAbilityRowVm> classRows, IReadOnlyList<LevelAbilityRowVm> raceRows) =
                (Array.Empty<LevelAbilityRowVm>(), Array.Empty<LevelAbilityRowVm>());

            await Task.Run(async () =>
            {
                await CharacterBuilderVm.SyncDraftLifeAsync().ConfigureAwait(false);
                await CharacterBuilderVm.RefreshDraftAbilitiesAsync().ConfigureAwait(false);
                await EnsureAbilityCostIndexAsync().ConfigureAwait(false);
                (classRows, raceRows) = await BuildReviewLevelAbilityRowsAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ApplyReviewLevelAbilityRows(classRows, raceRows);
                ApplyAdvancementSummary();
            });
            return;
        }

        await CharacterBuilderVm.SyncDraftLifeAsync();
        await CharacterBuilderVm.RefreshDraftAbilitiesAsync();
        await EnsureAbilityCostIndexAsync();
        var (freshClassRows, freshRaceRows) = await BuildReviewLevelAbilityRowsAsync();
        if (MainThread.IsMainThread)
        {
            ApplyReviewLevelAbilityRows(freshClassRows, freshRaceRows);
            ApplyAdvancementSummary();
        }
        else
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ApplyReviewLevelAbilityRows(freshClassRows, freshRaceRows);
                ApplyAdvancementSummary();
            });
        }
    }

    private async Task<(IReadOnlyList<LevelAbilityRowVm> ClassRows, IReadOnlyList<LevelAbilityRowVm> RaceRows)>
        BuildReviewLevelAbilityRowsAsync()
    {
        var classRows = await BuildClassLevelAbilityRowsAsync();
        var raceRows = await BuildRaceLevelAbilityRowsAsync();
        return (classRows, raceRows);
    }

    private async Task<IReadOnlyList<LevelAbilityRowVm>> BuildClassLevelAbilityRowsAsync()
    {
        var className = (Draft.Class ?? string.Empty).Trim();
        if (className.Length == 0)
            return Array.Empty<LevelAbilityRowVm>();

        var classes = await _creationDataService.GetClassesAsync();
        var classKey = ResolveRecordKey(classes, className);
        if (string.IsNullOrWhiteSpace(classKey))
            return Array.Empty<LevelAbilityRowVm>();

        if (!classes.TryGetValue(classKey, out var classRecord) || classRecord?.Levels == null)
            return Array.Empty<LevelAbilityRowVm>();

        var raceKey = ResolveRaceKeyForLifeScale(Draft);
        var lifeByLevel = await _creationDataService.GetLifeScaleAsync(raceKey, classKey);

        var rows = new List<LevelAbilityRowVm>(capacity: 8);
        for (var level = 1; level <= 8; level++)
        {
            var abilities = GetAbilitiesForLevel(classRecord.Levels, level);
            var body = lifeByLevel.Count >= level ? lifeByLevel[level - 1].Body.ToString() : string.Empty;
            var loc = lifeByLevel.Count >= level ? lifeByLevel[level - 1].Loc.ToString() : string.Empty;
            rows.Add(LevelAbilityRowBuilder.Build(level, abilities, body, loc));
        }

        return rows;
    }

    private async Task<IReadOnlyList<LevelAbilityRowVm>> BuildRaceLevelAbilityRowsAsync()
    {
        var raceName = (Draft.Race ?? string.Empty).Trim();
        if (raceName.Length == 0)
            return Array.Empty<LevelAbilityRowVm>();

        var races = await _creationDataService.GetPeopleAsync();
        var raceKey = ResolveRecordKey(races, raceName);
        if (string.IsNullOrWhiteSpace(raceKey))
            return Array.Empty<LevelAbilityRowVm>();

        if (!races.TryGetValue(raceKey, out var raceRecord) || raceRecord?.LevelledAbilities == null)
            return Array.Empty<LevelAbilityRowVm>();

        var rows = new List<LevelAbilityRowVm>();
        for (var level = 1; level <= 8; level++)
        {
            var abilities = GetAbilitiesForLevel(raceRecord.LevelledAbilities, level);
            if (abilities.Count == 0)
                continue;

            rows.Add(LevelAbilityRowBuilder.Build(level, abilities));
        }

        return rows;
    }

    private void ApplyReviewLevelAbilityRows(
        IReadOnlyList<LevelAbilityRowVm> classRows,
        IReadOnlyList<LevelAbilityRowVm> raceRows)
    {
        _classLevelAbilityRows = classRows ?? Array.Empty<LevelAbilityRowVm>();
        _raceLevelAbilityRows = raceRows ?? Array.Empty<LevelAbilityRowVm>();

        Raise(nameof(ClassLevelAbilityRows));
        Raise(nameof(HasClassLevelAbilityRows));
        Raise(nameof(ClassLevelAbilityHeader));
        Raise(nameof(RaceLevelAbilityRows));
        Raise(nameof(HasRaceLevelAbilityRows));
        Raise(nameof(RaceLevelAbilityHeader));
    }

    private static IReadOnlyList<AbilityDefinition> GetAbilitiesForLevel(
        Dictionary<string, List<AbilityDefinition>> levels,
        int level)
    {
        if (levels == null || levels.Count == 0)
            return Array.Empty<AbilityDefinition>();

        foreach (var kvp in levels)
        {
            var parsedLevel = ExtractLevel(kvp.Key);
            if (parsedLevel != level)
                continue;

            var abilities = kvp.Value?
                .Where(def => def != null)
                .ToList();

            return abilities ?? new List<AbilityDefinition>();
        }

        return Array.Empty<AbilityDefinition>();
    }

    private string? ResolveRecordKey<T>(IReadOnlyDictionary<string, T> records, string rawName)
    {
        var trimmed = (rawName ?? string.Empty).Trim();
        if (trimmed.Length == 0 || records == null || records.Count == 0)
            return null;

        var direct = records.Keys.FirstOrDefault(key => key.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(direct))
            return direct;

        var normalized = _creationDataService.NormalizeLifeScaleKey(trimmed);
        if (normalized.Length == 0)
            return null;

        var normalizedMatch = records.Keys.FirstOrDefault(key =>
            _creationDataService.NormalizeLifeScaleKey(key).Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(normalizedMatch))
            return normalizedMatch;

        return records.Keys.FirstOrDefault(key =>
        {
            var normalizedKey = _creationDataService.NormalizeLifeScaleKey(key);
            return normalizedKey.Length > 0 &&
                   (normalizedKey.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains(normalizedKey, StringComparison.OrdinalIgnoreCase));
        });
    }

    private static int? ExtractLevel(string? key)
    {
        if (int.TryParse(key, out var numericLevel))
            return numericLevel;

        var match = LevelNumberRegex.Match(key ?? string.Empty);
        if (!match.Success)
            return null;

        return int.TryParse(match.Value, out numericLevel)
            ? numericLevel
            : null;
    }

    private static string ResolveRaceKeyForLifeScale(CharacterDraft draft)
    {
        var race = (draft.Race ?? string.Empty).Trim();
        if (!race.Equals("Elf", StringComparison.OrdinalIgnoreCase))
            return race;

        var subtype = (draft.RaceSubtype ?? string.Empty).Trim();
        if (subtype.Equals("Winter", StringComparison.OrdinalIgnoreCase))
            return "Winter Elf";

        if (subtype.Equals("Summer", StringComparison.OrdinalIgnoreCase))
            return "Drowe";

        return "Elf";
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
        var seenSelections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string NormalizeKey(string? key)
            => new string((key ?? string.Empty)
                .Trim()
                .ToLowerInvariant()
                .Where(char.IsLetterOrDigit)
                .ToArray());

        void AddLine(string text, string? potentialAbilityIndex, string? specialisationKey, string? selectedOption)
        {
            var dedupeKey = SpecialisationSummaryDedupe.BuildKey(
                text,
                specialisationKey,
                selectedOption);

            if (dedupeKey.Length > 0 && !seenSelections.Add(dedupeKey))
                return;

            lines.Add(CreateSpecialisationSummaryLine(
                text: text,
                potentialAbilityIndex: potentialAbilityIndex,
                specialisationKey: specialisationKey,
                selectedOption: selectedOption));
        }

        var subtype = Draft.RaceSubtypeValue ?? Draft.RaceSubtype;
        if (!string.IsNullOrWhiteSpace(subtype))
        {
            var subtypeKey = CharacterBuilderVm?.SpecialisationVm?.RaceSubtypeDetailKey ?? string.Empty;
            AddLine(
                text: $"Subtype: {subtype}",
                potentialAbilityIndex: subtype,
                specialisationKey: subtypeKey,
                selectedOption: subtype);
        }

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
                AddLine(
                    text: lineText,
                    potentialAbilityIndex: selection,
                    specialisationKey: group.DetailKey,
                    selectedOption: slot.SelectedOption);
                var normalizedGroupKey = NormalizeKey(group.Title);
                if (normalizedGroupKey.Length > 0)
                    includedKeys.Add(normalizedGroupKey);
            }

            foreach (var mapped in specVm.MappedSpecialisations)
            {
                if (mapped == null)
                    continue;

                var picked = (mapped.SelectedOption ?? string.Empty).Trim();
                if (picked.Length == 0)
                    continue;

                AddLine(
                    text: $"{mapped.Title}: {picked}",
                    potentialAbilityIndex: picked,
                    specialisationKey: mapped.DetailKey,
                    selectedOption: picked);
                var normalizedMappedKey = NormalizeKey(mapped.Key);
                if (normalizedMappedKey.Length > 0)
                    includedKeys.Add(normalizedMappedKey);
            }
        }

        foreach (var kvp in Draft.SpecialisationSelections.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            var normalizedDraftKey = NormalizeKey(kvp.Key);
            if (normalizedDraftKey.Length > 0 && includedKeys.Contains(normalizedDraftKey))
                continue;

            AddLine(
                text: $"{kvp.Key}: {kvp.Value}",
                potentialAbilityIndex: kvp.Value,
                specialisationKey: kvp.Key,
                selectedOption: kvp.Value);
        }

        return lines;
    }

    private SpecialisationSummaryLineVm CreateSpecialisationSummaryLine(
        string text,
        string? potentialAbilityIndex,
        string? specialisationKey,
        string? selectedOption)
    {
        EvolutionService.AbilityResult? ability = null;
        if (_specialisationAbilityLookup.Count > 0)
            ability = AbilityDetailsLookupService.FindByIndex(_specialisationAbilityLookup, potentialAbilityIndex);

        return new SpecialisationSummaryLineVm(
            text: text,
            ability: ability,
            specialisationKey: specialisationKey,
            selectedOption: selectedOption);
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
        Raise(nameof(ClassLevelAbilityRows));
        Raise(nameof(HasClassLevelAbilityRows));
        Raise(nameof(ClassLevelAbilityHeader));
        Raise(nameof(RaceLevelAbilityRows));
        Raise(nameof(HasRaceLevelAbilityRows));
        Raise(nameof(RaceLevelAbilityHeader));
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
        await DownloadBattleboardAsExcelAsync();
    }

    public async Task DownloadBattleboardAsExcelAsync()
    {
        await SyncDraftStateAsync();
        var path = await _battleboardExportService.ExportAsync(Draft);
        await _exportService.OpenFileAsync(path);
    }

    public async Task DownloadBattleboardAsPdfAsync()
    {
        await SyncDraftStateAsync();
        var excelPath = await _battleboardExportService.ExportAsync(Draft);
        var pdfPath = await _battleboardDocumentService.ConvertExcelToPdfAsync(
            excelPath,
            BuildBattleboardPdfFileName());
        await _exportService.OpenFileAsync(pdfPath);
    }

    public async Task EmailBattleboardPdfToDeskAsync()
    {
        if (!Email.Default.IsComposeSupported)
            throw new NotSupportedException("Email composition is not supported on this device.");

        await SyncDraftStateAsync();
        var excelPath = await _battleboardExportService.ExportAsync(Draft);
        var pdfPath = await _battleboardDocumentService.ConvertExcelToPdfAsync(
            excelPath,
            BuildBattleboardPdfFileName());

        var message = new EmailMessage
        {
            To = new List<string> { "battleboards@labyrinthe.com" },
            Subject = BuildBattleboardDeskSubject(),
            Body = string.Empty,
            BodyFormat = EmailBodyFormat.PlainText,
            Attachments = new List<EmailAttachment> { new(pdfPath) }
        };

        await Email.Default.ComposeAsync(message);
    }

    private string BuildBattleboardDeskSubject()
    {
        var playerName = (Draft.PlayerName ?? string.Empty).Trim();
        var characterName = (Draft.Name ?? string.Empty).Trim();
        return $"{playerName} - {characterName}";
    }

    private string BuildBattleboardPdfFileName()
    {
        var characterName = (Draft.Name ?? string.Empty).Trim();
        if (characterName.Length == 0)
            characterName = "Character";

        var sanitized = new string(characterName
            .Where(ch => !Path.GetInvalidFileNameChars().Contains(ch))
            .ToArray())
            .Trim();

        if (sanitized.Length == 0)
            sanitized = "Character";

        return $"Battleboard_{sanitized}.pdf";
    }

    private bool SaveToWallet()
    {
        try
        {
            _draftStore.Save();
            RaiseReviewProperties();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WizardVm] Failed to save character draft: {ex}");
            RuntimeLog.Write("WIZARD_SAVE", "Failed to save character draft at wizard completion.", ex);
            _ = MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var page = Application.Current?.MainPage;
                if (page != null)
                    await page.DisplayAlert("Save failed", $"{ex.Message}\n\nLog: {RuntimeLog.LogPath}", "OK");
            });
            return false;
        }
    }

    private async Task EnsureAbilityCostIndexAsync()
    {
        if (_abilityCostIndex.Count > 0)
            return;

        var abilities = await EvolutionService.GetAllAbilitiesAsync();
        foreach (var entry in abilities)
        {
            var name = (entry.Index ?? string.Empty).Trim();
            if (name.Length == 0)
                continue;

            RegisterAbilityCostEntry(name, entry.Cost, displayName: name);

            var abilityKey = AbilityKey.Build(entry);
            if (!string.IsNullOrWhiteSpace(abilityKey))
                RegisterAbilityCostEntry(abilityKey, entry.Cost, displayName: name);

            var legacyKey = AbilityKey.BuildEvolutionFallback(entry);
            if (!string.IsNullOrWhiteSpace(legacyKey)
                && !string.Equals(legacyKey, abilityKey, StringComparison.OrdinalIgnoreCase))
            {
                RegisterAbilityCostEntry(legacyKey, entry.Cost, displayName: name);
            }
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
                ResolveAdvancementAbilityDisplayName(line.Name),
                line.Cost,
                line.RunningTotal));
        }

        AdvancementPointsSpent = _domainService.ComputeAbilityPointsSpent(
            Draft.AdvancementAbilities ?? new List<string>(),
            _abilityCostIndex);
        RaiseReviewProperties();
    }

    private void RegisterAbilityCostEntry(string? rawKey, int cost, string displayName)
    {
        var key = (rawKey ?? string.Empty).Trim();
        if (key.Length == 0)
            return;

        if (!_abilityCostIndex.ContainsKey(key))
            _abilityCostIndex[key] = Math.Max(0, cost);

        if (_abilityDisplayIndex.ContainsKey(key))
            return;

        var normalizedDisplay = EvolutionService.NormalizeAbilityDisplayText(displayName);
        _abilityDisplayIndex[key] = string.IsNullOrWhiteSpace(normalizedDisplay)
            ? key
            : normalizedDisplay;
    }

    private string ResolveAdvancementAbilityDisplayName(string? rawKeyOrName)
    {
        var key = (rawKeyOrName ?? string.Empty).Trim();
        if (key.Length == 0)
            return string.Empty;

        if (_abilityDisplayIndex.TryGetValue(key, out var cachedDisplay))
            return cachedDisplay;

        var ability = AbilityDetailsLookupService.FindByIndex(_specialisationAbilityLookup, key);
        if (ability == null)
            return key;

        var displayName = EvolutionService.NormalizeAbilityDisplayText(ability.Index);
        if (displayName.Length == 0)
            return key;

        RegisterAbilityCostEntry(key, ability.Cost, displayName);
        return displayName;
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
        public string SpecialisationKey { get; }
        public string SelectedOption { get; }
        public bool HasDetails => Ability != null || SpecialisationKey.Length > 0;

        public SpecialisationSummaryLineVm(
            string text,
            EvolutionService.AbilityResult? ability,
            string? specialisationKey,
            string? selectedOption)
        {
            Text = text;
            Ability = ability;
            SpecialisationKey = (specialisationKey ?? string.Empty).Trim();
            SelectedOption = (selectedOption ?? string.Empty).Trim();
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
