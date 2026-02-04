using System.Text.Json;
using System.Text.Json.Serialization;
using SQLite;

namespace labyItems.Services;


public sealed class MiracleLookupService : ILookupService
{
    public async Task<IReadOnlyList<LookupItem>> GetAllAsync()
    {
        var all = await MiracleService.GetAllAsync();
        return all
            .Select(m => new LookupItem(Key: m.name, Display: $"{m.name} (P{m.power})"))
            .ToList();
    }

    public async Task<IReadOnlyList<LookupItem>> SearchAsync(string query)
    {
        var hits = await MiracleService.SearchAsync(query);
        return hits
            .Select(m => new LookupItem(Key: m.name, Display: $"{m.name} (P{m.power})"))
            .ToList();
    }
}

public static class MiracleService
{
    public sealed record MiracRaw
    {
        public int power { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string sphere { get; set; }
        public bool isAdvanced { get; set; }
        public string alignment { get; set; }
        [JsonConverter(typeof(IntArrayListConverter))]
        public List<int[]>? damage { get; set; }
        public List<string>? damType { get; set; }
        [JsonConverter(typeof(IntArrayListConverter))]
        public List<int[]>? sacApplies { get; set; }
        public string? damageOverride { get; set; }
        [JsonConverter(typeof(IntArrayListConverter))]
        public List<int[]>? healing { get; set; }
        public List<string>? healType { get; set; }

    }
    private static List<MiracRaw>? _cache;
    private static readonly string? _dbPath = ResolveDbPath();

    public static async Task<IReadOnlyList<MiracRaw>> GetAllAsync()
    {
        if (_cache != null) return _cache;

        var dbList = LoadFromDatabase();
        if (dbList is { Count: > 0 })
        {
            _cache = dbList;
            return _cache;
        }

        throw new InvalidOperationException("Miracles DB not found or empty; ensure laby.db is installed.");
    }

    private static List<MiracRaw>? LoadFromDatabase()
    {
        if (string.IsNullOrEmpty(_dbPath) || !File.Exists(_dbPath))
            return null;

        try
        {
            using var conn = new SQLiteConnection(_dbPath, SQLiteOpenFlags.ReadOnly);
            var rows = conn.Query<DbRow>("SELECT data_json FROM miracles ORDER BY power, name;");
            var list = new List<MiracRaw>();
            foreach (var row in rows)
            {
                var e = JsonSerializer.Deserialize<MiracRaw>(row.data_json);
                if (e != null) list.Add(e);
            }

            return Normalize(list);
        }
        catch
        {
            return null;
        }
    }

    private static List<MiracRaw> Normalize(IEnumerable<MiracRaw> source) =>
        source
            .Select(e => new MiracRaw
            {
                power = e.power,
                name = e.name ?? string.Empty,
                description = e.description ?? string.Empty,
                sphere = e.sphere ?? string.Empty,
                isAdvanced = e.isAdvanced,
                alignment = e.alignment ?? string.Empty,
                damage = e.damage,
                damType = e.damType,
                sacApplies = e.sacApplies,
                damageOverride = e.damageOverride,
                healing = e.healing,
                healType = e.healType,
            })
            .OrderBy(e => e.power)
            .ThenBy(e => e.name)
            .ToList();

    public static async Task<IReadOnlyList<MiracRaw>> SearchAsync(string query)
    {
        var all = await GetAllAsync();
        if (string.IsNullOrWhiteSpace(query)) return all;
        query = query.Trim().ToLowerInvariant();

        return all.Where(e =>
                e.name.ToLowerInvariant().Contains(query))
            .ToList();
    }

    private sealed class DbRow
    {
        public string data_json { get; set; } = string.Empty;
    }

    private static string? ResolveDbPath()
    {
        var appDb = Path.Combine(FileSystem.AppDataDirectory, "laby.db");
        if (File.Exists(appDb))
        {
            return appDb;
        }

        var devCandidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "output", "laby.db"),
        };

        foreach (var candidate in devCandidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private sealed class IntArrayListConverter : JsonConverter<List<int[]>?>
    {
        public override List<int[]>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException("Expected array for int list.");

            var list = new List<int[]>();

            reader.Read();
            if (reader.TokenType == JsonTokenType.EndArray)
                return list;

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                while (reader.TokenType == JsonTokenType.StartArray)
                {
                    var values = ReadIntArray(ref reader);
                    list.Add(values);
                    reader.Read();
                }

                if (reader.TokenType != JsonTokenType.EndArray)
                    throw new JsonException("Expected end of array.");

                return list;
            }

            if (reader.TokenType == JsonTokenType.Number)
            {
                var values = new List<int>();
                while (reader.TokenType == JsonTokenType.Number)
                {
                    values.Add(reader.GetInt32());
                    reader.Read();
                }

                if (reader.TokenType != JsonTokenType.EndArray)
                    throw new JsonException("Expected end of array for int list.");

                list.Add(values.ToArray());
                return list;
            }

            throw new JsonException("Unexpected token for int list.");
        }

        public override void Write(Utf8JsonWriter writer, List<int[]>? value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStartArray();
            foreach (var arr in value)
            {
                writer.WriteStartArray();
                if (arr != null)
                {
                    foreach (var n in arr)
                        writer.WriteNumberValue(n);
                }
                writer.WriteEndArray();
            }
            writer.WriteEndArray();
        }

        private static int[] ReadIntArray(ref Utf8JsonReader reader)
        {
            var values = new List<int>();

            reader.Read();
            while (reader.TokenType == JsonTokenType.Number)
            {
                values.Add(reader.GetInt32());
                reader.Read();
            }

            if (reader.TokenType != JsonTokenType.EndArray)
                throw new JsonException("Expected end of array for int tuple.");

            return values.ToArray();
        }
    }
}
