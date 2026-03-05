using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using labyItems.Models.Enums;

namespace labyItems.Services;

public static class MiracleDamageService
{
    private static List<DamageSpell>? _cache;

    public static async Task<List<DamageSpell>> GetDamagingMiraclesAsync()
    {
        if (_cache != null) return _cache;

        IReadOnlyList<MiracleService.MiracRaw> all;
        try
        {
            all = await MiracleService.GetAllAsync();
        }
        catch
        {
            _cache = new List<DamageSpell>();
            return _cache;
        }

        _cache = all
            .Where(m => m.GetDamageAmounts().Count > 0 || !string.IsNullOrWhiteSpace(m.damageOverride))
            .Select(m => new DamageSpell(
                Name: m.name,
                Level: m.power,
                Kind: "Miracle",
                Parts: BuildParts(m)))
            .ToList();

        return _cache;
    }

    private static IReadOnlyList<DamagePart> BuildParts(MiracleService.MiracRaw m)
    {
        var damage = m.GetDamageAmounts();
        if (damage.Count == 0 && string.IsNullOrWhiteSpace(m.damageOverride))
            return Array.Empty<DamagePart>();

        var types = m.GetDamageTypes();
        var armours = m.GetArmourApplies();
        var armourType = m.GetArmourTypeEnum();
        var pacs = m.GetPacDamage();
        var useSac = armourType == ArmourType.SAC;

        var partCount = damage.Count > 0 ? damage.Count : 1;
        return Enumerable.Range(0, partCount)
            .Select(i =>
            {
                var dmg = damage.Count > 0 ? damage[i] : Array.Empty<int>();

                var tblp = At(dmg, 0);
                var loc = At(dmg, 1);

                var type = DamTypeParser.NormalizeOrFallback(OrLast(types, i), "Missile");

                var armourPair = armourType.HasValue
                    ? OrDefault(armours, i, new[] { 0, 0 })
                    : new[] { 0, 0 };
                var armourTblp = At(armourPair, 0);
                var armourLoc = At(armourPair, 1);
                var pac = OrLast(pacs, i);

                return new DamagePart(tblp, loc, type, armourTblp, armourLoc, pac, m.damageOverride, useSac, armourType);
            })
            .ToList();
    }

    private static int At(int[]? arr, int index)
        => (arr != null && index >= 0 && index < arr.Length) ? arr[index] : 0;

    private static T? OrLast<T>(IReadOnlyList<T> list, int index)
        => list.Count == 0 ? default : (index < list.Count ? list[index] : list[^1]);

    private static T OrDefault<T>(IReadOnlyList<T> list, int index, T fallback)
        => list.Count == 0 ? fallback : (index < list.Count ? list[index] : list[^1]);
}
