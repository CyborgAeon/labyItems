using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using labyItems.Controls;
using labyItems.Models.Characters;
using labyItems.Pages.Characters;
using labyItems.Pages.Characters.ViewModels;
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

    public CharacterDraft Draft { get; } = new();

    public ObservableCollection<StepItem> StepSteps { get; } = new()
    {
        new StepItem { Id = 1, Label = "Race & Class" },
        new StepItem { Id = 2, Label = "Info" },
        new StepItem { Id = 3, Label = "Guilds" },
        new StepItem { Id = 4, Label = "Status" },
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

    // Step VMs (created once to preserve state)
    public CharacterBuilderVm CharacterBuilderVm { get; }

    // NEW
    public GuildsVm GuildsVm { get; }

    public WizardVm()
    {
        CharacterBuilderVm = new CharacterBuilderVm(Draft, NotifyGatingChanged);

        // NEW: optional guild selection step
        GuildsVm = new GuildsVm(Draft, NotifyGatingChanged, null);
        BackCommand = new Command(OnBack);
        NextCommand = new Command(OnNext);
        StepClickCommand = new Command<int>(TryGoToStep);

        CurrentStep = 0;
        UpdateStepView();
    }

    public void NotifyGatingChanged()
    {
        Raise(nameof(CanGoNext));
        Raise(nameof(NextButtonText));
    }

    private void OnBack()
    {
        if (!CanGoBack) return;
        CurrentStep--;
    }

    private async void OnNext()
    {
        if (CurrentStep == StepSteps.Count - 1)
        {
            await Task.CompletedTask;
            return;
        }

        var target = CurrentStep + 1;
        if (!CanNavigateToStep(target)) return;
        CurrentStep = target;
    }

    private void TryGoToStep(int targetIndex)
    {
        if (targetIndex == CurrentStep) return;

        if (targetIndex < CurrentStep)
        {
            CurrentStep = targetIndex;
            return;
        }

        if (!CanNavigateToStep(targetIndex)) return;
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

            // NEW: real guilds step
            2 => new Guilds(GuildsVm),

            3 => BuildPlaceholder("Step 4 - Status/Buffs (not implemented here)"),
            4 => BuildPlaceholder("Step 5 - Review (not implemented here)"),
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
}
