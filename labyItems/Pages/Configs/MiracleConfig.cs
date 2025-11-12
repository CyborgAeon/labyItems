using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using labyItems.Models.DTOs;

namespace labyItems.Pages.Configs
{
    public class MiracleConfig : INotifyPropertyChanged
    {
        // ------------ Identity / header ------------
        private string _miracleName = "Miracle";
        public string MiracleName
        {
            get => _miracleName;
            set { if (_miracleName != value) { _miracleName = value; OnPropertyChanged(); OnPropertyChanged(nameof(Title)); } }
        }

        private int _power;
        public int Power
        {
            get => _power;
            set
            {
                var v = Math.Max(0, value);
                if (_power != v)
                {
                    _power = v;
                    OnPropertyChanged();
                    Recalculate();
                }
            }
        }

        private bool? _isAdvanced;   // nullable to support “unset”
        public bool? IsAdvanced
        {
            get => _isAdvanced;
            set { if (_isAdvanced != value) { _isAdvanced = value; OnPropertyChanged(); Recalculate(); } }
        }

        private string _alignment = "";
        public string Alignment
        {
            get => _alignment;
            set { if (_alignment != value) { _alignment = value; OnPropertyChanged(); } }
        }

        public string Title => $"{MiracleName} ({Power} SP)";

        // ------------ Innates ------------
        private int _basicPerDay;
        public int BasicPerDay
        {
            get => _basicPerDay;
            set { var v = Math.Clamp(value, 0, 100); if (_basicPerDay != v) { _basicPerDay = v; OnPropertyChanged(); OnPropertyChanged(nameof(HasInnates)); Recalculate();  } }
        }

        private int _advancedPerDay;
        public int AdvancedPerDay
        {
            get => _advancedPerDay;
            set { var v = Math.Clamp(value, 0, 100); if (_advancedPerDay != v) { _advancedPerDay = v; OnPropertyChanged(); OnPropertyChanged(nameof(HasInnates)); Recalculate(); } }
        }
        public bool HasInnates => (BasicPerDay > 0) || (AdvancedPerDay > 0);
        private bool _innateIsMantic;
        public bool InnateIsMantic
        {
            get => _innateIsMantic;
            set { if (_innateIsMantic != value) { _innateIsMantic = value; OnPropertyChanged(); Recalculate(); } }
        }

        // ------------ Spirit store ------------
        private int _generalSpiritStore;   // slider 0..12
        public int GeneralSpiritStore
        {
            get => _generalSpiritStore;
            set { var v = Math.Clamp(value, 0, 12); if (_generalSpiritStore != v) { _generalSpiritStore = v; OnPropertyChanged(); OnPropertyChanged(nameof(AnySpiritStore)); Recalculate(); } }
        }

        private int _sphereSpiritStore;    // slider 0..12
        public int SphereSpiritStore
        {
            get => _sphereSpiritStore;
            set { var v = Math.Clamp(value, 0, 12); if (_sphereSpiritStore != v) { _sphereSpiritStore = v; OnPropertyChanged(); OnPropertyChanged(nameof(AnySpiritStore)); Recalculate(); } }
        }

        public bool AnySpiritStore => (GeneralSpiritStore > 0) || (SphereSpiritStore > 0);

        private bool _spiritStoreRegenerates;   // only visible if AnySpiritStore
        public bool SpiritStoreRegenerates
        {
            get => _spiritStoreRegenerates;
            set { if (_spiritStoreRegenerates != value) { _spiritStoreRegenerates = value; OnPropertyChanged(); Recalculate(); } }
        }

        // ------------ Base list flags ------------
        private bool _addBasicToList;      // only if IsAdvanced == false
        public bool AddBasicToList
        {
            get => _addBasicToList;
            set { if (_addBasicToList != value) { _addBasicToList = value; OnPropertyChanged(); Recalculate(); } }
        }

        private bool _addAdvancedToList;   // only if IsAdvanced == true
        public bool AddAdvancedToList
        {
            get => _addAdvancedToList;
            set { if (_addAdvancedToList != value) { _addAdvancedToList = value; OnPropertyChanged(); Recalculate(); } }
        }

        private bool _addWithPrep30;       // always visible
        public bool AddWithPrep30
        {
            get => _addWithPrep30;
            set { if (_addWithPrep30 != value) { _addWithPrep30 = value; OnPropertyChanged(); Recalculate(); } }
        }

        // ------------ Mantic conversions ------------
        private int _turnBasicUpTo5thMantic;
        public int TurnBasicUpTo5thMantic
        {
            get => _turnBasicUpTo5thMantic;
            set { var v = Math.Max(0, value); if (_turnBasicUpTo5thMantic != v) { _turnBasicUpTo5thMantic = v; OnPropertyChanged(); Recalculate(); } }
        }

        private int _turnBasicMantic;
        public int TurnBasicMantic
        {
            get => _turnBasicMantic;
            set { var v = Math.Max(0, value); if (_turnBasicMantic != v) { _turnBasicMantic = v; OnPropertyChanged(); Recalculate(); } }
        }

        private int _turnAdvancedUpTo6thMantic;
        public int TurnAdvancedUpTo6thMantic
        {
            get => _turnAdvancedUpTo6thMantic;
            set { var v = Math.Max(0, value); if (_turnAdvancedUpTo6thMantic != v) { _turnAdvancedUpTo6thMantic = v; OnPropertyChanged(); Recalculate(); } }
        }

