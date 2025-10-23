using labyItems.Models;
using labyItems.Pages.Configs;
using System.Windows.Input;

namespace labyItems.Pages;

public partial class SpellConfigPage : ContentPage
{
    private readonly TaskCompletionSource<CalcResult?> _tcs = new();
    public Task<CalcResult?> Completion => _tcs.Task;

    public SpellConfigPage()
    {
        InitializeComponent();
        BindingContext = new SpellConfig();
    }

    private string BuildSummary(SpellConfig cfg)
    {
        // One-line summary for contribution list:
        var basic = cfg.BasicPerDay > 0 ? $"Basic x{cfg.BasicPerDay}" : null;
        var adv = cfg.AdvancedPerDay > 0 ? $"Advanced x{cfg.AdvancedPerDay}" : null;
        var addAnyPow = cfg.AdditionalPower > 0 ? $"Additional {cfg.AdditionalPower} mana store" : null;
        var addPow = cfg.AdditionalPowerOfColour.Item1 > 0 ? $"Additional {cfg.AdditionalPowerOfColour.Item1} {cfg.AdditionalPowerOfColour.Item2} mana store" : null;
        var makeUnder7thMantic = cfg.MakeSpellUnder7thMantic > 0 ? $"Turn handbook or basic new colour spell to 6th mantic {cfg.MakeSpellUnder7thMantic}/day" : null;
        var makeBasicMantic = cfg.MakeBasicSpellMantic > 0 ? $"Turn any basic spell mantic {cfg.MakeBasicSpellMantic}/day" : null;
        var makeAdvMantic = cfg.MakeAdvancedSpellMantic > 0 ? $"Turn any spell mantic {cfg.MakeAdvancedSpellMantic}/day" : null;

        var tags = new List<string?>([
            basic, adv,
            cfg.AddBasic ? "+Basic" : null,
            cfg.AddAdvanced ? "+Advanced" : null,
            cfg.AddPrep ? "+30s prep" : null,
            cfg.InnateIsMantic ? "turn innate mantic" : null,
            cfg.PowerStoreRegenerates ? "Power gained in the specific power store regenerates at 1/15 minutes" : null,
            cfg.IsTeachingScroll ? "Teaching scroll of a spell the character could already learn & read." : null,
        ]).Where(s => !string.IsNullOrWhiteSpace(s));

        var tagText = string.Join(", ", tags);
        var name = string.IsNullOrWhiteSpace(cfg.SpellName) ? "Spell" : cfg.SpellName;
        return $"{name}: {tagText} → {cfg.Total} ISP";
    }

    private async void OnSearchSpell(object sender, EventArgs e)
    {
        var picked = await new Spell().PickAsync(Navigation);
        if (picked == null) return;

        if (BindingContext is SpellConfig cfg)
            cfg.ApplySpell(picked);
    }

    private async void OnReturn(object sender, EventArgs e)
    {
        var cfg = (SpellConfig)BindingContext;
        var res = new CalcResult
        {
            TotalIsp = cfg.Total,
            Summary = BuildSummary(cfg)
        };

        _tcs.TrySetResult(res);
        await Navigation.PopAsync();
    }
}
