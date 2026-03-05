using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SQLite;

namespace labyItems.Services;

public static class EvocationCatalogService
{
    public static async Task<IReadOnlyList<DruidEvocationService.EvocRaw>> GetAllAsync()
    {
        var packaged = await TryLoadPackagedAsync();
        if (packaged.Count > 0)
            return packaged;

        return await TryLoadLegacyAsync();
    }

    private static async Task<IReadOnlyList<DruidEvocationService.EvocRaw>> TryLoadPackagedAsync()
    {
        try
        {
            var list = await DruidEvocationService.GetAllAsync();
            System.Diagnostics.Debug.WriteLine($"[EVOCS] Packaged load count: {list.Count}");
            return list;
        }
        catch (FileNotFoundException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EVOCS] Packaged load failed: {ex}");
            return Array.Empty<DruidEvocationService.EvocRaw>();
        }
        catch (DirectoryNotFoundException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EVOCS] Packaged load failed: {ex}");
            return Array.Empty<DruidEvocationService.EvocRaw>();
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EVOCS] Packaged load failed: {ex}");
            return Array.Empty<DruidEvocationService.EvocRaw>();
        }
        catch (InvalidOperationException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EVOCS] Packaged load failed: {ex}");
            return Array.Empty<DruidEvocationService.EvocRaw>();
        }
    }

    private static async Task<IReadOnlyList<DruidEvocationService.EvocRaw>> TryLoadLegacyAsync()
    {
        try
        {
            var legacy = await EarthPowerService.GetAllAsync();
            var mapped = legacy
                .Where(e => !string.IsNullOrWhiteSpace(e.name))
                .Select(MapLegacyEvocation)
                .ToList();

            System.Diagnostics.Debug.WriteLine($"[EVOCS] Legacy fallback count: {mapped.Count}");
            return mapped;
        }
        catch (SQLiteException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EVOCS] Legacy fallback failed: {ex}");
            return Array.Empty<DruidEvocationService.EvocRaw>();
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EVOCS] Legacy fallback failed: {ex}");
            return Array.Empty<DruidEvocationService.EvocRaw>();
        }
        catch (IOException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EVOCS] Legacy fallback failed: {ex}");
            return Array.Empty<DruidEvocationService.EvocRaw>();
        }
        catch (InvalidOperationException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EVOCS] Legacy fallback failed: {ex}");
            return Array.Empty<DruidEvocationService.EvocRaw>();
        }
    }

    private static DruidEvocationService.EvocRaw MapLegacyEvocation(EarthPowerService.EvocRaw source)
    {
        return new DruidEvocationService.EvocRaw
        {
            name = source.name ?? string.Empty,
            power = source.power,
            range = source.range ?? string.Empty,
            duration = source.duration ?? string.Empty,
            verbal = source.verbal ?? string.Empty,
            fields = (source.fields ?? new List<string>())
                .Select(f => (f ?? string.Empty).Trim())
                .Where(f => f.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            description = source.description ?? string.Empty,
            isAdvanced = source.isAdvanced
        };
    }
}
