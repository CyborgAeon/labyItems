using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using labyItems.Controls;
using labyItems.Models.Characters;
using labyItems.Pages.Characters;
using labyItems.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;

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
    private readonly IBattleboardExportService _exportService;
    private readonly Func<Task>? _onFinished;

    public ObservableCollection<StepItem> StepSteps { get; } = new()
    {
        new StepItem { Id = 1, Label = "Race/Class" },
        new StepItem { Id = 2, Label = "Specialise" },
        new StepItem { Id = 3, Label = "Guilds" },
        new StepItem { Id = 4, Label = "Details" },
        new StepItem { Id = 5, Label = "Review" },
    };

    public ObservableCollection<int?> ArmourLayers { get; } = new() { 0 };

    private int _currentStep;
    public int CurrentStep
    {
        get => _currentStep;
        set
        {
            if (!Set(ref _currentStep, value)) return;
            UpdateStepView();
            Raise(nameof(CanGoBack));
            Raise(nameof(CanGoNext));
            Raise(nameof(NextButtonText));
        }
    }

    private View? _currentStepView;
    public View? CurrentStepView
    {
        get => _currentStepView;
        private set => Set(ref _currentStepView, value);
    }

    public bool CanGoBack => CurrentStep > 0;

    public bool CanGoNext
    {
        get
        {
            if (CurrentStep >= StepSteps.Count - 1) return true;
            return CanNavigateToStep(CurrentStep + 1);
        }
    }

    public string NextButtonText => CurrentStep == StepSteps.Count - 1 ? "Save & Continue" : "Next";

    public ICommand BackCommand { get; }
    public ICommand NextCommand { get; }
    public Command<int> StepClickCommand { get; }
    public Command ExportToBattleboardCommand { get; }
    public Command ExportToExcelCommand { get; }
    public Command SaveToWalletCommand { get; }
    public CharacterBuilderVm CharacterBuilderVm { get; }
    public GuildsVm GuildsVm { get; }
    private int _armourMaxBasePac;
    private int _armourMaxTotalPac;
    private int _wornArmourPac;
    private bool _isArmourSelectionEnabled = true;

    public WizardVm(CharacterDraft? draft = null, Func<Task>? onFinished = null)
    {
        Draft = draft ?? new CharacterDraft();
        _onFinished = onFinished;
        _exportService = new BattleboardExportService();
        BackCommand = new Command(OnBack);
        NextCommand = new Command(async () => await OnNextAsync());
        StepClickCommand = new Command<int>(async i => await TryGoToStepAsync(i));
        ExportToBattleboardCommand = new Command(async () => await ExportBattleboardAsync(), () => Draft.IsRaceAndClassSelected);
        ExportToExcelCommand = new Command(async () => await ExportBattleboardToExcelAsync(), () => Draft.IsRaceAndClassSelected);
        SaveToWalletCommand = new Command(SaveToWallet, () => Draft.IsRaceAndClassSelected);
        CharacterBuilderVm = new CharacterBuilderVm(Draft, NotifyGatingChanged);

        // NEW: optional guild selection step
        GuildsVm = new GuildsVm(Draft, NotifyGatingChanged, CharacterBuilderVm.GetNonGuildAlignmentRules, CharacterBuilderVm.RefreshDraftAbilitiesAsync);

        UpdateArmourUiFromDraft();

        CurrentStep = 0;
        UpdateStepView();

        MainThread.BeginInvokeOnMainThread(async () => await SyncDraftStateAsync());
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

    public IEnumerable<string> SpecialisationSummaryLines => BuildSpecialisationSummary();

    public string SpecialisationSummaryHeader => SpecialisationSummaryLines.Any()
        ? "Chosen options:"
        : "No specialisations selected.";

    public string NotesSummary => string.IsNullOrWhiteSpace(Draft.Notes)
        ? "No notes provided."
        : Draft.Notes;

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

    private void OnBack()
    {
        if (!CanGoBack) return;
        CurrentStep--;
    }

    private async Task OnNextAsync()
    {
        if (CurrentStep == StepSteps.Count - 1)
        {
            await SyncDraftStateAsync();
            SaveToWallet();
            if (_onFinished != null)
                await _onFinished();
            return;
        }

        var target = CurrentStep + 1;
        if (!CanNavigateToStep(target)) return;
        await SyncDraftStateAsync();
        CurrentStep = target;
    }

    private async Task SyncDraftStateAsync()
    {
        ClampWornArmourPac();
        Draft.WornArmour = ClampWornArmour(WornArmourPac);
        await CharacterBuilderVm.SyncDraftLifeAsync();
        await CharacterBuilderVm.RefreshDraftAbilitiesAsync();
    }

    private async Task TryGoToStepAsync(int targetIndex)
    {
        if (targetIndex == CurrentStep) return;

        if (targetIndex < CurrentStep)
        {
            CurrentStep = targetIndex;
            return;
        }

        if (!CanNavigateToStep(targetIndex)) return;
        await SyncDraftStateAsync();
        CurrentStep = targetIndex;
    }

    private bool CanNavigateToStep(int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= StepSteps.Count) return false;

        if (targetIndex == 0) return true;

        return targetIndex switch
        {
            1 => Draft.IsRaceAndClassSelected,
            2 => Draft.IsRaceAndClassSelected && IsStep2Valid(),
            3 => Draft.IsRaceAndClassSelected && IsStep2Valid(),
            4 => Draft.IsRaceAndClassSelected && IsStep2Valid(),
            _ => false
        };
    }

    private bool IsStep2Valid()
    {
        return CharacterBuilderVm.SpecialisationVm.IsComplete;
    }

    private void UpdateStepView()
    {
        if (CurrentStep == 2)
        {
            MainThread.BeginInvokeOnMainThread(async () => { await GuildsVm.ReloadAsync(); });
        }
        else if (CurrentStep == 3)
        {
            UpdateArmourUiFromDraft();
        }

        CurrentStepView = CurrentStep switch
        {
            0 => new CharacterBuilder(CharacterBuilderVm),
            1 => new CharacterSpecialisation(CharacterBuilderVm),

            2 => new Guilds(GuildsVm),
            3 => new CharacterInfoView(this),
            4 => new CharacterReviewView(this),
            _ => BuildPlaceholder("Unknown step")
        };
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

    private IEnumerable<string> BuildSpecialisationSummary()
    {
        var lines = new List<string>();

        var subtype = Draft.RaceSubtypeValue ?? Draft.RaceSubtype;
        if (!string.IsNullOrWhiteSpace(subtype))
            lines.Add($"Subtype: {subtype}");

        foreach (var kvp in Draft.SpecialisationSelections.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            lines.Add($"{kvp.Key}: {kvp.Value}");
        }

        return lines;
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
        }
    }

    private string BuildArmourAvailabilityText()
    {
        var tier = GetArmourTierFromDraft();
        if (tier == ArmourTier.None)
            return "Armour: none allowed.";

        var layers = GetMaxLayersForTier(tier);
        return $"Armour: {tier} (max base PAC {ArmourMaxBasePac}, max worn PAC {ArmourMaxTotalPac}, layers {layers})";
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
        Raise(nameof(SpecialisationSummaryLines));
        Raise(nameof(SpecialisationSummaryHeader));
        Raise(nameof(NotesSummary));
        Raise(nameof(AlignmentSummary));
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
        var path = await _exportService.ExportAsync(Draft);

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = $"{Draft.Name}'s battleboard",
            File = new ShareFile(path)
        });
    }

    private async Task ExportBattleboardToExcelAsync()
    {
        await SyncDraftStateAsync();
        var path = await _exportService.ExportAsync(Draft);

        await Launcher.OpenAsync(new OpenFileRequest
        {
            File = new ReadOnlyFile(path)
        });
    }

    private void SaveToWallet()
    {
        LiteDbService.UpsertDraft(Draft);
        RaiseReviewProperties();
    }

    private enum ArmourTier
    {
        None = 0,
        Light = 1,
        Medium = 2,
        Heavy = 3
    }
}
