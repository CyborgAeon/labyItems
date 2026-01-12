using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using labyItems.Models;
using labyItems.Pages.Calculator;
using labyItems.Pages.Configs;

using System.Windows.Input;

namespace labyItems.Pages.Calculator.CalcNav;
    public partial class CharmNav : ContentPage
    {
        public event Action<CalcContribution>? ContributionAdded;
        private readonly List<CalcContribution> _contributions = new();
        private bool _categoryLocked;
        private readonly Dictionary<Type, object> _cachedPages = new();
        public static readonly BindableProperty TotalProperty =
        BindableProperty.Create(
            nameof(Total),
            typeof(int),
            typeof(CharmNav),
            0);

    public int Total
    {
        get => (int)GetValue(TotalProperty);
        set => SetValue(TotalProperty, value);
    }

        public static readonly BindableProperty ReturnToFormCommandProperty =
    BindableProperty.Create(
        nameof(ReturnToFormCommand),
        typeof(ICommand),
        typeof(CharmNav),
        null);

public ICommand? ReturnToFormCommand
{
    get => (ICommand?)GetValue(ReturnToFormCommandProperty);
    set => SetValue(ReturnToFormCommandProperty, value);
}
        public CharmNav()
        {
            InitializeComponent();
            ComputeTotal();
        }
private async Task OnReturnCommand(){
            var result = new CalcResult
            {
                TotalIsp = ComputeTotal(),
                Summary  = BuildSummary()
            };

            await Navigation.PopAsync();
}
private async void OnReturn(object sender, EventArgs e) => OnReturnCommand();

        private async void OnCharmEarthPower(object sender, EventArgs e)
            => await HandleCharmAsync<EvocationConfigPage, EvocationConfig>();
        private async void OnCharmGeneral(object sender, EventArgs e)
            => await HandleCharmAsync<GeneralConfigPage, GeneralConfig>();
        private async void OnCharmMagic(object sender, EventArgs e)
            => await HandleCharmAsync<SpellConfigPage, SpellConfig>();
        private async void OnCharmSpirit(object sender, EventArgs e)
            => await HandleCharmAsync<MiracleConfigPage, MiracleConfig>();

private TPage GetOrCreatePage<TPage, TConfig>()
    where TPage   : ConfigPageBase<TConfig>, new()
    where TConfig : ConfigBase, new()
{
    if (_cachedPages.TryGetValue(typeof(TPage), out var existing) && existing is TPage page)
        return page;

    var created = new TPage();
    _cachedPages[typeof(TPage)] = created;
    return created;
}

private async Task HandleCharmAsync<TPage, TConfig>()
    where TPage   : ConfigPageBase<TConfig>, new()
    where TConfig : ConfigBase, new()
{
    if (_categoryLocked) return;
    _categoryLocked = true;

    var cfgPage = GetOrCreatePage<TPage, TConfig>();
    cfgPage.CalculatorContext = BindingContext as IspCalculator;
    cfgPage.ApplyBaseTotal(GetBaseIsp());
    var completion = cfgPage.Completion;
    await Navigation.PushAsync(cfgPage);

    var cfg = await completion;
    _categoryLocked = false;
    if (cfg is null) return;

    var pageName = typeof(TPage).Name; 
    var sourceName = pageName
        .Replace("ConfigPage", string.Empty) 
        .Replace("Config", string.Empty);

    var id = $"Charm/{sourceName}";
    var added = new CalcContribution(
        Id: id,
        Source: id,
        Result: cfg,
        OnRemove: cfgPage.ResetConfig);

    AddOrReplaceContribution(added);
}

        public void UpdateTotal() => ComputeTotal();

        private int ComputeTotal()
        {
            var sum = _contributions.Sum(c => c.Result.TotalIsp);
            Total = sum <= 0 ? 0 : sum;
            return Total;
        }

        private string BuildSummary()
        {
            var lines = new List<string>();
            foreach (var c in _contributions)
                lines.Add(c.Result.Summary);

            var total = ComputeTotal();
            if (total > 0)
                lines.Add($"Total ISP: {total}");

            return string.Join("\n", lines);
        }

        private int GetBaseIsp() =>
            (BindingContext as IspCalculator)?.BaseTotal ?? 0;

        private void AddOrReplaceContribution(CalcContribution contribution)
        {
            var existing = _contributions.FirstOrDefault(c => c.Id == contribution.Id);
            if (existing != null)
                _contributions.Remove(existing);

            _contributions.Add(contribution);
            ContributionAdded?.Invoke(contribution);
            UpdateTotal();
        }
    }
