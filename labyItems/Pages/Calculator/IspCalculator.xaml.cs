using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using labyItems.Categories;
using labyItems.Controls;
using labyItems.Helpers;
using labyItems.Models;
using labyItems.Pages.Calculator.CalcNav;
using labyItems.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;

namespace labyItems.Pages.Calculator;

public partial class IspCalculator : TabbedPage
{
    public event Action<int>? TotalChanged;

    private int _baseIsp;
    private readonly List<CalcContribution> _contributions = new();
    private TaskCompletionSource<IspCalculationResult?>? _tcsCalc;
    private readonly Func<IspCalculationResult, Task>? _onSave;
    private double _lastAppliedTabFontSize;
    private double _lastMeasuredWidth;
    private bool _isHandlingBackTabSelection;
    private Page? _lastNonBackTab;
    private bool _chromeConfigured;
    private bool _iosChromePinned;
    private readonly Dictionary<string, ContentPage> _tabPlaceholders = new(StringComparer.Ordinal);
    private ArmourNav? _armourCategoryPage;
    private WeaponConfigPage? _weaponCategoryPage;
    private CharmNav? _charmCategoryPage;
    private LifeConfigPage? _lifeCategoryPage;
    private MoreNav? _moreCategoryPage;

    public ICommand? ReturnToFormCommand { get; set; }
    public ICommand RemoveContributionCommand { get; }
    public ObservableCollection<ContributionRow> BreakdownItems { get; } = new();
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

    public IspCalculator(
        int baseTotal,
        IEnumerable<CalcResult>? existingAbilities = null,
        Func<IspCalculationResult, Task>? onSave = null)
    {
        InitializeComponent();

        _baseIsp = baseTotal;
        _onSave = onSave;
        Total = baseTotal;

        ReturnToFormCommand = new Command(async () => await ExecuteSaveAsync());
        RemoveContributionCommand = new Command<string>(RemoveContributionById);

        CurrentPageChanged += OnCurrentPageChanged;
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;

        ConfigureChromeIfNeeded();
        InitializeTabs();

        if (existingAbilities != null)
            SeedExisting(existingAbilities);

        UpdateTotal();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ConfigureChromeIfNeeded();
        EnsureIosChromePinned();
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
            string.Equals(a.AbilityType, "Base", StringComparison.OrdinalIgnoreCase));
        _baseIsp = baseAbility?.TotalIsp ?? _baseIsp;

        foreach (var ability in abilities)
        {
            if (string.Equals(ability.AbilityType, "Base", StringComparison.OrdinalIgnoreCase))
                continue;

            AddContribution(new CalcContribution(Guid.NewGuid().ToString(), ability.AbilityType, ability));
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

        if (_tcsCalc != null)
        {
            _tcsCalc.TrySetResult(result);
            await Navigation.PopAsync();
            return;
        }

        if (_onSave != null)
        {
            try
            {
                await _onSave(result);
                await Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                RuntimeLog.Write("ISP_SAVE_CALLBACK", "Character item save callback failed.", ex);
                await DisplayAlert("Save failed", ex.Message, "OK");
            }

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

            var payload = ItemEmailService.BuildItemPayload(item, result.Abilities);
            item.PayloadJson = ItemEmailService.SerializeItemPayload(payload);

            LiteDbService.InsertItem(item);
            await DisplayAlert("Saved", "Item saved to wallet.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Save failed", ex.Message, "OK");
        }
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        UpdateTabFontSize();
        EnsureIosChromePinned();
    }

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
                    if (_armourCategoryPage != null && Children.Contains(_armourCategoryPage))
                        return _armourCategoryPage;

