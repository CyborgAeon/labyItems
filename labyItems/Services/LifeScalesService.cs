using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SQLite;

namespace labyItems.Services;

public static class LifeScalesService
{
    private static Dictionary<string, Dictionary<string, List<int[]>>>? _cache;

    public static Task<Dictionary<string, Dictionary<string, List<int[]>>>> GetAllAsync()
    {
        if (_cache != null) return Task.FromResult(_cache);
        try
        {
            using var conn = ServiceHelper.OpenReadOnlyConnection();
            var rows = conn.Query<LifeScaleRow>("SELECT race, class, idx, body, loc FROM lifescales ORDER BY race, class, idx;");
            var dict = new Dictionary<string, Dictionary<string, List<int[]>>>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                if (!dict.TryGetValue(row.race, out var classMap))
                {
                    classMap = new Dictionary<string, List<int[]>>(StringComparer.OrdinalIgnoreCase);
                    dict[row.race] = classMap;
                }

                if (!classMap.TryGetValue(row.@class, out var list))
                {
                    list = new List<int[]>();
                    classMap[row.@class] = list;
                }

                list.Add(new[] { row.body, row.loc });
            }

            _cache = dict;
        }
        catch (Exception ex)
        {
            ServiceHelper.LogDbError("Get lifescales", ex);
            throw;
        }

        return Task.FromResult(_cache);
    }

    public static async Task<IReadOnlyList<string>> GetRaceNamesAsync()
    {
        var all = await GetAllAsync();
        return all.Keys.OrderBy(x => x).ToList();
    }

    public static async Task<IReadOnlyList<string>> GetClassNamesAsync()
    {
        var all = await GetAllAsync();
        var classes = all.Values.SelectMany(d => d.Keys).Distinct().OrderBy(x => x).ToList();
        return classes;
    }

    public static async Task<IReadOnlyList<string>> GetClassesForRaceAsync(string raceName)
    {
        var all = await GetAllAsync();

        var raceKey = FindBestKey(all.Keys, raceName);
        if (raceKey == null) return Array.Empty<string>();

        return all[raceKey].Keys.OrderBy(x => x).ToList();
    }

    public static async Task<IReadOnlyList<string>> GetRacesForClassAsync(string className)
    {
        var all = await GetAllAsync();

        var wanted = NormalizeKey(className);

        var races = new List<string>();
        foreach (var race in all)
        {
            if (race.Value.Keys.Any(k => NormalizeKey(k) == wanted))
                races.Add(race.Key);
        }

        races.Sort(StringComparer.OrdinalIgnoreCase);
        return races;
    }

    public static async Task<IReadOnlyList<LifeScalePoint>> GetLifeScaleAsync(string raceName, string className)
    {
        var all = await GetAllAsync();

        var resolvedRaceName = string.IsNullOrWhiteSpace(raceName) ? "Human" : raceName;

        var raceKey = FindBestKey(all.Keys, resolvedRaceName) ?? FindBestKey(all.Keys, "Human");
        if (raceKey == null) return Array.Empty<LifeScalePoint>();

        var classKey = FindBestKey(all[raceKey].Keys, className);
        if (classKey == null)
        {
            var humanKey = FindBestKey(all.Keys, "Human");
            if (humanKey != null)
            {
                var humanClassKey = FindBestKey(all[humanKey].Keys, className);
                if (humanClassKey != null)
                {
                    raceKey = humanKey;
                    classKey = humanClassKey;
                }
            }
        }

        if (classKey == null) return Array.Empty<LifeScalePoint>();

        var rows = all[raceKey][classKey];

        var result = new List<LifeScalePoint>(rows.Count);
        foreach (var pair in rows)
        {
            if (pair.Length >= 2)
                result.Add(new LifeScalePoint(pair[0], pair[1]));
        }

        return result;
    }

    public static string NormalizeKey(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";

        var chars = s
            .Trim()
            .ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c));

        return new string(chars.ToArray());
    }

    private static string? FindBestKey(IEnumerable<string> candidates, string input)
    {
        var wanted = NormalizeKey(input);
        if (wanted.Length == 0) return null;

        foreach (var c in candidates)
            if (NormalizeKey(c) == wanted)
                return c;

        return candidates.FirstOrDefault(c =>
            NormalizeKey(c).Contains(wanted) || wanted.Contains(NormalizeKey(c)));
    }
}

public readonly record struct LifeScalePoint(int Body, int Loc);

internal sealed class LifeScaleRow
{
    public string race { get; set; } = string.Empty;
    public string @class { get; set; } = string.Empty;
    public int idx { get; set; }
    public int body { get; set; }
    public int loc { get; set; }
}
