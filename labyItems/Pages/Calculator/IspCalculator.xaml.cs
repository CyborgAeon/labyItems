using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using labyItems.Categories;
using labyItems.Controls;
using labyItems.Models;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;

namespace labyItems.Pages.Calculator;

public partial class IspCalculator : TabbedPage, INotifyPropertyChanged
{
    public event Action<int>? TotalChanged;

    private int _baseIsp;
    private readonly List<CalcContribution> _contributions = new();
    private TaskCompletionSource<IspCalculationResult?>? _tcsCalc;
    private double _lastAppliedTabFontSize;
    private double _lastMeasuredWidth;

    public ICommand? ReturnToFormCommand { get; set; }
    public ICommand RemoveContributionCommand { get; }
    public ObservableCollection<ContributionRow> BreakdownItems { get; } = new();
    public event PropertyChangedEventHandler? PropertyChanged;
    public int BaseTotal => _baseIsp;

    private int _total;
    public int Total
    {
        get => _total;
        private set
        {
            if (_total != value)
            {
                _total = value;
                OnPropertyChanged();
            }
        }
    }

    public IspCalculator() : this(0, null)
    {
    }

    public IspCalculator(int baseTotal, IEnumerable<CalcResult>? existingAbilities = null)
    {
        InitializeComponent();
        _baseIsp = baseTotal;
        Total = baseTotal;

        ArmourCategoryPage.BindingContext = this;
        CharmCategoryPage.BindingContext = this;
        ConsumableCategoryPage.BindingContext = this;
        LifeCategoryPage.BindingContext = this;
        WeaponCategoryPage.CalculatorContext = this;

        ArmourCategoryPage.ContributionAdded += AddContribution;
        WeaponCategoryPage.ContributionAdded += AddContribution;
        CharmCategoryPage.ContributionAdded += AddContribution;
        ConsumableCategoryPage.ContributionAdded += AddContribution;
        LifeCategoryPage.ContributionAdded += AddContribution;

        ReturnToFormCommand = new Command(async () => await ExecuteReturnAsync());
        ArmourCategoryPage.ReturnToFormCommand = ReturnToFormCommand;
        CharmCategoryPage.ReturnToFormCommand = ReturnToFormCommand;
        // WeaponCategoryPage.ReturnToFormCommand = ReturnToFormCommand;
        ConsumableCategoryPage.ReturnToFormCommand = ReturnToFormCommand;
        LifeCategoryPage.ReturnToFormCommand = ReturnToFormCommand;

        RemoveContributionCommand = new Command<string>(RemoveContributionById);
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;

        if (existingAbilities != null)
            SeedExisting(existingAbilities);

        UpdateTotal();
    }

    public void UpsertContribution(CalcContribution contribution) => AddContribution(contribution);
    private void AddContribution(CalcContribution contribution)
    {
        var existing = _contributions.FirstOrDefault(c => c.Id == contribution.Id);
        if (existing != null)
            _contributions.Remove(existing);
        _contributions.Add(contribution);
        UpdateTotal();
    }

    private void SeedExisting(IEnumerable<CalcResult> abilities)
    {
        _contributions.Clear();
        var baseAbility = abilities.FirstOrDefault(a =>
            string.Equals(a.AbilityType, "Base", StringComparison.OrdinalIgnoreCase)
        );
        _baseIsp = baseAbility?.TotalIsp ?? _baseIsp;

        foreach (var a in abilities)
        {
            if (string.Equals(a.AbilityType, "Base", StringComparison.OrdinalIgnoreCase))
                continue;

            AddContribution(new CalcContribution(Guid.NewGuid().ToString(), a.AbilityType, a));
        }
    }

    private async Task ExecuteReturnAsync()
    {
        var result = new IspCalculationResult
        {
            TotalIsp = ComputeTotal(),
            Abilities = BuildAbilityList(),
            SummaryText = BuildSummary(),
        };

        // If we're in modal mode (called via GetResultAsync), complete the task
        if (_tcsCalc != null)
        {
            _tcsCalc.TrySetResult(result);
            await Navigation.PopAsync();
        }
        else
        {
            // Direct mode: navigate to summary page
            var summaryPage = new ItemSummaryPage(result.TotalIsp, result.Abilities);
            await Navigation.PushAsync(summaryPage);
        }
    }