                    return Children.FirstOrDefault(page => !ReferenceEquals(page, BackTab));
                },
                getIsNavigationInProgress: () => _isHandlingBackTabSelection,
                setIsNavigationInProgress: value => _isHandlingBackTabSelection = value,
                lastNonBackTab: _lastNonBackTab);
            return;
        }

        if (TryRealizeLazyTab(CurrentPage, out var realizedPage))
        {
            _lastNonBackTab = realizedPage;
            EnsureIosChromePinned();
            return;
        }

        _lastNonBackTab = CurrentPage;
        EnsureIosChromePinned();
    }

    public async Task<IspCalculationResult?> GetResultAsync(INavigation nav)
    {
        _tcsCalc = new TaskCompletionSource<IspCalculationResult?>();
        await nav.PushAsync(this);
        return await _tcsCalc.Task;
    }

    public int GetTotalExcludingContribution(string? contributionId)
    {
        var targetId = (contributionId ?? string.Empty).Trim();
        var sum = _baseIsp + _contributions
            .Where(c => !string.Equals(c.Id, targetId, StringComparison.Ordinal))
            .Sum(c => c.Result.TotalIsp);

        return sum <= 0 ? 0 : sum;
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
                });
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

        if (abilities.Any(a =>
            string.Equals(a.AbilityType, "Spell", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a.AbilityType, "Magic", StringComparison.OrdinalIgnoreCase)))
        {
            return ItemTypeEnum.Magic;
        }

        if (abilities.Any(a =>
            string.Equals(a.AbilityType, "Miracle", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a.AbilityType, "Spirit", StringComparison.OrdinalIgnoreCase)))
        {
            return ItemTypeEnum.Spirit;
        }

        if (abilities.Any(a =>
            string.Equals(a.AbilityType, "Evocation", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a.AbilityType, "Earthpower", StringComparison.OrdinalIgnoreCase)))
        {
            return ItemTypeEnum.EarthPower;
        }

        return ItemTypeEnum.Other;
    }

    private void RefreshBreakdown()
    {
        BreakdownItems.Clear();
        var running = 0;

        if (_baseIsp > 0)
        {
            running += _baseIsp;
            BreakdownItems.Add(new ContributionRow
            {
                Id = "base",
                Text = $"Base ISP: {_baseIsp}",
                RunningTotal = running,
            });
        }

        foreach (var contribution in _contributions)
        {
            running += contribution.Result.TotalIsp;
            BreakdownItems.Add(new ContributionRow
            {
                Id = contribution.Id,
                Text = contribution.Result.Summary,
                RunningTotal = running,
            });
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
        base.OnPropertyChanged(name);

    private void UpdateTabFontSize()
    {
        var width = Width > 0
            ? Width
            : DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;

        if (Math.Abs(width - _lastMeasuredWidth) < 1 && _lastAppliedTabFontSize > 0)
            return;

        _lastMeasuredWidth = width;
        var targetFontSize = CalculateTabFontSize(width);

        if (Math.Abs(targetFontSize - _lastAppliedTabFontSize) < 0.1)
            return;

        _lastAppliedTabFontSize = targetFontSize;
        MainThread.BeginInvokeOnMainThread(() => ApplyPlatformTabFontSize(targetFontSize));
    }

    private double CalculateTabFontSize(double availableWidth)
    {
        if (availableWidth <= 0 || Children.Count == 0)
            return 12;

        var perTabWidth = availableWidth / Children.Count;

        const double minFont = 11;
        const double maxFont = 14;
        const double minWidth = 70;
        const double maxWidth = 140;

        var clamped = Math.Clamp(perTabWidth, minWidth, maxWidth);
        var ratio = (clamped - minWidth) / (maxWidth - minWidth);

        return Math.Round(minFont + (maxFont - minFont) * ratio, 1);
    }

    private void InitializeTabs()
    {
        Children.Clear();
        Children.Add(BackTab);

        var armourPage = GetOrCreateArmourCategoryPage();
        Children.Add(armourPage);
        Children.Add(CreateLazyTabPlaceholder("Weapon"));
        Children.Add(CreateLazyTabPlaceholder("Charm"));
        Children.Add(CreateLazyTabPlaceholder("Life"));
        Children.Add(CreateLazyTabPlaceholder("More"));

        CurrentPage = armourPage;
        _lastNonBackTab = armourPage;
    }

    private void ConfigureChromeIfNeeded()
    {
        if (_chromeConfigured)
            return;

        TabbedPageChromeHelper.ApplyHiddenNavigation(this);
        TabbedPageChromeHelper.ConfigureTabPageChrome(BackTab);
        _chromeConfigured = true;
    }

    private void ConfigureTabPage(Page page) => TabbedPageChromeHelper.ConfigureTabPageChrome(page);

    private void EnsureIosChromePinned()
    {
        if (_iosChromePinned)
            return;

        _iosChromePinned = true;
        IosTabBarHelper.EnsurePinnedToTop(this);
    }

    private ContentPage CreateLazyTabPlaceholder(string title)
    {
        var placeholder = new ContentPage
        {
            Title = title,
            Content = new Grid()
        };

        ConfigureTabPage(placeholder);
        _tabPlaceholders[title] = placeholder;
        return placeholder;
    }

    private bool TryRealizeLazyTab(Page? selectedPage, out Page realizedPage)
    {
        realizedPage = selectedPage ?? BackTab;
        if (selectedPage == null)
            return false;

        var title = selectedPage.Title ?? string.Empty;
        if (!_tabPlaceholders.TryGetValue(title, out var placeholder) || !ReferenceEquals(placeholder, selectedPage))
            return false;

        realizedPage = title switch
        {
            "Weapon" => GetOrCreateWeaponCategoryPage(),
            "Charm" => GetOrCreateCharmCategoryPage(),
            "Life" => GetOrCreateLifeCategoryPage(),
            "More" => GetOrCreateMoreCategoryPage(),
            _ => selectedPage
        };

        var index = Children.IndexOf(placeholder);
        if (index < 0)
            return false;

        Children.RemoveAt(index);
        Children.Insert(index, realizedPage);
        _tabPlaceholders.Remove(title);
        CurrentPage = realizedPage;
        return true;
    }

    private ArmourNav GetOrCreateArmourCategoryPage()
    {
        if (_armourCategoryPage != null)
            return _armourCategoryPage;

        var page = new ArmourNav
        {
            Title = "Armour",
            BindingContext = this,
            ReturnToFormCommand = ReturnToFormCommand
        };

        page.ContributionAdded += AddContribution;
        ConfigureTabPage(page);
        _armourCategoryPage = page;
        return page;
    }

    private WeaponConfigPage GetOrCreateWeaponCategoryPage()
    {
        if (_weaponCategoryPage != null)
            return _weaponCategoryPage;

        var page = new WeaponConfigPage
        {
            Title = "Weapon",
            CalculatorContext = this
        };

        page.ContributionAdded += AddContribution;
        ConfigureTabPage(page);
        _weaponCategoryPage = page;
        return page;
    }

    private CharmNav GetOrCreateCharmCategoryPage()
    {
        if (_charmCategoryPage != null)
            return _charmCategoryPage;

        var page = new CharmNav
        {
            Title = "Charm",
            BindingContext = this,
            ReturnToFormCommand = ReturnToFormCommand
        };

        page.ContributionAdded += AddContribution;
        ConfigureTabPage(page);
        _charmCategoryPage = page;
        return page;
    }

    private LifeConfigPage GetOrCreateLifeCategoryPage()
    {
        if (_lifeCategoryPage != null)
            return _lifeCategoryPage;

        var page = new LifeConfigPage
        {
            Title = "Life",
            BindingContext = this,
            ReturnToFormCommand = ReturnToFormCommand
        };

        page.ContributionAdded += AddContribution;
        ConfigureTabPage(page);
        _lifeCategoryPage = page;
        return page;
    }

    private MoreNav GetOrCreateMoreCategoryPage()
    {
        if (_moreCategoryPage != null)
            return _moreCategoryPage;

        var page = new MoreNav
        {
            Title = "More",
            CalculatorContext = this,
            ReturnToFormCommand = ReturnToFormCommand
        };

        page.ContributionAdded += AddContribution;
        ConfigureTabPage(page);
        _moreCategoryPage = page;
        return page;
    }

    partial void ApplyPlatformTabFontSize(double fontSize);
}
