using labyItems.Services;

namespace labyItems.Pages.NonStandard;

/// <summary>Adapts miracle combat data to the shared combat editor interaction.</summary>
public static class MiracleCombatEditorPage
{
    public static async Task<MiracleService.MiracRaw?> EditAsync(
        INavigation navigation, MiracleService.MiracRaw source, bool isDamage)
    {
        var adapter = new DruidEvocationService.EvocRaw
        {
            Damage = source.Damage == null ? null : new DruidEvocationService.EvocationDamageRaw
            {
                amount = source.Damage.amount?.Select(ClonePair).ToList(),
                type = source.Damage.type?.ToList(),
                DamageType = source.Damage.DamageType?.ToList(),
                ArmourApplies = source.Damage.ArmourApplies?.Select(ClonePair).ToList(),
                ArmourType = source.Damage.ArmourType
            },
            Heal = source.Heal == null ? null : new DruidEvocationService.EvocationHealRaw
            {
                amount = source.Heal.amount?.Select(ClonePair).ToList(),
                type = source.Heal.type?.ToList()
            },
            damage = source.damage?.Select(ClonePair).ToList(),
            damType = source.damType?.ToList(),
            InnatePacApplies = source.sacApplies?.Select(ClonePair).ToList(),
            healing = source.healing?.Select(ClonePair).ToList(),
            healType = source.healType?.ToList()
        };

        var edited = await EvocationCombatEditorPage.EditAsync(navigation, adapter, isDamage);
        if (edited == null) return null;

        if (isDamage)
        {
            source.Damage = edited.Damage == null ? null : new MiracleService.MiracleDamageRaw
            {
                amount = edited.Damage.amount?.Select(ClonePair).ToList(),
                type = edited.Damage.type?.ToList(),
                DamageType = edited.Damage.DamageType?.ToList(),
                ArmourApplies = edited.Damage.ArmourApplies?.Select(ClonePair).ToList(),
                ArmourType = edited.Damage.ArmourType
            };
            source.damage = null; source.damType = null; source.sacApplies = null;
        }
        else
        {
            source.Heal = edited.Heal == null ? null : new MiracleService.MiracleHealRaw
            {
                amount = edited.Heal.amount?.Select(ClonePair).ToList(),
                type = edited.Heal.type?.ToList()
            };
            source.healing = null; source.healType = null;
        }
        return source;
    }

    private static int[] ClonePair(int[] pair) => pair.ToArray();
}