    private async void OnReturn(object sender, EventArgs e) => await ExecuteReturnAsync();
    private void OnLoaded(object? sender, EventArgs e) => UpdateTabFontSize();
    private void OnSizeChanged(object? sender, EventArgs e) => UpdateTabFontSize();

    public async Task<IspCalculationResult?> GetResultAsync(INavigation nav)
    {
        _tcsCalc = new TaskCompletionSource<IspCalculationResult?>();
        await nav.PushAsync(this);
        return await _tcsCalc.Task;
    }

    private int ComputeTotal()
    {
        var sum = _baseIsp + _contributions.Sum(c => c.Result.TotalIsp);
        return sum <= 0 ? 0 : sum;
    }

    private void UpdateTotal()
    {
        Total = ComputeTotal();
        TotalChanged?.Invoke(Total);
        RefreshBreakdown();
    }

    private string BuildSummary()
    {
        var lines = new List<string>();

        if (_baseIsp > 0)
            lines.Add($"Base ISP: {_baseIsp}");

        lines.AddRange(_contributions.Select(c => c.Result.Summary));

        if (Total > 0)
            lines.Add($"Total ISP: {Total}");

        return string.Join("\n", lines);
    }

    private List<CalcResult> BuildAbilityList()
    {
        var list = _contributions.Select(c => c.Result).ToList();
        if (_baseIsp > 0)
        {
            list.Insert(
                0,
                new CalcResult
                {
                    AbilityType = "Base",
                    AbilityName = "Manual ISP entry",
                    TotalIsp = _baseIsp,
                    Details = new() { ["source"] = "ItemForm" },
                }
            );
        }
        return list;
    }

    private void RefreshBreakdown()
    {
        BreakdownItems.Clear();
        int running = 0;

        if (_baseIsp > 0)
        {
            running += _baseIsp;
            BreakdownItems.Add(
                new ContributionRow
                {
                    Id = "base",
                    Text = $"Base ISP: {_baseIsp}",
                    RunningTotal = running,
                }
            );
        }

        foreach (var c in _contributions)
        {
            running += c.Result.TotalIsp;
            BreakdownItems.Add(
                new ContributionRow
                {
                    Id = c.Id,
                    Text = c.Result.Summary,
                    RunningTotal = running,
                }
            );
        }
    }

    private void RemoveContributionById(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        var existing = _contributions.FirstOrDefault(c => c.Id == id);
        if (existing is null)
            return;

        _contributions.Remove(existing);
        existing.OnRemove?.Invoke();
        UpdateTotal();
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private void UpdateTabFontSize()
    {
        double width = Width > 0
            ? Width
            : DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;

        // Avoid excessive churn when size hasn't meaningfully changed
        if (Math.Abs(width - _lastMeasuredWidth) < 1 && _lastAppliedTabFontSize > 0)
            return;

        _lastMeasuredWidth = width;
        double targetFontSize = CalculateTabFontSize(width);

        if (Math.Abs(targetFontSize - _lastAppliedTabFontSize) < 0.1)
            return;

        _lastAppliedTabFontSize = targetFontSize;
        MainThread.BeginInvokeOnMainThread(() => ApplyPlatformTabFontSize(targetFontSize));
    }

    private double CalculateTabFontSize(double availableWidth)
    {
        if (availableWidth <= 0 || Children.Count == 0)
            return 12;

        double perTabWidth = availableWidth / Children.Count;

        // Scale font size linearly between small and large widths
        const double minFont = 11;
        const double maxFont = 14;
        const double minWidth = 70;  // typical small-phone width per tab
        const double maxWidth = 140; // roomy tablet width per tab

        double clamped = Math.Clamp(perTabWidth, minWidth, maxWidth);
        double ratio = (clamped - minWidth) / (maxWidth - minWidth);

        return Math.Round(minFont + (maxFont - minFont) * ratio, 1);
    }

    partial void ApplyPlatformTabFontSize(double fontSize);
}
