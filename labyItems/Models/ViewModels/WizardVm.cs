using System.Windows.Input;
using labyItems.Infrastructure;
using labyItems.Models.DTOs;
using labyItems.Pages;

namespace labyItems.Models.ViewModels;

public sealed class ClassCardVm
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public string Summary { get; init; } = "";
    public string FullDetails { get; init; } = "";
    public string Tag1 { get; init; } = "AC 9";
    public string Tag2 { get; init; } = "72 HP";
    public string Tag3 { get; init; } = "Shield";

    public bool IsExpanded { get; set; }
}

public sealed class RaceCardVm
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Summary { get; init; } = "";
}


public sealed class WizardViewModel : ObservableObject
{
    public CharacterDraft Draft { get; } = new();

    // StepIndicator data source
    public IList<StepItem> StepSteps { get; } = new List<StepItem>
    {
        new() { Id = 1, Label = "Race/Class" },
        new() { Id = 2, Label = "Info" },
        new() { Id = 3, Label = "Guilds" },
        new() { Id = 4, Label = "Buffs" },
        new() { Id = 5, Label = "Review" },
    };

    private int _currentStep;
    public int CurrentStep
    {
        get => _currentStep;
        set
        {
            if (!SetProperty(ref _currentStep, value)) return;
            UpdateStepView();
            Raise(nameof(CanGoNext));
            Raise(nameof(NextButtonText));
            Raise(nameof(CanGoBack));
        }
    }

    // The view displayed by ContentPresenter
    private View? _currentStepView;
    public View? CurrentStepView
    {
        get => _currentStepView;
        private set => SetProperty(ref _currentStepView, value);
    }

    public bool CanGoBack => CurrentStep > 0;

    public bool CanGoNext => CanNavigateToStep(CurrentStep + 1);

    public string NextButtonText => CurrentStep == StepSteps.Count - 1 ? "Save & Continue" : "Next";

    // Commands
    public ICommand BackCommand { get; }
    public ICommand NextCommand { get; }
    public Command<int> StepClickCommand { get; }

    public WizardViewModel()
    {
        BackCommand = new Command(OnBack, () => CanGoBack);
        NextCommand = new Command(OnNext);

        // This is what StepIndicator calls when a bubble is clicked
        StepClickCommand = new Command<int>(TryGoToStep);

        // Start at step 0
        CurrentStep = 0;
    }

    private void OnBack()
    {
        if (CurrentStep <= 0) return;
        CurrentStep--;
        (BackCommand as Command)?.ChangeCanExecute();
    }

    private async void OnNext()
    {
        // Last step: save and navigate away
        if (CurrentStep == StepSteps.Count - 1)
        {
            // TODO: persist Draft -> create character record
            // TODO: navigate to Characters screen
            // Example:
            // await Shell.Current.GoToAsync("//CharactersPage");
            return;
        }

        var target = CurrentStep + 1;
        if (!CanNavigateToStep(target)) return;

        CurrentStep = target;
        (BackCommand as Command)?.ChangeCanExecute();
    }

    private void TryGoToStep(int targetIndex)
    {
        // Bubble click from StepIndicator ends up here
        if (targetIndex == CurrentStep) return;

        if (!CanNavigateToStep(targetIndex))
            return;

        CurrentStep = targetIndex;
        (BackCommand as Command)?.ChangeCanExecute();
    }

    private bool CanNavigateToStep(int targetIndex)
    {
        // bounds
        if (targetIndex < 0 || targetIndex >= StepSteps.Count) return false;

        // Always allow going backwards
        if (targetIndex <= CurrentStep) return true;

        // Forward gating rules
        return targetIndex switch
        {
            0 => true,
            1 => Draft.IsRaceAndClassSelected,            // info requires race/class
            2 => Draft.IsRaceAndClassSelected,            // guilds requires race/class
            3 => Draft.IsRaceAndClassSelected && IsStep2Valid(),
            4 => Draft.IsRaceAndClassSelected && IsStep2Valid(),
            _ => false
        };
    }

    private bool IsStep2Valid()
    {
        // replace with your real validation logic
        return !string.IsNullOrWhiteSpace(Draft.Name);
    }

    private void UpdateStepView()
    {
        // IMPORTANT: Step views are ContentViews (not ContentPages)
        // Pass Draft/VM into step views as BindingContext.
        CurrentStepView = CurrentStep switch
        {
            0 => new CharacterBuilderPage { BindingContext = this },
            1 => new Step2InfoView { BindingContext = this },
            2 => new Step3GuildsView { BindingContext = this },
            3 => new Step4BuffsView { BindingContext = this },
            4 => new Step5ReviewView { BindingContext = this },
            _ => new ContentView()
        };
    }

    // Call this from step views whenever something changes that affects gating
    public void NotifyGatingChanged()
    {
        Raise(nameof(CanGoNext));
        Raise(nameof(NextButtonText));
        (BackCommand as Command)?.ChangeCanExecute();
    }
}