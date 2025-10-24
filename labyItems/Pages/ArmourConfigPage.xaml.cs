using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using labyItems.Models.Enums;

namespace labyItems.Pages
{
    public partial class ArmourConfigPage : ContentPage
    {
        private readonly TaskCompletionSource<CalcResult?> _tcs = new();
        public Task<CalcResult?> Completion => _tcs.Task;

        public ArmourConfigPage()
        {
            InitializeComponent();
            BindingContext = new ArmourConfig();
        }
        
        private string BuildSummary(ArmourConfig cfg)
        {
            var kind = cfg.SelectedArmour switch
            {
                ArmourKind.MagicalMasterCrafted => "Magical MC Armour",
                ArmourKind.SpiritualMasterCrafted => "Spiritual MC Armour",
                ArmourKind.ManticMasterCrafted => "Mantic MC Armour",
                _ => "No Armour"
            };

            var tags = new List<string>();
            if (cfg.ACBase > 0) tags.Add($"AC {cfg.ACBase}");
            if (cfg.SelectedArmour == ArmourKind.MagicalMasterCrafted && cfg.MagicalColoursCount > 0)
                tags.Add($"+{2 * cfg.MagicalColoursCount} (colours)");
            if (cfg.SelectedArmour == ArmourKind.SpiritualMasterCrafted && cfg.SpiritualNonOpposite)
                tags.Add("+3 (non-opposite)");

            if (cfg.PAC > 0) tags.Add($"PAC {cfg.PAC}");
            if (cfg.DAC > 0) tags.Add($"DAC {cfg.DAC}");
            if (cfg.MAC > 0) tags.Add($"MAC {cfg.MAC}");
            if (cfg.SAC > 0) tags.Add($"SAC {cfg.SAC}");

            var tagText = tags.Count > 0 ? $" [{string.Join(", ", tags)}]" : "";
            return $"{kind}{tagText} → {cfg.Total} ISP";
        }

        // ---- Event handlers to wire from XAML ----

        private void OnCalculate(object sender, EventArgs e)
        {
            var cfg = (ArmourConfig)BindingContext;
            cfg.Recalculate();
        }

        private void OnArmourChanged(object sender, EventArgs e)
        {
            var cfg = (ArmourConfig)BindingContext;
            cfg.Recalculate();
        }

        private void OnAcBaseChanged(object sender, TextChangedEventArgs e)
        {
            var cfg = (ArmourConfig)BindingContext;
            cfg.ACBase = TryParseInt(e.NewTextValue);
            cfg.Recalculate();
        }

        private void OnMagicalColoursChanged(object sender, TextChangedEventArgs e)
        {
            var cfg = (ArmourConfig)BindingContext;
            cfg.MagicalColoursCount = Math.Max(0, TryParseInt(e.NewTextValue));
            cfg.Recalculate();
        }

        private void OnSpiritualNonOppositeToggled(object sender, ToggledEventArgs e)
        {
            var cfg = (ArmourConfig)BindingContext;
            cfg.SpiritualNonOpposite = e.Value;
            cfg.Recalculate();
        }

        private void OnPacChanged(object sender, ValueChangedEventArgs e)
        {
            var cfg = (ArmourConfig)BindingContext;
            cfg.PAC = Clamp0To6((int)e.NewValue);
            cfg.Recalculate();
        }

        private void OnDacChanged(object sender, ValueChangedEventArgs e)
        {
            var cfg = (ArmourConfig)BindingContext;
            cfg.DAC = Clamp0To6((int)e.NewValue);
            cfg.Recalculate();
        }

        private void OnMacChanged(object sender, ValueChangedEventArgs e)
        {
            var cfg = (ArmourConfig)BindingContext;
            cfg.MAC = Clamp0To6((int)e.NewValue);
            cfg.Recalculate();
        }

        private void OnSacChanged(object sender, ValueChangedEventArgs e)
        {
            var cfg = (ArmourConfig)BindingContext;
            cfg.SAC = Clamp0To6((int)e.NewValue);
            cfg.Recalculate();
        }

        private async void OnReturn(object sender, EventArgs e)
        {
            var cfg = (ArmourConfig)BindingContext;
            var res = new CalcResult
            {
                TotalIsp = cfg.Total,
                Summary = BuildSummary(cfg)
            };
            _tcs.TrySetResult(res);
            await Navigation.PopAsync();
        }

        // ---- helpers ----
        private static int TryParseInt(string? s) => int.TryParse(s, out var v) ? v : 0;
        private static int Clamp0To6(int v) => Math.Min(6, Math.Max(0, v));
    }
}
