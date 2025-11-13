using labyItems.Models;
using labyItems.Controls;
using labyItems.Pages.Configs;
using System.Windows.Input;
using System.Collections.Generic;
using System.Linq;
namespace labyItems.Pages.Calculator;

public partial class SpellConfigPage : ConfigPageBase<SpellConfig>
{
    public SpellConfigPage()
    {
        InitializeComponent();
    }
    
 private async void OnFooterReturnClicked(object sender, EventArgs e)
        {
            if (BindingContext is not SpellConfig cfg) return;

            var result = new CalcResult
            {
                TotalIsp = cfg.Total,
                Summary  = BuildSummary(cfg)
            };

            if (Navigation?.NavigationStack?.Count > 1)
            {
                _tcs.TrySetResult(result);
                await Navigation.PopAsync();
                return;
            }

            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync("..");
                return;
            }

            _tcs.TrySetResult(result);
            await Navigation.PopAsync();
        }

    protected override string BuildSummary(SpellConfig cfg)
    {
        var basic = cfg.BasicPerDay > 0 ? $"Basic x{cfg.BasicPerDay}" : null;
        var adv = cfg.PublishedPerDay > 0 ? $"Advanced x{cfg.PublishedPerDay}" : null;
        var addAnyPow = cfg.AdditionalGenericMana > 0 ? $"Additional {cfg.AdditionalGenericMana} mana store" : null;
        var addPow = cfg.AdditionalManaOfColour > 0 ? $"Additional {cfg.AdditionalManaOfColour} {cfg.AdditionalManaColour} mana store" : null;
        var under6th = cfg.TurnHandbookToSixthMantic > 0 ? $"Turn handbook→6th mantic {cfg.TurnHandbookToSixthMantic}/day" : null;
        var basicMantic = cfg.TurnAnyBasicMantic > 0 ? $"Any basic→mantic {cfg.TurnAnyBasicMantic}/day" : null;
        var anyMantic = cfg.TurnAnyPublishedMantic > 0 ? $"Any spell→mantic {cfg.TurnAnyPublishedMantic}/day" : null;

        var tags = new List<string?>
        {
            basic, adv, addAnyPow, addPow, under6th, basicMantic, anyMantic,
            cfg.AddBasicToBaseList ? "+Basic" : null,
            cfg.AddAdvancedToBaseList ? "+Advanced" : null,
            cfg.InnateIsMantic ? "innates mantic ×4" : null,
            cfg.PowerStoreRegenerates ? "store regenerates +25" : null,
            cfg.IsTeachingScroll ? $"teaching scroll (2×Power={2 * cfg.Power})" : null
        }.Where(s => !string.IsNullOrWhiteSpace(s));

        var name = string.IsNullOrWhiteSpace(cfg.SpellName) ? "Spell" : cfg.SpellName;
        var tagText = string.Join(", ", tags);
        return $"{name}: {tagText} → {cfg.Total} ISP";
    }

    private async void OnSearchSpell(object sender, EventArgs e)
    {
        var picked = await new Spell().PickAsync(Navigation);
        if (picked == null) return;

        if (BindingContext is SpellConfig cfg)
            cfg.ApplySpell(picked);
    }
}
