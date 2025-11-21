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
        private bool? _isAdvanced;
        private bool? _isImmune;
        private int _baseIsp;

        // Carries the starting ISP from the form into config pages for display-only totals.
        public int BaseIsp
        {
            get => _baseIsp;
            set => SetProperty(ref _baseIsp, value, affectsTotal: false, alsoNotify: nameof(TotalWithBase));
        }

        // Common properties
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value, affectsTotal: false, alsoNotify: nameof(Title));
        }

        public bool? IsAdvanced
        {
            get => _isAdvanced;
            set => SetProperty(ref _isAdvanced, value, affectsTotal: false, alsoNotify: nameof(Title));
        }
        public bool? IsImmune
        {
            get => _isImmune;
            set => SetProperty(ref _isImmune, value, affectsTotal: false, alsoNotify: nameof(Title));
        }

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

        protected virtual int BaseTotal()
        {
            int t = 0;
            t += 2 * Power * BasicPerDay;
            t += 3 * Power * AdvancedPerDay;
            if (AddBasic) { t += 15; return t; }
            if (AddAdvanced) { t += 18; return t; }
            if (AddPrep) { t += t / 2; return t; }
            return t;
        }

        protected abstract int ExtraTotal();
        protected virtual int ApplyMultipliers(int total) => total;
        public virtual int Total =>
            BaseTotal() + ExtraTotal();
        public int TotalWithBase => BaseIsp + Total;

        // --- INotifyPropertyChanged helpers ---
        protected bool SetProperty<T>(ref T storage, T value,
            bool affectsTotal,
            [CallerMemberName] string? propertyName = null,
            params string[] alsoNotify)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
                return false;

            storage = value;
            if (affectsTotal)
            {
                OnPropertyChanged(nameof(Total));
                OnPropertyChanged(nameof(TotalWithBase));
            }
            foreach (var n in alsoNotify) OnPropertyChanged(n);
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
