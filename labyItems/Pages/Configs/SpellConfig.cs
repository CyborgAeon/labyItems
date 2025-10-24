using System.ComponentModel;

namespace labyItems.Pages.Configs
{
    public class SpellConfig : ConfigBase
    {
        public string SpellName
        {
            get => Name;
            set => Name = value;
        }

        private int _makeSpellUnder7thMantic;
        private int _makeBasicSpellMantic;
        private int _makeAdvancedSpellMantic;
        private bool _innateIsMantic;
        private int _additionalPower;
        private (int, string) _additionalPowerOfColour;
        private bool _powerStoreRegenerates;
        private bool _isTeachingScroll;

        public int MakeSpellUnder7thMantic
        {
            get => _makeSpellUnder7thMantic;
            set => SetProperty(ref _makeSpellUnder7thMantic, value, affectsTotal: true);
        }
        public int MakeBasicSpellMantic
        {
            get => _makeBasicSpellMantic;
            set => SetProperty(ref _makeBasicSpellMantic, value, affectsTotal: true);
        }
        public int MakeAdvancedSpellMantic
        {
            get => _makeAdvancedSpellMantic;
            set => SetProperty(ref _makeAdvancedSpellMantic, value, affectsTotal: true);
        }

        public bool InnateIsMantic
        {
            get => _innateIsMantic;
            set => SetProperty(ref _innateIsMantic, value, affectsTotal: true);
        }

        public int AdditionalPower
        {
            get => _additionalPower;
            set => SetProperty(ref _additionalPower, value, affectsTotal: true);
        }

        // If you still hold a tuple elsewhere, expose a flat int for calculation/binding.
        public (int, string) AdditionalPowerOfColour
        {
            get => _additionalPowerOfColour;
            set => SetProperty(ref _additionalPowerOfColour, value, affectsTotal: true);
        }

        public bool PowerStoreRegenerates
        {
            get => _powerStoreRegenerates;
            set => SetProperty(ref _powerStoreRegenerates, value, affectsTotal: true);
        }

        public bool IsTeachingScroll
        {
            get => _isTeachingScroll;
            set => SetProperty(ref _isTeachingScroll, value, affectsTotal: true);
        }

        protected override string NoneSelectedText => "Spell (none selected)";

        protected override double ExtraTotal()
        {
            double t = 0;
            t += 4 * AdditionalPower;
            t += 3 * AdditionalPowerOfColour.Item1;
            t += 40 * MakeSpellUnder7thMantic;
            t += 50 * MakeBasicSpellMantic;
            t += 80 * MakeAdvancedSpellMantic;
            if (PowerStoreRegenerates) t += 15;
            // IsTeachingScroll currently doesn't affect ISP per your code; add here if needed.
            return t;
        }

        protected override double ApplyMultipliers(double total)
        {
            // The mantic flag multiplies the final total by 4 (as in your original code)
            if (InnateIsMantic) total *= 4;
            return total;
        }

        public void ApplySpell(Spell.Result picked)
        {
            SpellName = picked.Name;
            Power = picked.Power;
        }
    }
}
