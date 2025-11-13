using labyItems.Models;
using labyItems.Categories;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
namespace labyItems.Pages.Calculator;

public partial class IspCalculator : TabbedPage, INotifyPropertyChanged
{

    public event Action<int>? TotalChanged;
    private readonly List<CalcContribution> _contributions = new();
    private TaskCompletionSource<CalcResult?>? _tcs;
    public ICommand? ReturnToFormCommand { get; set;}
public event PropertyChangedEventHandler? PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private int _total;
        public int Total { get => _total; private set { if (_total != value) { _total = value; OnPropertyChanged(); } } }

    public IspCalculator(ItemTypeEnum category)
    {

        InitializeComponent();
        ArmourCategoryPage.BindingContext = this;
        WeaponCategoryPage.BindingContext = this;
        CharmCategoryPage.BindingContext = this;
        ConsumableCategoryPage.BindingContext = this;
        LifeCategoryPage.BindingContext = this;

        ArmourCategoryPage.ContributionAdded += c => { _contributions.Add(c); UpdateTotal(); };
        WeaponCategoryPage.ContributionAdded += c => { _contributions.Add(c); UpdateTotal(); };
        CharmCategoryPage.ContributionAdded  += c => { _contributions.Add(c); UpdateTotal(); };
        ConsumableCategoryPage.ContributionAdded += c => { _contributions.Add(c); UpdateTotal(); };
        LifeCategoryPage.ContributionAdded   += c => { _contributions.Add(c); UpdateTotal(); };
        
        ReturnToFormCommand = new Command(async () => await ExecuteReturnAsync());
        ArmourCategoryPage.ReturnToFormCommand  = ReturnToFormCommand;
        WeaponCategoryPage.ReturnToFormCommand  = ReturnToFormCommand;
        CharmCategoryPage.ReturnToFormCommand   = ReturnToFormCommand;
        ConsumableCategoryPage.ReturnToFormCommand = ReturnToFormCommand;
        LifeCategoryPage.ReturnToFormCommand    = ReturnToFormCommand;
    }

    private async Task ExecuteReturnAsync()
    {
        var result = new CalcResult
        {
            TotalIsp = ComputeTotal(),
            Summary  = BuildSummary()
        };

        _tcs?.TrySetResult(result);
        await Navigation.PopAsync();
    }

    // Existing toolbar handler just delegates:
    private async void OnReturn(object sender, EventArgs e)
        => await ExecuteReturnAsync();

    public async Task<CalcResult?> GetResultAsync(INavigation nav)
    {
        _tcs = new TaskCompletionSource<CalcResult?>();
        await nav.PushAsync(this);
        return await _tcs.Task;
    }

    private void UpdateTotal()
    {
        var total = ComputeTotal();
        TotalChanged?.Invoke(total);
    }

    private int ComputeTotal()
    {
        var sum = _contributions.Sum(c => c.Isp);
        return sum <= 0 ? 0 : sum;
    }

    private string BuildSummary()
    {
        var lines = _contributions.Select(c => c.Label).ToList();
        var total = ComputeTotal();
        if (total > 0) lines.Add($"Total ISP: {total}");
        return string.Join("\n", lines);
    }
}
