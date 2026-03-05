using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using labyItems.Models.Enums;

namespace labyItems.Services;

public static class EvocationDamageService
{
    private static List<DamageSpell>? _cache;

    public static async Task<List<DamageSpell>> GetDamagingEvocationsAsync()
    {
        if (_cache != null)
            return _cache;

        var all = await EvocationCatalogService.GetAllAsync();

        _cache = all
            .Where(e => !string.IsNullOrWhiteSpace(e.name))
            .Select(e => new DamageSpell(
                Name: e.name,
                Level: e.power,
                Kind: "Evocation",
                Parts: BuildParts(e)))
            .ToList();

        return _cache;
    }

    private static IReadOnlyList<DamagePart> BuildParts(DruidEvocationService.EvocRaw evocation)
    {
        var damage = evocation.GetDamageAmounts();
        if (damage.Count == 0)
            return Array.Empty<DamagePart>();

        var types = evocation.GetDamageTypes();
        var armours = evocation.GetArmourApplies();
        var armourType = evocation.GetArmourTypeEnum();
        var useSac = armourType == ArmourType.SAC;
        var pacs = evocation.GetPacDamage();

        return Enumerable.Range(0, damage.Count)
            .Select(i =>
            {
                var dmg = damage[i];
                var tblp = At(dmg, 0);
                var loc = At(dmg, 1);
                var type = DamTypeParser.NormalizeOrFallback(OrLast(types, i), "Missile");

                var armourPair = armourType.HasValue
                    ? OrDefault(armours, i, new[] { 0, 0 })
                    : new[] { 0, 0 };
                var armourTblp = At(armourPair, 0);
                var armourLoc = At(armourPair, 1);

                var pac = OrLast(pacs, i);

                return new DamagePart(tblp, loc, type, armourTblp, armourLoc, pac, null, useSac, armourType);
            })
            .ToList();
    }

    private static int At(int[]? arr, int index)
        => arr != null && index >= 0 && index < arr.Length ? arr[index] : 0;

    private static T? OrLast<T>(IReadOnlyList<T> list, int index)
        => list.Count == 0 ? default : index < list.Count ? list[index] : list[^1];

    private static T OrDefault<T>(IReadOnlyList<T> list, int index, T fallback)
        => list.Count == 0 ? fallback : index < list.Count ? list[index] : list[^1];
}
