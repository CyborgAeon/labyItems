using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace labyItems.Services;

public static class LifeScalesService
{
    private static Dictionary<string, Dictionary<string, List<int[]>>>? _cache;

    public static async Task<Dictionary<string, Dictionary<string, List<int[]>>>> GetAllAsync()
    {
        if (_cache != null) return _cache;

        using var s = await FileSystem.OpenAppPackageFileAsync("people/lifescales.json");
        using var r = new StreamReader(s);
        var json = await r.ReadToEndAsync();

        _cache = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, List<int[]>>>>(json)
                 ?? new Dictionary<string, Dictionary<string, List<int[]>>>();

        return _cache;
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
