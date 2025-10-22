using System.Text;
using labyItems.Models;

namespace labyItems.Pages;

public partial class IspCalculator : ContentPage
{
    // ---- Result DTO returned to the form
    public class CalcResult
    {
        public int TotalIsp { get; set; }
        public string Summary { get; set; } = string.Empty;
    }

    private TaskCompletionSource<CalcResult?>? _tcs;

    // Data sources for Weapon
    private List<CalcRow> _magic = new();
    private List<CalcRow> _spirit = new();
    private List<CalcRow> _other = new();
    private readonly List<Evocation.Result> _chosenEarthPowers = new();

    public IspCalculator()
    {            
        InitializeComponent();

        // Start with Weapon section
        BuildWeaponRows();
        MagicRows.ItemsSource = _magic;
        SpiritRows.ItemsSource = _spirit;
        OtherRows.ItemsSource = _other;

        // Category toggles: only Weapon implemented now
        RbWeapon.CheckedChanged += (_, e) => WeaponSection.IsVisible = RbWeapon.IsChecked;
        RbArmour.CheckedChanged += (_, e) => WeaponSection.IsVisible = RbWeapon.IsChecked;
        RbCharm.CheckedChanged += (_, e) => { WeaponSection.IsVisible = false; CharmSection.IsVisible = RbCharm.IsChecked; };
        RbConsumable.CheckedChanged += (_, e) => WeaponSection.IsVisible = RbWeapon.IsChecked;

        UpdateTotal();
    }

    private async void OnEarthPower(object sender, EventArgs e)
    {
        var picker = new Evocation();
        var picked = await picker.PickAsync(Navigation);
        if (picked == null) return;

        _chosenEarthPowers.Add(picked);

        // Show a small summary under the button
        EarthPowerCharmPickedLabel.IsVisible = true;
        EarthPowerCharmPickedLabel.Text =
            string.Join("\n", _chosenEarthPowers.Select(p =>
                $"{p.Name} (Power {p.Power}) — {string.Join(", ", p.Fields)}"));

        // If you want these to affect ISP, add their power here and refresh total:
        // _earthPowerIsp = _chosenEarthPowers.Sum(p => p.Power);
        // UpdateTotal();
    }

    // Expose a task so the opener can await a result
    public async Task<CalcResult?> GetResultAsync(INavigation nav)
    {
        _tcs = new TaskCompletionSource<CalcResult?>();
        await nav.PushAsync(this);
        return await _tcs.Task;
    }
    private void BuildWeaponRows()
    {
        // MAGIC / VS GROUP
        _magic = new List<CalcRow>
        {
            new() { Name = "+0 Magic, +1 vs one group", Cost = 30, AllowMultiple = false },
            new() { Name = "+1 Magic", Cost = 40, AllowMultiple = false },
            new() { Name = "+2 Magic", Cost = 60, AllowMultiple = false },
            new() { Name = "+0 Pure Magic", Cost = 40, AllowMultiple = false },

            // “Add Non-Grey Colour/Hue of damage to magical weapon (can add multiple)”  +3/colour
            new() { Name = "Add magical colour/hue (each)", Cost = 3, AllowMultiple = true },
        };

        // SPIRIT
        _spirit = new List<CalcRow>
        {
            new() { Name = "+0 Spirit (any one alignment)", Cost = 25, AllowMultiple = false },
            new() { Name = "+0 Spirit, +1 vs one type", Cost = 30, AllowMultiple = false },
            new() { Name = "+0 Spirit, +1 vs one group", Cost = 35, AllowMultiple = false },
            new() { Name = "+1 Spirit", Cost = 50, AllowMultiple = false },
            new() { Name = "+2 Spirit", Cost = 75, AllowMultiple = false },
            new() { Name = "+0 Pure Spirit", Cost = 45, AllowMultiple = false },

            // “Add non-opposite alignment of damage to spiritual weapon” +5
            new() { Name = "Add non-opposite spirit alignment", Cost = 5, AllowMultiple = true },
        };

        // MANTIC / PHYSICAL / SPECIALS
        _other = new List<CalcRow>
        {
            new() { Name = "+0 Mantic (any one colour & any one alignment)", Cost = 50, AllowMultiple = false },
            new() { Name = "+1 Mantic", Cost = 100, AllowMultiple = false },
            new() { Name = "+0 Pure Mantic", Cost = 90, AllowMultiple = false },
            new() { Name = "+1 Physical", Cost = 20, AllowMultiple = false },

            new() { Name = "Magic/Spirit weapon turns PURE 1/day for 5 mins", Cost = 5, AllowMultiple = false },
            new() { Name = "Mantic weapon turns PURE 1/day for 5 mins", Cost = 10, AllowMultiple = false },

            new() { Name = "Inflicts adventure perm damage 1/day (5 mins)", Cost = 25, AllowMultiple = false },
            new() { Name = "Inflicts damage ‘thru’ PAC at all times", Cost = 30, AllowMultiple = false },
            new() { Name = "Oversize weapon can be blade-sharpened", Cost = 5, AllowMultiple = false },
            new() { Name = "Supernatural weapon can be blade-sharpened", Cost = 10, AllowMultiple = false },

            new() { Name = "Cut through Opponent’s Aura of Defence 1/day (5 mins)", Cost = 25, AllowMultiple = false },
        };
        // Call after (re)building data
        HookRows(_magic);
        HookRows(_spirit);
        HookRows(_other);
    }

    void HookRows(IEnumerable<CalcRow> rows)
    {
        foreach (var r in rows)
            r.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(CalcRow.Count))
                    UpdateTotal();
            };
    }
    
    private void RefreshCollections()
    {
        // force UI to refresh counts
        MagicRows.ItemsSource = null; 
        MagicRows.ItemsSource = _magic;
        SpiritRows.ItemsSource = null; 
        SpiritRows.ItemsSource = _spirit;
        OtherRows.ItemsSource = null; 
        OtherRows.ItemsSource = _other;
    }

    private int ComputeTotal()
    {
        // Sum selected rows × cost, then round to multiples of 5 (spec says: cost values are multiples of 5; we’ll enforce)
        int sum = _magic.Sum(r => r.Count * r.Cost)
                + _spirit.Sum(r => r.Count * r.Cost)
                + _other.Sum(r => r.Count * r.Cost);

        // Safety: round to nearest 5 above zero
        if (sum <= 0) return 0;
        return (int)(Math.Round(sum / 5.0, MidpointRounding.AwayFromZero) * 5);
    }

    private void UpdateTotal()
    {
        TotalLabel.Text = $"Total: {ComputeTotal()} ISP";
    }

    private string BuildSummary()
    {
        var sb = new StringBuilder();
        void add(IEnumerable<CalcRow> rows, string header)
        {
            var selected = rows.Where(r => r.Count > 0).ToList();
            if (!selected.Any()) return;
            sb.AppendLine(header + ":");
            foreach (var r in selected)
            {
                var countPart = r.AllowMultiple ? $" x{r.Count}" : "";
                sb.AppendLine($"• {r.Name}{countPart} ({r.Cost * r.Count} ISP)");
            }
        }

        add(_magic, "Magic");
        add(_spirit, "Spirit");
        add(_other, "Other");

        var total = ComputeTotal();
        if (total > 0)
        {
            sb.AppendLine($"Total ISP: {total}");
        }
        return sb.ToString().Trim();
    }

    private async void OnReturn(object sender, EventArgs e)
    {
        var result = new CalcResult
        {
            TotalIsp = ComputeTotal(),
            Summary = BuildSummary()
        };

        _tcs?.TrySetResult(result);
        await Navigation.PopAsync();
    }
}
