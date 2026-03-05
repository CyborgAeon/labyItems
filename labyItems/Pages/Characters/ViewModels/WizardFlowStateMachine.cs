using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace labyItems.Pages.Characters.ViewModels;

public sealed class WizardFlowStateMachine
{
    private readonly IReadOnlyList<WizardStepDefinition> _steps;
    private int _currentStep;
    private Func<Task>? _pendingEnter;

    public WizardFlowStateMachine(IReadOnlyList<WizardStepDefinition> steps, int initialStep = 0)
    {
        if (steps == null || steps.Count == 0)
            throw new ArgumentException("Wizard flow must contain at least one step.", nameof(steps));

        if (initialStep < 0 || initialStep >= steps.Count)
            throw new ArgumentOutOfRangeException(nameof(initialStep));

        _steps = steps;
        _currentStep = initialStep;
    }

    public IReadOnlyList<WizardStepDefinition> Steps => _steps;
    public int CurrentStep => _currentStep;

    public bool CanEnter(int index)
    {
        if (index < 0 || index >= _steps.Count)
            return false;

        return _steps[index].CanEnter();
    }

    public async Task<bool> TryTransitionAsync(int targetIndex, bool deferEnter = false)
    {
        if (targetIndex < 0 || targetIndex >= _steps.Count)
            return false;

        if (targetIndex == _currentStep)
            return true;

        if (targetIndex > _currentStep && !CanEnter(targetIndex))
            return false;

        var exit = _steps[_currentStep].OnExitAsync;
        if (exit != null)
            await exit();

        _currentStep = targetIndex;

        var enter = _steps[_currentStep].OnEnterAsync;
        if (enter != null)
        {
            if (deferEnter)
                _pendingEnter = enter;
            else
            {
                _pendingEnter = null;
                await enter();
            }
        }
        else
        {
            _pendingEnter = null;
        }

        return true;
    }

    public Func<Task>? ConsumePendingEnter()
    {
        var enter = _pendingEnter;
        _pendingEnter = null;
        return enter;
    }
}
