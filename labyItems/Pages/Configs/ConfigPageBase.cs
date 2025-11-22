using Microsoft.Maui.Controls;
using System.Threading.Tasks;
using labyItems.Controls;
using labyItems.Models;
using labyItems.Pages.Calculator;

namespace labyItems.Pages.Configs;
    public abstract class ConfigPageBase<TConfig> : ContentPage
        where TConfig : ConfigBase, new()
    {
        protected readonly TaskCompletionSource<CalcResult?> _tcs = new();
        public Task<CalcResult?> Completion => _tcs.Task;

        public TConfig Config { get; private set; }
        public Command ReturnFromConfigCommand { get; }
        public Pages.Calculator.IspCalculator? CalculatorContext { get; set; }

        protected ConfigPageBase()
        {
            Config = new TConfig();
            BindingContext = Config;

            ReturnFromConfigCommand = new Command(async () =>
            {
                var result = BuildResult(Config);

                _tcs.TrySetResult(result);
                await StickyFooterControl.DefaultNavigateAsync(this);
            });
        }

        public void ApplyBaseTotal(int baseTotal)
        {
            Config.BaseIsp = baseTotal;
        }

        public void ResetConfig()
        {
            var baseIsp = Config.BaseIsp;
            Config = new TConfig { BaseIsp = baseIsp };
            BindingContext = Config;
        }

        protected abstract CalcResult BuildResult(TConfig cfg);
    }
