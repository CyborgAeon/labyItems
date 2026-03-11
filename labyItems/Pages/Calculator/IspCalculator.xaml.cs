using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using labyItems.Categories;
using labyItems.Controls;
using labyItems.Helpers;
using labyItems.Models;
using labyItems.Services;
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
    private bool _isHandlingBackTabSelection;
    private Page? _lastNonBackTab;

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

    public IspCalculator(int baseTotal, IEnumerable<CalcResult>? existingAbilities = null)
    {
        InitializeComponent();
        TabbedPageChromeHelper.ApplyHiddenNavigation(this);
        foreach (var page in Children)
            TabbedPageChromeHelper.ConfigureTabPageChrome(page);

        _baseIsp = baseTotal;
        Total = baseTotal;

        ArmourCategoryPage.BindingContext = this;
        CharmCategoryPage.BindingContext = this;
        // Consumable tab temporarily removed per request:
        // ConsumableCategoryPage.BindingContext = this;
        LifeCategoryPage.BindingContext = this;
        MoreCategoryPage.CalculatorContext = this;
        WeaponCategoryPage.CalculatorContext = this;

        ArmourCategoryPage.ContributionAdded += AddContribution;
        WeaponCategoryPage.ContributionAdded += AddContribution;
        CharmCategoryPage.ContributionAdded += AddContribution;
        // Consumable tab temporarily removed per request:
        // ConsumableCategoryPage.ContributionAdded += AddContribution;
        LifeCategoryPage.ContributionAdded += AddContribution;
        MoreCategoryPage.ContributionAdded += AddContribution;

        ReturnToFormCommand = new Command(async () => await ExecuteSaveAsync());
        ArmourCategoryPage.ReturnToFormCommand = ReturnToFormCommand;
        CharmCategoryPage.ReturnToFormCommand = ReturnToFormCommand;
        // WeaponCategoryPage.ReturnToFormCommand = ReturnToFormCommand;
        // Consumable tab temporarily removed per request:
        // ConsumableCategoryPage.ReturnToFormCommand = ReturnToFormCommand;
        LifeCategoryPage.ReturnToFormCommand = ReturnToFormCommand;
        MoreCategoryPage.ReturnToFormCommand = ReturnToFormCommand;

        RemoveContributionCommand = new Command<string>(RemoveContributionById);
        CurrentPageChanged += OnCurrentPageChanged;
        CurrentPage = ArmourCategoryPage;
        _lastNonBackTab = ArmourCategoryPage;
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;

        if (existingAbilities != null)
            SeedExisting(existingAbilities);

        UpdateTotal();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        TabbedPageChromeHelper.ApplyHiddenNavigation(this);
        foreach (var page in Children)
            TabbedPageChromeHelper.ConfigureTabPageChrome(page);
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

    private async Task ExecuteSaveAsync()
    {
        var result = new IspCalculationResult
        {
            TotalIsp = ComputeTotal(),
            Abilities = BuildAbilityList(),
            SummaryText = BuildSummary(),
        };

        // Legacy flow: calculator opened from ItemForm and expecting a callback result.
        if (_tcsCalc != null)
        {
            _tcsCalc.TrySetResult(result);
            await Navigation.PopAsync();
            return;
        }

        try
        {
            var walletLines = BuildWalletSummaryLines(result);
            var item = new Item
            {
                ItemType = ResolveWalletItemType(result.Abilities),
                Maker = new Character
                {
                    Name = "Quick ISP",
                    PlayerName = string.Empty
                },
                Description = string.Join("\n", walletLines),
                Isp = result.TotalIsp,
                CreatedDate = DateTime.Now
            };

            LiteDbService.InsertItem(item);
            await DisplayAlert("Saved", "Item saved to wallet.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Save failed", ex.Message, "OK");
        }
    }

    private void OnLoaded(object? sender, EventArgs e) => UpdateTabFontSize();
    private void OnSizeChanged(object? sender, EventArgs e) => UpdateTabFontSize();

    private void OnCurrentPageChanged(object? sender, EventArgs e)
    {
        if (CurrentPage == null)
            return;

        if (ReferenceEquals(CurrentPage, BackTab))
        {
            _ = TabbedPageChromeHelper.HandleBackTabSelectionAsync(
                owner: this,
                backTab: BackTab,
                fallbackFactory: () =>
                {
                    if (Children.Contains(ArmourCategoryPage))
                        return ArmourCategoryPage;

                    return Children.FirstOrDefault(page => !ReferenceEquals(page, BackTab));
                },
                getIsNavigationInProgress: () => _isHandlingBackTabSelection,
                setIsNavigationInProgress: value => _isHandlingBackTabSelection = value,
                lastNonBackTab: _lastNonBackTab);
            return;
        }

        _lastNonBackTab = CurrentPage;
    }

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

    private List<string> BuildWalletSummaryLines(IspCalculationResult result)
    {
        var lines = new List<string>
        {
            $"ISP total: {result.TotalIsp}"
        };

        if (BreakdownItems.Count > 0)
        {
            lines.Add("ISP breakdown:");
            lines.AddRange(BreakdownItems
                .Select(row => (row.Text ?? string.Empty).Trim())
                .Where(text => text.Length > 0));
        }
        else if (!string.IsNullOrWhiteSpace(result.SummaryText))
        {
            lines.AddRange(result.SummaryText
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line)));
        }

        return lines;
    }

    private static ItemTypeEnum ResolveWalletItemType(IEnumerable<CalcResult>? abilities)
    {
        if (abilities == null)
            return ItemTypeEnum.None;

        bool hasMagic = abilities.Any(a =>
            string.Equals(a.AbilityType, "Spell", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a.AbilityType, "Magic", StringComparison.OrdinalIgnoreCase));
        if (hasMagic)
            return ItemTypeEnum.Magic;

        bool hasSpirit = abilities.Any(a =>
            string.Equals(a.AbilityType, "Miracle", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a.AbilityType, "Spirit", StringComparison.OrdinalIgnoreCase));
        if (hasSpirit)
            return ItemTypeEnum.Spirit;

        bool hasEarthpower = abilities.Any(a =>
            string.Equals(a.AbilityType, "Evocation", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a.AbilityType, "Earthpower", StringComparison.OrdinalIgnoreCase));
        if (hasEarthpower)
            return ItemTypeEnum.EarthPower;

        return ItemTypeEnum.Other;
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
