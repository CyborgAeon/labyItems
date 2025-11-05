using System.ComponentModel;
using labyItems.Services;

namespace labyItems.Pages.Configs
{
    public class EvocationConfig : ConfigBase
    {
        private int _drawOnEpPerDay;

        // Keep your existing public API by aliasing Name <-> EvocationName
        public string EvocationName
        {
            get => Name;
            set => Name = value;
        }

        // Optional: keep Power/BasicPerDay/AdvancedPerDay/Add* in base

        public int DrawOnEpPerDay
        {
            get => _drawOnEpPerDay;
            set => SetProperty(ref _drawOnEpPerDay, value, affectsTotal: true);
        }

        protected override string NoneSelectedText => "Evocation (none selected)";

        protected override double ExtraTotal()
        {
            double t = 0;
            t += 16 * DrawOnEpPerDay;
            return t;
        }

        // No extra multipliers for evocation
        protected override double ApplyMultipliers(double total) => total;

        public void ApplyEvocation(Evocation.Result picked)
        {
            EvocationName = picked.Name;
            Power = picked.Power;
            IsAdvanced = picked.IsAdvanced;
        }
    }
}
