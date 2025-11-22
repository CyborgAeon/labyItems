using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using labyItems.Categories;
using labyItems.Controls;
using labyItems.Models;

namespace labyItems.Pages.Calculator;

public partial class IspCalculator : TabbedPage, INotifyPropertyChanged
{
    public event Action<int>? TotalChanged;

    private readonly List<CalcContribution> _contributions = new();
    private int _baseIsp;

    private TaskCompletionSource<CalcResult?>? _tcs; // or whatever your result type is

    public ICommand? ReturnToFormCommand { get; set; }
    public event PropertyChangedEventHandler? PropertyChanged;

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

        _baseIsp = baseTotal;
        Total = baseTotal;

        ArmourCategoryPage.BindingContext = this;
        WeaponCategoryPage.BindingContext = this;
        CharmCategoryPage.BindingContext = this;
        ConsumableCategoryPage.BindingContext = this;
        LifeCategoryPage.BindingContext = this;

        ArmourCategoryPage.ContributionAdded += c =>
        {
            _contributions.Add(c);
            UpdateTotal();
        };
        WeaponCategoryPage.ContributionAdded += c =>
        {
            _contributions.Add(c);
            UpdateTotal();
        };
        CharmCategoryPage.ContributionAdded += c =>
        {
            _contributions.Add(c);
            UpdateTotal();
        };
        ConsumableCategoryPage.ContributionAdded += c =>
        {
            _contributions.Add(c);
            UpdateTotal();
        };
        LifeCategoryPage.ContributionAdded += c =>
        {
            _contributions.Add(c);
            UpdateTotal();
        };

        ReturnToFormCommand = new Command(async () => await ExecuteReturnAsync());
        ArmourCategoryPage.ReturnToFormCommand = ReturnToFormCommand;
        WeaponCategoryPage.ReturnToFormCommand = ReturnToFormCommand;
        CharmCategoryPage.ReturnToFormCommand = ReturnToFormCommand;
        ConsumableCategoryPage.ReturnToFormCommand = ReturnToFormCommand;
        LifeCategoryPage.ReturnToFormCommand = ReturnToFormCommand;

        if (existingAbilities != null && existingAbilities.Any())
        {
            SeedExisting(existingAbilities);
        }
        else
        {
            UpdateTotal(); // just baseIsp
        }
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

            _contributions.Add(
                new CalcContribution(
                    Id: Guid.NewGuid().ToString(),
                    Source: a.AbilityType,
                    Result: a
                )
            );
        }

        UpdateTotal();
    }

    private async Task ExecuteReturnAsync()
    {
        var abilities = new List<CalcResult>();

        if (_baseIsp > 0)
        {
            abilities.Add(
                new CalcResult
                {
                    AbilityType = "Base",
                    AbilityName = "Base ISP",
                    TotalIsp = _baseIsp,
                    // Details, Summary, etc.
                }
            );
        }

        abilities.AddRange(_contributions.Select(c => c.Result));

        var result = new CalcResult
        {
            TotalIsp = ComputeTotal(),
            Abilities = abilities,
            SummaryText = BuildSummary(),
        };

        _tcs?.TrySetResult(result);
        await Navigation.PopAsync();
    }

    // Existing toolbar handler just delegates:
    private async void OnReturn(object sender, EventArgs e) => await ExecuteReturnAsync();

    public async Task<IspCalculationResult?> GetResultAsync(INavigation nav)
    {
        _tcsCalc = new TaskCompletionSource<IspCalculationResult?>();
        await nav.PushAsync(this);
        return await _tcsCalc.Task;
    }

    private int ComputeTotal()
    {
        var sum = _baseIsp + _contributions.Sum(c => c.Isp);
        return sum <= 0 ? 0 : sum;
    }

    private void UpdateTotal()
    {
        var total = ComputeTotal();
        Total = total; // this is what StickyFooter binds to
        TotalChanged?.Invoke(total);
    }

    private string BuildSummary()
    {
        var lines = new List<string>();

        if (_baseIsp > 0)
            lines.Add($"Base ISP: {_baseIsp}");

        lines.AddRange(_contributions.Select(c => c.Label));

        var total = ComputeTotal();
        if (total > 0)
            lines.Add($"Total ISP: {total}");

        return string.Join("\n", lines);
    }

    private List<CalcResult> BuildAbilityList()
    {
        var list = _contributions.Select(c => c.Result).ToList();
        if (_baseTotal > 0)
        {
            list.Insert(
                0,
                new CalcResult
                {
                    AbilityType = "Base",
                    AbilityName = "Manual ISP entry",
                    TotalIsp = _baseTotal,
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

        if (_baseTotal > 0)
        {
            running += _baseTotal;
            BreakdownItems.Add(
                new ContributionRow
                {
                    Id = "base",
                    Text = $"Base ISP: {_baseTotal}",
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

        if (id == "base")
        {
            _baseTotal = 0;
            UpdateTotal();
            return;
        }

        var existing = _contributions.FirstOrDefault(c => c.Id == id);
        if (existing is null)
            return;

        _contributions.Remove(existing);
        existing.OnRemove?.Invoke();
        UpdateTotal();
    }
}
