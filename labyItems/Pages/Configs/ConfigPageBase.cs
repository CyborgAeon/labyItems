using Microsoft.Maui.Controls;
using System.Threading.Tasks;
using labyItems.Controls;
using labyItems.Models;

namespace labyItems.Pages.Configs;
    public abstract class ConfigPageBase<TConfig> : ContentPage
        where TConfig : ConfigBase, new()
    {
        protected readonly TaskCompletionSource<CalcResult?> _tcs = new();
        public Task<CalcResult?> Completion => _tcs.Task;

        public TConfig Config { get; }
        public Command ReturnFromConfigCommand { get; }

        protected ConfigPageBase()
        {
            Config = new TConfig();
            BindingContext = Config;

            ReturnFromConfigCommand = new Command(async () =>
            {
                var result = new CalcResult
                {
                    TotalIsp = Config.Total,
                    Summary  = BuildSummary(Config)
                };

                _tcs.TrySetResult(result);
                await StickyFooterControl.DefaultNavigateAsync(this);
            });
        }

        public void ApplyBaseTotal(int baseTotal)
        {
            Config.BaseIsp = baseTotal;
        }

        protected abstract string BuildSummary(TConfig cfg);
    }
