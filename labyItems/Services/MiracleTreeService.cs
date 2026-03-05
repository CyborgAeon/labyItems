using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace labyItems.Services;

public static class MiracleTreeService
{
    private static Dictionary<string, IReadOnlyList<string>>? _cache;

    public static async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetPreReqsAsync()
    {
        if (_cache != null)
            return _cache;

        try
        {
            string json = string.Empty;
            var candidates = new[]
            {
                "words_from_above/miracles.json",
                "words_from_above/base-miracles1.json"
            };

            foreach (var path in candidates)
            {
                try
                {
                    json = await ServiceHelper.ReadPackageTextAsync(path);
                    if (!string.IsNullOrWhiteSpace(json))
                        break;
                }
                catch
                {
                    // Try next candidate.
                }
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                _cache = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
                return _cache;
            }

            var entries = JsonSerializer.Deserialize<List<MiracleTreeRaw>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<MiracleTreeRaw>();

            var dict = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries)
            {
                var name = (entry.name ?? string.Empty).Trim();
                if (name.Length == 0)
                    continue;

                if (entry.preReqs is not { Count: > 0 })
                    continue;

                var prereqs = entry.preReqs
                    .Select(r => (r ?? string.Empty).Trim())
                    .Where(r => r.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (prereqs.Count > 0)
                    dict[name] = prereqs;
            }

            _cache = dict;
        }
        catch
        {
            _cache = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        }

        return _cache;
    }

    private sealed class MiracleTreeRaw
    {
        public string? name { get; set; }

        [JsonPropertyName("preReqs")]
        public List<string>? preReqs { get; set; }
    }
}
