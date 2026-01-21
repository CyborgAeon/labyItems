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

    public CharacterDraft Draft { get; } = new();
    private readonly IBattleboardExportService _exportService;

    public ObservableCollection<StepItem> StepSteps { get; } = new()
    {
        new StepItem { Id = 1, Label = "Race/Class" },
        new StepItem { Id = 2, Label = "Specialise" },
        new StepItem { Id = 3, Label = "Guilds" },
        new StepItem { Id = 4, Label = "Details" },
        new StepItem { Id = 5, Label = "Review" },
    };

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
    public Command SaveToWalletCommand { get; }
    public CharacterBuilderVm CharacterBuilderVm { get; }
    public GuildsVm GuildsVm { get; }

    public WizardVm()
    {
        _exportService = new BattleboardExportService();
        CharacterBuilderVm = new CharacterBuilderVm(Draft, NotifyGatingChanged);

        // NEW: optional guild selection step
        GuildsVm = new GuildsVm(Draft, NotifyGatingChanged, null, CharacterBuilderVm.RefreshDraftAbilitiesAsync);
        BackCommand = new Command(OnBack);
        NextCommand = new Command(async () => await OnNextAsync());
        StepClickCommand = new Command<int>(async i => await TryGoToStepAsync(i));
        ExportToBattleboardCommand = new Command(async () => await ExportBattleboardAsync(), () => Draft.IsRaceAndClassSelected);
        SaveToWalletCommand = new Command(SaveToWallet, () => Draft.IsRaceAndClassSelected);

        CurrentStep = 0;
        UpdateStepView();
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

    public void NotifyGatingChanged()
    {
        Raise(nameof(CanGoNext));
        Raise(nameof(NextButtonText));
        Raise(nameof(AlignmentOptions));
        Raise(nameof(SelectedAlignment));
        RaiseReviewProperties();
        ExportToBattleboardCommand.ChangeCanExecute();
        SaveToWalletCommand.ChangeCanExecute();
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
            return;
        }

        var target = CurrentStep + 1;
        if (!CanNavigateToStep(target)) return;
        await SyncDraftStateAsync();
        CurrentStep = target;
    }

    private async Task SyncDraftStateAsync()
    {
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

    private void SaveToWallet()
    {
        LiteDbService.UpsertDraft(Draft);
        RaiseReviewProperties();
    }
}
