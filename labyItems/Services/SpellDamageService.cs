using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace labyItems.Services;

public static class SpellDamageService
{
    private static List<DamageSpell>? _cache;

    public static async Task<List<DamageSpell>> GetDamagingSpellsAsync()
    {
        if (_cache != null) return _cache;

        var all = await SpellService.GetAllAsync();

        _cache = all
            .Where(s => s.GetDamageAmounts().Count > 0 || !string.IsNullOrWhiteSpace(s.damageOverride))
            .Select(s => new DamageSpell(
                Name: s.name,
                Level: s.level,
                Kind: "Spell",
                Parts: BuildParts(s)))
            .ToList();

        return _cache;
    }

    private static IReadOnlyList<DamagePart> BuildParts(SpellService.SpellRaw s)
    {
        var damage = s.GetDamageAmounts();
        if (damage.Count == 0 && string.IsNullOrWhiteSpace(s.damageOverride))
            return Array.Empty<DamagePart>();

        var types = s.GetDamageTypes();
        var armours = s.GetArmourApplies();
        var armourType = s.GetArmourType();
        var useMacArmour = armourType.Equals("MAC", StringComparison.OrdinalIgnoreCase);
        var pacs = s.GetPacDamage();

        var partCount = damage.Count > 0 ? damage.Count : 1;
        return Enumerable.Range(0, partCount)
            .Select(i =>
            {
                var dmg = damage.Count > 0 ? damage[i] : Array.Empty<int>();

                var tblp = At(dmg, 0);
                var loc = At(dmg, 1);

                var type = OrLast(types, i) ?? "Missile";

                var macPair = useMacArmour
                    ? OrDefault(armours, i, new[] { 0, 0 })
                    : new[] { 0, 0 };
                var macTblp = At(macPair, 0);
                var macLoc = At(macPair, 1);

                var pac = OrLast(pacs, i);

                return new DamagePart(tblp, loc, type, macTblp, macLoc, pac, s.damageOverride, UseSac: false);
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

public sealed record DamageSpell(string Name, int Level, string Kind, IReadOnlyList<DamagePart> Parts)
{
    public string Summary => string.Join("; ", Parts.Select(FormatPart));

    private static string FormatPart(DamagePart part)
    {
        var type = part.DamType?.Trim();
        if (!string.IsNullOrWhiteSpace(part.DamageOverride))
        {
            var label = FormatOverride(part.DamageOverride);
            return string.IsNullOrWhiteSpace(type) ? label : $"{label} ({type})";
        }

        var prefix = string.IsNullOrWhiteSpace(type) ? string.Empty : $"{type}:";
        return $"{prefix}{part.Tblp}/{part.Loc}";
    }

    private static string FormatOverride(string raw)
    {
        var token = raw.Trim().ToLowerInvariant();
        return token switch
        {
            "sever" => "Sever",
            "loc0" => "Loc 0",
            "soullance" => "Soul lance",
            _ => raw
        };
    }
}

public sealed record DamagePart(
    int Tblp,
    int Loc,
    string DamType,
    int MacTblp,
    int MacLoc,
    int PacDam,
    string? DamageOverride,
    bool UseSac
);
