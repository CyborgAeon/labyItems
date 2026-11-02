using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using labyItems.Models.Enums;

namespace labyItems.Services;

public static class MiracleHealingService
{
    public static void InvalidateCache() => _cache = null;

    private static List<HealingEntry>? _cache;

    public static async Task<List<HealingEntry>> GetHealingMiraclesAsync()
    {
        var accountCacheVersion = LabyrintheSession.Shared.Version;
        if (_cache != null) return _cache;

        IReadOnlyList<MiracleService.MiracRaw> all;
        try
        {
            all = await MiracleService.GetAllAsync();
        }
        catch
        {
            LabyrintheSession.Shared.Publish(accountCacheVersion, () => _cache = new List<HealingEntry>());
            return _cache;
        }

        LabyrintheSession.Shared.Publish(accountCacheVersion, () => _cache = all
            .Where(m => m.GetHealAmounts().Count > 0)
            .Select(m => new HealingEntry(
                Name: m.name,
                Parts: BuildParts(m)))
            .ToList());

        return _cache;
    }

    private static IReadOnlyList<HealingPart> BuildParts(MiracleService.MiracRaw m)
    {
        var healing = m.GetHealAmounts();
        if (healing.Count == 0) return Array.Empty<HealingPart>();

        var types = m.GetHealTypes();

        return Enumerable.Range(0, healing.Count)
            .Select(i =>
            {
                var heal = healing[i];
                var tblp = At(heal, 0);
                var loc = At(heal, 1);
                var type = DamTypeParser.NormalizeOrFallback(OrLast(types, i), "Missile");
                return new HealingPart(tblp, loc, type);
            })
            .ToList();
    }

    private static int At(int[]? arr, int index)
        => (arr != null && index >= 0 && index < arr.Length) ? arr[index] : 0;

    private static T? OrLast<T>(IReadOnlyList<T> list, int index)
        => list.Count == 0 ? default : (index < list.Count ? list[index] : list[^1]);
}

public sealed record HealingEntry(string Name, IReadOnlyList<HealingPart> Parts)
{
    public string Summary => string.Join("; ", Parts.Select(FormatPart));

    private static string FormatPart(HealingPart part)
    {
        var type = part.HealType?.Trim();
        var prefix = string.IsNullOrWhiteSpace(type) ? string.Empty : $"{type}:";
        return $"{prefix}{part.Tblp}/{part.Loc}";
    }
}

public sealed record HealingPart(int Tblp, int Loc, string HealType);