        private int _turnAdvancedAbove6thMantic;
        public int TurnAdvancedAbove6thMantic
        {
            get => _turnAdvancedAbove6thMantic;
            set { var v = Math.Max(0, value); if (_turnAdvancedAbove6thMantic != v) { _turnAdvancedAbove6thMantic = v; OnPropertyChanged(); Recalculate(); } }
        }

        // ------------ Teaching / believer ------------
        private bool _isTeachingScroll;
        public bool IsTeachingScroll
        {
            get => _isTeachingScroll;
            set { if (_isTeachingScroll != value) { _isTeachingScroll = value; OnPropertyChanged(); Recalculate(); } }
        }

        private int _trueBeliever;
        public int TrueBeliever
        {
            get => _trueBeliever;
            set { var v = Math.Max(0, value); if (_trueBeliever != v) { _trueBeliever = v; OnPropertyChanged(); Recalculate(); } }
        }

        private int _total;
        public int Total { get => _total; private set { if (_total != value) { _total = value; OnPropertyChanged(); } } }

        private string _breakdown = "";
        public string Breakdown { get => _breakdown; private set { if (_breakdown != value) { _breakdown = value; OnPropertyChanged(); } } }

        public void ApplyMiracle(Miracle.Result picked)
        {
            Power = picked.Power;
            MiracleName = string.IsNullOrWhiteSpace(picked.Name) ? "Miracle" : picked.Name;
            IsAdvanced = picked.IsAdvanced;
            Alignment = picked.Alignment;
        }

        public void Recalculate()
        {
            int total = 0;
            var sb = new StringBuilder();

            // Innates
            int innates = (2 * Power * Math.Max(0, BasicPerDay)) +
                          (3 * Power * Math.Max(0, AdvancedPerDay));
            if (innates > 0)
            {
                if (InnateIsMantic)
                {
                    int before = innates;
                    innates *= 4;
                    sb.AppendLine($"+ Innates of *mantic* {MiracleName}: (2×{Power}×{BasicPerDay} + 3×{Power}×{AdvancedPerDay}) × 4 = {innates} (was {before})");
                }
                else
                {
                    sb.AppendLine($"+ Innates of {MiracleName}: 2×{Power}×{BasicPerDay} + 3×{Power}×{AdvancedPerDay} = {innates}");
                }
                total += innates;
            }

            // Spirit stores
            if (GeneralSpiritStore > 0)
            {
                int c = 4 * GeneralSpiritStore;
                total += c;
                sb.AppendLine($"+ General spirit store: 4 × {GeneralSpiritStore} = {c}");
            }
            if (SphereSpiritStore > 0)
            {
                int c = 3 * SphereSpiritStore;
                total += c;
                sb.AppendLine($"+ Sphere spirit store: 3 × {SphereSpiritStore} = {c}");
            }
            if (AnySpiritStore && SpiritStoreRegenerates)
            {
                total += 25;
                sb.AppendLine("+ Spirit store regenerates: 25");
            }

            // Base list flags
            bool isAdv = IsAdvanced == true;
            bool isBasic = IsAdvanced == false;

            if (isBasic && AddBasicToList)
            {
                total += 15;
                sb.AppendLine("+ Add basic miracle to list: 15");
            }
            if (isAdv && AddAdvancedToList)
            {
                total += 18;
                sb.AppendLine("+ Add advanced miracle to list: 18");
            }

            if (AddWithPrep30)
            {
                int c = (int)Math.Round(Power / 2.0, MidpointRounding.AwayFromZero);
                total += c;
                sb.AppendLine($"+ Add miracle to list with 30s prep: {Power}/2 = {c}");
            }

            // Mantic conversions
            if (TurnBasicUpTo5thMantic > 0)
            {
                int c = 40 * TurnBasicUpTo5thMantic;
                total += c;
                sb.AppendLine($"+ Turn *basic* miracle up to 5th mantic: 40 × {TurnBasicUpTo5thMantic} = {c}");
            }
            if (TurnBasicMantic > 0)
            {
                int c = 50 * TurnBasicMantic;
                total += c;
                sb.AppendLine($"+ Turn *basic* miracle mantic: 50 × {TurnBasicMantic} = {c}");
            }
            if (TurnAdvancedUpTo6thMantic > 0)
            {
                int c = 60 * TurnAdvancedUpTo6thMantic;
                total += c;
                sb.AppendLine($"+ Turn *advanced* miracle up to 6th mantic: 60 × {TurnAdvancedUpTo6thMantic} = {c}");
            }
            if (TurnAdvancedAbove6thMantic > 0)
            {
                int c = 80 * TurnAdvancedAbove6thMantic;
                total += c;
                sb.AppendLine($"+ Turn *advanced* miracle above 6th mantic: 80 × {TurnAdvancedAbove6thMantic} = {c}");
            }

            // Teaching scroll
            if (IsTeachingScroll)
            {
                int c = 3 * Power;
                total += c;
                sb.AppendLine($"+ Teaching scroll: 3 × Power ({Power}) = {c}");
            }

            // True believer
            if (TrueBeliever > 0)
            {
                int c = 16 * TrueBeliever;
                total += c;
                sb.AppendLine($"+ True believer: 16 × {TrueBeliever} = {c}");
            }

            Total = total;
            Breakdown = sb.ToString().TrimEnd();
        }

        // INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
