using System;
using System.Threading.Tasks;
using labyItems.Controls;
using Microsoft.Maui.Controls;

namespace labyItems.Pages.Characters.ViewModels;

public sealed class WizardStepDefinition
{
    public int Index { get; }
    public StepItem StepItem { get; }
    public Func<bool> CanEnter { get; }
    public Func<View> CreateView { get; }
    public Func<Task>? OnEnterAsync { get; }
    public Func<Task>? OnExitAsync { get; }

    public WizardStepDefinition(
        int index,
        string label,
        Func<bool> canEnter,
        Func<View> createView,
        Func<Task>? onEnterAsync = null,
        Func<Task>? onExitAsync = null)
    {
        Index = index;
        StepItem = new StepItem { Id = index + 1, Label = label ?? string.Empty };
        CanEnter = canEnter ?? (() => false);
        CreateView = createView ?? (() => new ContentView());
        OnEnterAsync = onEnterAsync;
        OnExitAsync = onExitAsync;
    }
}
