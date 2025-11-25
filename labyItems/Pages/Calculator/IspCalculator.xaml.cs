using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using labyItems.Categories;
using labyItems.Controls;
using labyItems.Models;

namespace labyItems.Pages.Calculator;

public partial class IspCalculator : TabbedPage, INotifyPropertyChanged
{
    public event Action<int>? TotalChanged;

    private int _baseIsp;
    private readonly List<CalcContribution> _contributions = new();
    private TaskCompletionSource<IspCalculationResult?>? _tcsCalc;

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

        if (existingAbilities != null)
            SeedExisting(existingAbilities);

        UpdateTotal();
    }

    public void UpsertContribution(CalcContribution contribution) => AddContribution(contribution);

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

        _tcsCalc?.TrySetResult(result);
        await Navigation.PopAsync();
    }

    private async void OnReturn(object sender, EventArgs e) => await ExecuteReturnAsync();

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

    private void AddContribution(CalcContribution contribution)
    {
        var existing = _contributions.FirstOrDefault(c => c.Id == contribution.Id);
        if (existing != null)
            _contributions.Remove(existing);
        _contributions.Add(contribution);
        UpdateTotal();
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
}
