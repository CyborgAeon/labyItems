using System;
using System.Collections.Generic;
using System.Windows.Input;
using labyItems.Models;
using Microsoft.Maui.Controls;
using labyItems.Controls;

namespace labyItems.Pages.Calculator.CalcNav;

public partial class LifeConfigPage : ContentPage
{
    public event Action<CalcContribution>? ContributionAdded;
    public static readonly BindableProperty TotalProperty = BindableProperty.Create(
        nameof(Total),
        typeof(int),
        typeof(LifeConfigPage),
        0
    );

    public int Total
    {
        get => (int)GetValue(TotalProperty);
        set => SetValue(TotalProperty, value);
    }

    public LifeConfigPage()
    {
        BackNavigationCommand = new Command(async () => await NavigateBackAsync());
        InitializeComponent();
        ComputeTotal();
        // Default mapping (your keys/values)
        var data = new Dictionary<string, int>
        {
            {"No additional life", 0},
            { "3/1", 4 },
            { "6/2", 9 },
            { "9/3", 14 },
            { "12/4", 20 },
            { "15/5", 28 },
            { "18/6", 38 },
            { "21/7", 50 },
            { "24/8", 65 },
        };

        LifeSlider.ItemsSource = data;
    }

    public static readonly BindableProperty ReturnToFormCommandProperty = BindableProperty.Create(
        nameof(ReturnToFormCommand),
        typeof(ICommand),
        typeof(LifeConfigPage),
        null
    );

    public ICommand? ReturnToFormCommand
    {
        get => (ICommand?)GetValue(ReturnToFormCommandProperty);
        set => SetValue(ReturnToFormCommandProperty, value);
    }

    public ICommand BackNavigationCommand { get; }

    private async Task OnReturnCommand()
    {
        var key = LifeSlider.SelectedKey; // e.g. "12/4"
        var value = LifeSlider.SelectedValue; // e.g. 20
        var result = new CalcResult
        {
            AbilityType = "Life",
            AbilityName = key,
            TotalIsp = value,
            Details = new Dictionary<string, object?> { ["life"] = key },
        };
        ContributionAdded?.Invoke(
            new CalcContribution(
                Id: Guid.NewGuid().ToString(),
                Source: "Life",
                Result: result,
                OnRemove: ResetSelection
            )
        );
    }

    private void OnReturnToCalculator(object sender, EventArgs e) => OnReturnCommand();

    private int ComputeTotal()
    {
        return LifeSlider.SelectedValue;
    }

    private void ResetSelection()
    {
        LifeSlider.SelectedIndex = -1;
    }

    private void OnLifeSelectionChanged(object sender, DictionarySelectionChangedEventArgs e)
    {
        Total = LifeSlider.SelectedValue;
        var key = LifeSlider.SelectedKey;
        var result = new CalcResult
        {
            AbilityType = "Life",
            AbilityName = key,
            TotalIsp = Total,
            Details = new Dictionary<string, object?> { ["life"] = key },
        };

        ContributionAdded?.Invoke(
            new CalcContribution(
                Id: "life",
                Source: "Life",
                Result: result,
                OnRemove: ResetSelection
            )
        );
    }

    private async Task NavigateBackAsync()
    {
        if (Navigation?.NavigationStack?.Count > 1)
        {
            await Navigation.PopAsync();
            return;
        }

        if (Shell.Current != null)
            await Shell.Current.GoToAsync("..");
    }
}
