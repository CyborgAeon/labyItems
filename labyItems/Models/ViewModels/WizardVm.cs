using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using labyItems.Controls;
using labyItems.Models.Characters;
using labyItems.Pages.Characters;

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

    // Steps displayed in StepIndicator
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
            if (CurrentStep >= StepSteps.Count - 1) return true; // "Save & Continue" enabled when review is reachable
            return CanNavigateToStep(CurrentStep + 1);
        }
    }

    public string NextButtonText => CurrentStep == StepSteps.Count - 1 ? "Save & Continue" : "Next";

    public ICommand BackCommand { get; }
    public ICommand NextCommand { get; }
    public Command<int> StepClickCommand { get; }

    // Step VMs (created once to preserve state)
    public CharacterBuilderVm CharacterBuilderVm { get; }

    public WizardVm()
    {
        CharacterBuilderVm = new CharacterBuilderVm(Draft, NotifyGatingChanged);

        BackCommand = new Command(OnBack);
        NextCommand = new Command(OnNext);
        StepClickCommand = new Command<int>(TryGoToStep);

        CurrentStep = 0;
        UpdateStepView();
    }

    public void NotifyGatingChanged()
    {
        // Called by step VMs whenever user selection changes
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
        // Final step: save & continue
        if (CurrentStep == StepSteps.Count - 1)
        {
            // TODO: Persist Draft to storage and navigate to your Characters screen.
            // Example with Shell routes:
            // await Shell.Current.GoToAsync("//Characters");
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

        // Always allow backward navigation
        if (targetIndex < CurrentStep)
        {
            CurrentStep = targetIndex;
            return;
        }

        // Forward navigation is gated
        if (!CanNavigateToStep(targetIndex)) return;
        CurrentStep = targetIndex;
    }

    private bool CanNavigateToStep(int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= StepSteps.Count) return false;

        // Always allow step 0
        if (targetIndex == 0) return true;

        // Gate forward steps
        return targetIndex switch
        {
            1 => Draft.IsRaceAndClassSelected,
            2 => Draft.IsRaceAndClassSelected,                 // Guilds needs race/class for availability filtering
            3 => Draft.IsRaceAndClassSelected && IsStep2Valid(),// Status needs info valid
            4 => Draft.IsRaceAndClassSelected && IsStep2Valid(),// Review needs everything required
            _ => false
        };
    }

    private bool IsStep2Valid()
    {
        // Placeholder. Replace with your real step-2 validations.
        // Example: name required
        return !string.IsNullOrWhiteSpace(Draft.Name);
    }

    private void UpdateStepView()
    {
        CurrentStepView = CurrentStep switch
        {
            0 => new CharacterBuilder(CharacterBuilderVm),
            1 => BuildPlaceholder("Step 2 - Character Info (not implemented here)"),
            2 => BuildPlaceholder("Step 3 - Guilds (not implemented here)"),
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
