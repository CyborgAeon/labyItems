using System.Text;
using labyItems.Models;
using labyItems.Pages.Configs;

namespace labyItems.Pages;

public sealed record CalcContribution(string Source, string Label, int Isp);
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
    private readonly List<EvocationConfig> _chosenEarthPowers = new();
    private readonly List<CalcContribution> _contributions = new();
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
        var picker = new Evocation(); // your search page
        var picked = await picker.PickAsync(Navigation);
        if (picked == null) return;

        // new: configure this evocation
        var configPage = new EvocationConfigPage(picked);
        var config = await configPage.Completion;
        if (config == null) return;

        _contributions.Add(new CalcContribution(
    Source: "Charm/Evocation",
    Label: $"{config.BaseEvocation.Name}: Basic x{config.BasicPerDay}, " +
           $"Advanced x{config.AdvancedPerDay}" +
           $"{(config.AddBasic ? ", +Basic" : "")}" +
           $"{(config.AddAdvanced ? ", +Advanced" : "")}" +
           $"{(config.AddPrep ? ", +30s prep" : "")} → {config.Total} ISP",
    Isp: config.Total));

        // Optional: show a quick summary in-page
        EarthPowerCharmPickedLabel.IsVisible = true;
        EarthPowerCharmPickedLabel.Text = string.Join("\n", _contributions
            .Where(c => c.Source == "Charm/Evocation")
            .Select(c => c.Label));

        // Recompute the calculator total (now includes this evocation)
        UpdateTotal();
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
        // base rows in this page (weapon/spirit/other)
        int baseRows =
        (_magic?.Sum(r => r.Count * r.Cost) ?? 0) +
        (_spirit?.Sum(r => r.Count * r.Cost) ?? 0) +
        (_other?.Sum(r => r.Count * r.Cost) ?? 0);

        // ALL extras from subflows (Evocations etc.)
        int extras = _contributions.Sum(c => c.Isp);

        int sum = baseRows + extras;
        if (sum <= 0) return 0;

        // keep your “multiples of 5” rule if you want it
        return (int)(Math.Round(sum / 5.0, MidpointRounding.AwayFromZero) * 5);
    }

    private void UpdateTotal()
    {
        TotalLabel.Text = $"Total: {ComputeTotal()} ISP";
    }

    private string BuildSummary()
    {
        var lines = new List<string>();

        // Example: summarize the rows that were selected in this page
        void addRows(string header, IEnumerable<CalcRow> rows)
        {
            var picked = rows.Where(r => r.Count > 0).ToList();
            if (picked.Count == 0) return;
            lines.Add(header + ":");
            lines.AddRange(picked.Select(r =>
                $"• {r.Name}{(r.AllowMultiple ? $" x{r.Count}" : "")} ({r.Cost * r.Count} ISP)"));
        }

        addRows("Magic", _magic);
        addRows("Spirit", _spirit);
        addRows("Other", _other);

        // Add every contribution (Evocation configs, Miracles, Spells, etc.)
        if (_contributions.Count > 0)
        {
            lines.Add("Charm:");
            lines.AddRange(_contributions.Select(c => $"• {c.Label}"));
        }

        var total = ComputeTotal();
        if (total > 0) lines.Add($"Total ISP: {total}");

        return string.Join("\n", lines);
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
