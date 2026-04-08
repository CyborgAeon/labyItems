using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SQLite;

namespace labyItems.Services;

public static class LifeScalesService
{
    private static readonly SemaphoreSlim CacheLock = new(1, 1);
    private static Dictionary<string, Dictionary<string, List<int[]>>>? _cache;

    public static async Task<Dictionary<string, Dictionary<string, List<int[]>>>> GetAllAsync()
    {
        if (_cache != null)
            return _cache;

        await CacheLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_cache != null)
                return _cache;

            // #if DEBUG
            //             _cache = await LoadDebugMergedAsync().ConfigureAwait(false);
            // #else
            _cache = await Task.Run(LoadFromDb).ConfigureAwait(false);
            // #endif
        }
        catch (Exception ex)
        {
            ServiceHelper.LogDbError("Get lifescales", ex);
            throw;
        }
        finally
        {
            CacheLock.Release();
        }

        return _cache;
    }

    public static void InvalidateCache()
        => _cache = null;

    private static Dictionary<string, Dictionary<string, List<int[]>>> LoadFromDb()
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

        return dict;
    }

    private static async Task<Dictionary<string, Dictionary<string, List<int[]>>>> LoadDebugMergedAsync()
    {
        var dict = new Dictionary<string, Dictionary<string, List<int[]>>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var json = await ServiceHelper.ReadPackageTextAsync("people/lifescales.json").ConfigureAwait(false);
            var packaged = await Task.Run(() =>
                    JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, List<int[]>>>>(json)
                    ?? new Dictionary<string, Dictionary<string, List<int[]>>>(StringComparer.OrdinalIgnoreCase))
                .ConfigureAwait(false);

            MergeLifeScaleMaps(dict, packaged);
        }
        catch (Exception ex)
        {
            ServiceHelper.LogDbError("Get lifescales (debug json)", ex);
        }

        try
        {
            var db = await Task.Run(LoadFromDb).ConfigureAwait(false);
            MergeLifeScaleMaps(dict, db);
        }
        catch (Exception ex)
        {
            ServiceHelper.LogDbError("Get lifescales (debug db merge)", ex);
        }

        return dict;
    }

    private static void MergeLifeScaleMaps(
        IDictionary<string, Dictionary<string, List<int[]>>> target,
        IReadOnlyDictionary<string, Dictionary<string, List<int[]>>> source)
    {
        foreach (var (race, classMap) in source)
        {
            if (!target.TryGetValue(race, out var targetClassMap))
            {
                targetClassMap = new Dictionary<string, List<int[]>>(StringComparer.OrdinalIgnoreCase);
                target[race] = targetClassMap;
            }

            foreach (var (className, points) in classMap)
                targetClassMap[className] = points ?? new List<int[]>();
        }
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

        var normalizedBaseRace = NormalizeKey(ExtractBaseRaceName(raceKey));
        var classes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (candidateRace, classMap) in all)
        {
            if (!RaceMatchesBase(candidateRace, normalizedBaseRace))
                continue;

            foreach (var className in classMap.Keys)
                classes.Add(className);
        }

        if (classes.Count == 0)
            return all[raceKey].Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();

        return classes.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static async Task<IReadOnlyList<string>> GetRacesForClassAsync(string className)
    {
        var all = await GetAllAsync();

        var wanted = NormalizeKey(className);

        var races = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var race in all)
        {
            if (race.Value.Keys.Any(k => NormalizeKey(k) == wanted))
                races.Add(ExtractBaseRaceName(race.Key));
        }

        return races.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static async Task<IReadOnlyList<LifeScalePoint>> GetLifeScaleAsync(string raceName, string className)
    {
        var all = await GetAllAsync();

        var requestedRace = (raceName ?? string.Empty).Trim();
        var applyForgottenHumanLifePenalty =
            NormalizeKey(requestedRace).Equals(NormalizeKey("Human (Forgotten)"), StringComparison.Ordinal);

        var requestedClass = (className ?? string.Empty).Trim();
        if (requestedClass.Length == 0)
            return Array.Empty<LifeScalePoint>();

        string? resolvedRaceKey = null;
        string? resolvedClassKey = null;

        var preferredRaceNames = new List<string>();
        if (!string.IsNullOrWhiteSpace(raceName))
            preferredRaceNames.Add(raceName);
        preferredRaceNames.Add("Human");

        var seenRaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var preferredRace in preferredRaceNames)
        {
            var raceKey = FindBestKey(all.Keys, preferredRace);
            if (raceKey == null || !seenRaces.Add(raceKey))
                continue;

            var classKey = FindBestKey(all[raceKey].Keys, requestedClass);
            if (classKey == null)
                continue;

            resolvedRaceKey = raceKey;
            resolvedClassKey = classKey;
            break;
        }

        if (resolvedRaceKey == null || resolvedClassKey == null)
        {
            var fallback = FindFirstRaceClassMatch(all, requestedClass);
            resolvedRaceKey = fallback.RaceKey;
            resolvedClassKey = fallback.ClassKey;
        }

        if (resolvedRaceKey == null || resolvedClassKey == null)
            return Array.Empty<LifeScalePoint>();

        var rows = all[resolvedRaceKey][resolvedClassKey];

        var result = new List<LifeScalePoint>(rows.Count);
        foreach (var pair in rows)
        {
            if (pair.Length >= 2)
                result.Add(new LifeScalePoint(pair[0], pair[1]));
        }

        if (applyForgottenHumanLifePenalty && result.Count > 0)
            return ApplyOneLevelLifePenalty(result);

        return result;
    }

    private static IReadOnlyList<LifeScalePoint> ApplyOneLevelLifePenalty(IReadOnlyList<LifeScalePoint> points)
    {
        if (points == null || points.Count == 0)
            return Array.Empty<LifeScalePoint>();

        var adjusted = points.ToList();
        for (var i = adjusted.Count - 1; i >= 1; i--)
            adjusted[i] = adjusted[i - 1];

        return adjusted;
    }

    private static (string? RaceKey, string? ClassKey) FindFirstRaceClassMatch(
        IReadOnlyDictionary<string, Dictionary<string, List<int[]>>> all,
        string className)
    {
        foreach (var raceKey in all.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            var classKey = FindBestKey(all[raceKey].Keys, className);
            if (classKey != null)
                return (raceKey, classKey);
        }

        return (null, null);
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

    private static bool RaceMatchesBase(string candidateRace, string normalizedBaseRace)
    {
        if (normalizedBaseRace.Length == 0)
            return false;

        var candidateBase = NormalizeKey(ExtractBaseRaceName(candidateRace));
        return candidateBase.Length > 0
               && string.Equals(candidateBase, normalizedBaseRace, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractBaseRaceName(string? raceName)
    {
        var value = (raceName ?? string.Empty).Trim();
        if (value.Length == 0)
            return string.Empty;

        var parenIndex = value.IndexOf('(');
        if (parenIndex <= 0)
            return value;

        return value[..parenIndex].Trim();
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
