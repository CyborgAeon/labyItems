using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading.Tasks;
using labyItems.Controls;
using labyItems.Models;
using labyItems.Pages.Calculator;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;

namespace labyItems.Pages.Configs;

public abstract class ConfigPageBase<TConfig> : ContentPage
    where TConfig : ConfigBase, new()
{
    private TaskCompletionSource<CalcResult?> _tcs = new();
    public Task<CalcResult?> Completion
    {
        get
        {
            if (_tcs.Task.IsCompleted)
                _tcs = new TaskCompletionSource<CalcResult?>();
            return _tcs.Task;
        }
    }

    public TConfig Config { get; private set; }
    public ObservableCollection<ContributionRow> FooterBreakdownItems { get; } = new();
    public Command ReturnFromConfigCommand { get; set; }
    private Pages.Calculator.IspCalculator? _calculatorContext;
    private bool _isUpdatingFooter;
    public Pages.Calculator.IspCalculator? CalculatorContext
    {
        get => _calculatorContext;
        set
        {
            if (_calculatorContext == value)
                return;

            if (_calculatorContext is not null)
                _calculatorContext.BreakdownItems.CollectionChanged -= OnCalculatorBreakdownChanged;

            _calculatorContext = value;

            if (_calculatorContext is not null)
                _calculatorContext.BreakdownItems.CollectionChanged += OnCalculatorBreakdownChanged;

            OnPropertyChanged();
            UpdateFooterBreakdown();
        }
    }
    private bool CompletionSet => _tcs.Task.IsCompleted;

    protected ConfigPageBase()
    {
        Config = new TConfig();
        BindingContext = Config;
        Config.PropertyChanged += OnConfigPropertyChanged;

        ReturnFromConfigCommand = new Command(async () =>
        {
            Complete(BuildResult(Config));
            await StickyFooterControl.DefaultNavigateAsync(this);
        });

        UpdateFooterBreakdown();
    }

    public void ApplyBaseTotal(int baseTotal)
    {
        Config.BaseIsp = baseTotal;
    }

    public void ResetConfig()
    {
        var baseIsp = Config.BaseIsp;
        Config.PropertyChanged -= OnConfigPropertyChanged;
        Config = new TConfig { BaseIsp = baseIsp };
        Config.PropertyChanged += OnConfigPropertyChanged;
        BindingContext = Config;
        UpdateFooterBreakdown();
    }

    protected abstract CalcResult BuildResult(TConfig cfg);

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // OnDisappearing fires when a child page is pushed (search, etc) as well as when this
        // page is popped. Delay and check the navigation stack so we only complete on a true pop.
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(25);

            var stillInStack = Navigation?.NavigationStack?.Contains(this) == true;
            if (!stillInStack && !CompletionSet)
                Complete(null);
        });
    }

    protected override bool OnBackButtonPressed()
    {
        Complete(null);
        return base.OnBackButtonPressed();
    }

    protected void Complete(CalcResult? result)
    {
        if (CompletionSet)
            return;

        _tcs.TrySetResult(result);
    }

    private void OnConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isUpdatingFooter)
            return;

        UpdateFooterBreakdown();
    }

    private void OnCalculatorBreakdownChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_isUpdatingFooter)
            return;

        UpdateFooterBreakdown();
    }

    private void UpdateFooterBreakdown()
    {
        if (_isUpdatingFooter)
            return;

        _isUpdatingFooter = true;
        try
        {
            FooterBreakdownItems.Clear();

            int running = 0;
            if (CalculatorContext is not null)
            {
                foreach (var row in CalculatorContext.BreakdownItems)
                {
                    FooterBreakdownItems.Add(row);
                    running = row.RunningTotal;
                }
            }

            var preview = BuildPreviewRow(running);
            if (preview is not null)
                FooterBreakdownItems.Add(preview);
        }
        finally
        {
            _isUpdatingFooter = false;
        }
    }

    private ContributionRow? BuildPreviewRow(int baseRunning)
    {
        CalcResult previewResult;
        try
        {
            previewResult = BuildResult(Config);
        }
        catch
        {
            return null;
        }

        var summary = previewResult.Summary?.Trim();
        if (string.IsNullOrWhiteSpace(summary))
            return null;

        return new ContributionRow
        {
            Id = "current-preview",
            Text = summary,
            RunningTotal = baseRunning + previewResult.TotalIsp,
        };
    }
}
