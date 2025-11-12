using System.ComponentModel;
using labyItems.Services;
using labyItems.Pages.Calculator;
namespace labyItems.Pages.Configs;
    public class EvocationConfig : ConfigBase
    {
        protected override int ExtraTotal() => Total + (DrawOnEpPerDay * 16);
        private int _drawOnEpPerDay;
        public int DrawOnEpPerDay
        {
            get => _drawOnEpPerDay;
            set => SetProperty(ref _drawOnEpPerDay, value, affectsTotal: true);
        }
        
        public string EvocationName { get; set; } = "";
        protected override string NoneSelectedText => "Evocation (none selected)";

        // No extra multipliers for evocation
        protected override int ApplyMultipliers(int total) => total;

        public void ApplyEvocation(Evocation.Result picked)
        {
            Name = $"{picked.Name} ({picked.Power} EP)";
            EvocationName = picked.Name;
            Power = picked.Power;
            IsAdvanced = picked.IsAdvanced;
        }
    }