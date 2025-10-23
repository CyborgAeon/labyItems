using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace labyItems.Pages.Configs
{
    public abstract class ConfigBase : INotifyPropertyChanged
    {
        private string _name = "";
        private int _power;
        private int _basicPerDay;
        private int _advancedPerDay;
        private bool _addBasic;
        private bool _addAdvanced;
        private bool _addPrep;

        // Common properties
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value, affectsTotal: false, alsoNotify: nameof(Title));
        }

        // Override to customize the “none selected” text in Title
        protected virtual string NoneSelectedText => "Item (none selected)";
        public string Title => string.IsNullOrWhiteSpace(Name) ? NoneSelectedText : Name;

        public int Power
        {
            get => _power;
            set => SetProperty(ref _power, value, affectsTotal: true);
        }

        public int BasicPerDay
        {
            get => _basicPerDay;
            set => SetProperty(ref _basicPerDay, value, affectsTotal: true);
        }

        public int AdvancedPerDay
        {
            get => _advancedPerDay;
            set => SetProperty(ref _advancedPerDay, value, affectsTotal: true);
        }

        public bool AddBasic
        {
            get => _addBasic;
            set => SetProperty(ref _addBasic, value, affectsTotal: true);
        }

        public bool AddAdvanced
        {
            get => _addAdvanced;
            set => SetProperty(ref _addAdvanced, value, affectsTotal: true);
        }

        public bool AddPrep
        {
            get => _addPrep;
            set => SetProperty(ref _addPrep, value, affectsTotal: true);
        }

        // --- Calculation pipeline ---
        // 1) Base shared cost
        protected virtual double BaseTotal()
        {
            double t = 0;
            t += 2 * Power * BasicPerDay;
            t += 3 * Power * AdvancedPerDay;
            if (AddBasic) t += 15;
            if (AddAdvanced) t += 18;
            if (AddPrep) t *= 1.5;
            return t;
        }

        // 2) Config-specific extra additive cost
        protected abstract double ExtraTotal();

        // 3) Config-specific multipliers (if any)
        protected virtual double ApplyMultipliers(double total) => total;

        public virtual int Total
            => (int)System.Math.Round(ApplyMultipliers(BaseTotal() + ExtraTotal()));

        // --- INotifyPropertyChanged helpers ---
        protected bool SetProperty<T>(ref T storage, T value,
            bool affectsTotal,
            [CallerMemberName] string? propertyName = null,
            params string[] alsoNotify)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
                return false;

            storage = value;
            OnPropertyChanged(propertyName);
            foreach (var n in alsoNotify) OnPropertyChanged(n);
            if (affectsTotal) OnPropertyChanged(nameof(Total));
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
