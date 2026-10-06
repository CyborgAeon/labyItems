using labyItems.Services;

namespace labyItems.Pages.NonStandard;

public static class NeuronicCombatEditorPage
{
    public static async Task<NeuronicService.NeuronicRaw?> EditAsync(
        INavigation navigation, NeuronicService.NeuronicRaw source, bool isDamage)
    {
        var adapter = new DruidEvocationService.EvocRaw
        {
            Damage = source.Damage == null ? null : new DruidEvocationService.EvocationDamageRaw
            {
                amount = source.Damage.amount?.Select(pair => pair.ToArray()).ToList(),
                type = source.Damage.type?.ToList(),
                DamageType = source.Damage.DamageType?.ToList(),
                ArmourApplies = source.Damage.ArmourApplies?.Select(pair => pair.ToArray()).ToList(),
                ArmourType = source.Damage.ArmourType,
                PACDam = source.Damage.PACDam?.ToList()
            }, Heal = source.Heal == null ? null : new DruidEvocationService.EvocationHealRaw { amount = source.Heal.amount?.Select(pair => pair.ToArray()).ToList(), type = source.Heal.type?.ToList() }
        };

        var edited = await EvocationCombatEditorPage.EditAsync(navigation, adapter, isDamage);
        if (edited == null) return null;
        if (!isDamage) { source.Heal = edited.Heal == null ? null : new NeuronicService.NeuronicHealRaw { amount = edited.Heal.amount, type = edited.Heal.type }; return source; }
        source.Damage = edited.Damage == null ? null : new NeuronicService.NeuronicDamageRaw
        {
            amount = edited.Damage.amount?.Select(pair => pair.ToArray()).ToList(),
            type = edited.Damage.type?.ToList(),
            DamageType = edited.Damage.DamageType?.ToList(),
            ArmourApplies = edited.Damage.ArmourApplies?.Select(pair => pair.ToArray()).ToList(),
            ArmourType = edited.Damage.ArmourType,
            PACDam = edited.Damage.PACDam?.ToList()
        };
        return source;
    }
}
