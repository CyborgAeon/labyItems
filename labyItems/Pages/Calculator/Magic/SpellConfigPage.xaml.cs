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

            var result = BuildResult(cfg);

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

    protected override CalcResult BuildResult(SpellConfig cfg)
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
        }.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

        var name = string.IsNullOrWhiteSpace(cfg.SpellName) ? "Spell" : cfg.SpellName;
        var details = new Dictionary<string, object?>();
        if (cfg.BasicPerDay > 0) details["basicPerDay"] = cfg.BasicPerDay;
        if (cfg.PublishedPerDay > 0) details["advancedPerDay"] = cfg.PublishedPerDay;
        if (cfg.AdditionalGenericMana > 0) details["additionalMana"] = cfg.AdditionalGenericMana;
        if (cfg.AdditionalManaOfColour > 0) details["additionalManaColour"] = new { colour = cfg.AdditionalManaColour, amount = cfg.AdditionalManaOfColour };
        if (cfg.TurnHandbookToSixthMantic > 0) details["handbookToSixthManticPerDay"] = cfg.TurnHandbookToSixthMantic;
        if (cfg.TurnAnyBasicMantic > 0) details["anyBasicManticPerDay"] = cfg.TurnAnyBasicMantic;
        if (cfg.TurnAnyPublishedMantic > 0) details["anyPublishedManticPerDay"] = cfg.TurnAnyPublishedMantic;
        if (cfg.AddBasicToBaseList) details["addBasicToList"] = true;
        if (cfg.AddAdvancedToBaseList) details["addAdvancedToList"] = true;
        if (cfg.InnateIsMantic) details["innateIsMantic"] = true;
        if (cfg.PowerStoreRegenerates) details["powerStoreRegenerates"] = true;
        if (cfg.IsTeachingScroll) details["isTeachingScroll"] = true;
        if (tags.Count > 0) details["notes"] = string.Join(", ", tags);

        return new CalcResult
        {
            AbilityType = "Spell",
            AbilityName = name,
            TotalIsp = cfg.Total,
            Details = details
        };
    }

    private async void OnSearchSpell(object sender, EventArgs e)
    {
        var picked = await new Spell().PickAsync(Navigation);
        if (picked == null) return;

        if (BindingContext is SpellConfig cfg)
            cfg.ApplySpell(picked);
    }
}
