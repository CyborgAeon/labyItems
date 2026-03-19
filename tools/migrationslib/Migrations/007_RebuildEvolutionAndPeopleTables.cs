using System.Data;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentMigrator;

namespace MigrationsLib.Migrations;

[Migration(7)]
public sealed class RebuildEvolutionAndPeopleTables : Migration
{
    private const int NgramSize = 3;
    public override void Up()
    {
        Execute.Sql("DROP TABLE IF EXISTS evolution_ngrams;");
        Execute.Sql("DROP TABLE IF EXISTS evolution;");
        Execute.Sql("DROP TABLE IF EXISTS classes;");
        Execute.Sql("DROP TABLE IF EXISTS races;");
        Execute.Sql("DROP TABLE IF EXISTS lifescales;");

        Create.Table("evolution")
            .WithColumn("id").AsString(36).PrimaryKey().NotNullable()
            .WithColumn("idx").AsString().NotNullable()
            .WithColumn("idx_lower").AsString().Nullable()
            .WithColumn("description").AsString().Nullable()
            .WithColumn("cost").AsInt32().Nullable()
            .WithColumn("available").AsString().Nullable()
            .WithColumn("table_id").AsInt32().Nullable()
            .WithColumn("can_buy_multiple").AsInt32().Nullable()
            .WithColumn("prereqs_json").AsString().Nullable()
            .WithColumn("data_json").AsString().Nullable()
            .WithColumn("is_default").AsInt32().Nullable()
            .WithColumn("created_at").AsString().Nullable()
            .WithColumn("updated_at").AsString().Nullable();
        Execute.Sql("CREATE INDEX IF NOT EXISTS idx_evolution_idx_lower ON evolution(idx_lower);");

        Create.Table("evolution_ngrams")
            .WithColumn("token").AsString().NotNullable()
            .WithColumn("evolution_id").AsString().NotNullable();
        Execute.Sql("CREATE INDEX IF NOT EXISTS idx_evolution_ngrams_token ON evolution_ngrams(token);");
        Execute.Sql("CREATE INDEX IF NOT EXISTS idx_evolution_ngrams_evolution_id ON evolution_ngrams(evolution_id);");

        Create.Table("classes")
            .WithColumn("id").AsString(36).PrimaryKey().NotNullable()
            .WithColumn("name").AsString().NotNullable()
            .WithColumn("name_lower").AsString().Nullable()
            .WithColumn("data_json").AsString().Nullable()
            .WithColumn("created_at").AsString().Nullable()
            .WithColumn("updated_at").AsString().Nullable();
        Execute.Sql("CREATE INDEX IF NOT EXISTS idx_classes_name_lower ON classes(name_lower);");

        Create.Table("races")
            .WithColumn("id").AsString(36).PrimaryKey().NotNullable()
            .WithColumn("name").AsString().NotNullable()
            .WithColumn("name_lower").AsString().Nullable()
            .WithColumn("data_json").AsString().Nullable()
            .WithColumn("created_at").AsString().Nullable()
            .WithColumn("updated_at").AsString().Nullable();
        Execute.Sql("CREATE INDEX IF NOT EXISTS idx_races_name_lower ON races(name_lower);");

        Create.Table("lifescales")
            .WithColumn("race").AsString().NotNullable()
            .WithColumn("class").AsString().NotNullable()
            .WithColumn("idx").AsInt32().NotNullable()
            .WithColumn("body").AsInt32().NotNullable()
            .WithColumn("loc").AsInt32().NotNullable();
        Execute.Sql("CREATE UNIQUE INDEX IF NOT EXISTS idx_lifescales_pk ON lifescales(race, class, idx);");
        Execute.Sql("CREATE INDEX IF NOT EXISTS idx_lifescales_race ON lifescales(race);");
        Execute.Sql("CREATE INDEX IF NOT EXISTS idx_lifescales_class ON lifescales(class);");

        var abilities = LoadAllAbilities();
        var classes = LoadNamedJson("people/classes.json");
        var races = LoadNamedJson("people/people.json");
        var lifescales = LoadLifeScales();

        Execute.WithConnection((conn, tran) =>
        {
            using var insert = conn.CreateCommand();
            insert.Transaction = tran;
            insert.CommandText = @"INSERT OR IGNORE INTO evolution
(id, idx, idx_lower, description, cost, available, table_id, can_buy_multiple, prereqs_json, data_json, is_default, created_at, updated_at)
VALUES (@id, @idx, @idx_lower, @description, @cost, @available, @table_id, @can_buy_multiple, @prereqs_json, @data_json, @is_default, @created_at, @updated_at);";

            using var insertNgram = conn.CreateCommand();
            insertNgram.Transaction = tran;
            insertNgram.CommandText = @"INSERT INTO evolution_ngrams (token, evolution_id) VALUES (@token, @evolution_id);";

            var now = DateTime.UtcNow.ToString("o");
            foreach (var a in abilities)
            {
                var name = (a.index ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var desc = (a.desc ?? string.Empty).Trim();
                var available = SerializeAvailability(a.available);
                var sourceBook = (a.sourceBook ?? string.Empty).Trim();
                var costRaw = (a.cost ?? string.Empty).Trim();
                var canBuyMultiple = costRaw.Contains('*');
                var hasPlus = costRaw.Contains('+');
                var cost = ParseCost(costRaw);
                var table = a.table;

                List<string>? preReqs = null;
                if (hasPlus)
                    preReqs = a.preReqs is { Count: > 0 } ? a.preReqs : new List<string>();
                else if (a.preReqs is { Count: > 0 })
                    preReqs = a.preReqs;

                var id = DeterministicGuid($"evo|{table}|{name}");

                insert.Parameters.Clear();
                AddParam(insert, "@id", id);
                AddParam(insert, "@idx", name);
                AddParam(insert, "@idx_lower", name.ToLowerInvariant());
                AddParam(insert, "@description", desc);
                AddParam(insert, "@cost", cost);
                AddParam(insert, "@available", available);
                AddParam(insert, "@table_id", table);
                AddParam(insert, "@can_buy_multiple", canBuyMultiple ? 1 : 0);
                AddParam(insert, "@prereqs_json", preReqs is null ? null : JsonSerializer.Serialize(preReqs));
                var dataJson = JsonSerializer.Serialize(new
                {
                    available,
                    index = name,
                    desc,
                    cost,
                    table,
                    canBuyMultiple,
                    preReqs,
                    sourceBook
                });
                AddParam(insert, "@data_json", dataJson);
                AddParam(insert, "@is_default", 1);
                AddParam(insert, "@created_at", now);
                AddParam(insert, "@updated_at", now);

                insert.ExecuteNonQuery();

                var combined = (name + " " + desc).ToLowerInvariant();
                var normalized = NormalizeForNgrams(combined);
                var tokens = GenerateNgrams(normalized, NgramSize);
                var inserted = new HashSet<string>(StringComparer.Ordinal);
                foreach (var token in tokens)
                {
                    if (!inserted.Add(token))
                        continue;

                    insertNgram.Parameters.Clear();
                    AddParam(insertNgram, "@token", token);
                    AddParam(insertNgram, "@evolution_id", id);
                    insertNgram.ExecuteNonQuery();
                }
            }

            using var insertClass = conn.CreateCommand();
            insertClass.Transaction = tran;
            insertClass.CommandText = @"INSERT OR IGNORE INTO classes
(id, name, name_lower, data_json, created_at, updated_at)
VALUES (@id, @name, @name_lower, @data_json, @created_at, @updated_at);";

            foreach (var (name, json) in classes)
            {
                var id = DeterministicGuid($"class|{name}");
                insertClass.Parameters.Clear();
                AddParam(insertClass, "@id", id);
                AddParam(insertClass, "@name", name);
                AddParam(insertClass, "@name_lower", name.ToLowerInvariant());
                AddParam(insertClass, "@data_json", json);
                AddParam(insertClass, "@created_at", now);
                AddParam(insertClass, "@updated_at", now);
                insertClass.ExecuteNonQuery();
            }

            using var insertRace = conn.CreateCommand();
            insertRace.Transaction = tran;
            insertRace.CommandText = @"INSERT OR IGNORE INTO races
(id, name, name_lower, data_json, created_at, updated_at)
VALUES (@id, @name, @name_lower, @data_json, @created_at, @updated_at);";

            foreach (var (name, json) in races)
            {
                var id = DeterministicGuid($"race|{name}");
                insertRace.Parameters.Clear();
                AddParam(insertRace, "@id", id);
                AddParam(insertRace, "@name", name);
                AddParam(insertRace, "@name_lower", name.ToLowerInvariant());
                AddParam(insertRace, "@data_json", json);
                AddParam(insertRace, "@created_at", now);
                AddParam(insertRace, "@updated_at", now);
                insertRace.ExecuteNonQuery();
            }

            using var insertLife = conn.CreateCommand();
            insertLife.Transaction = tran;
            insertLife.CommandText = @"INSERT OR IGNORE INTO lifescales (race, class, idx, body, loc) VALUES (@race, @class, @idx, @body, @loc);";

            foreach (var entry in lifescales)
            {
                insertLife.Parameters.Clear();
                AddParam(insertLife, "@race", entry.race);
                AddParam(insertLife, "@class", entry.@class);
                AddParam(insertLife, "@idx", entry.idx);
                AddParam(insertLife, "@body", entry.body);
                AddParam(insertLife, "@loc", entry.loc);
                insertLife.ExecuteNonQuery();
            }
        });
    }

    public override void Down()
    {
        // no-op (destructive rebuild migration)
    }

    private static List<AbilityRaw> LoadAllAbilities()
    {
        var combined = new List<AbilityRaw>();
        combined.AddRange(LoadMakesAbilities());
        combined.AddRange(LoadEvolutionTables());
        return combined;
    }

    private static List<AbilityRaw> LoadMakesAbilities()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .Single(n => n.EndsWith("makes_abilities.json", StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
            return new List<AbilityRaw>();

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            return new List<AbilityRaw>();

        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<List<AbilityRaw>>(json, opts) ?? new List<AbilityRaw>();
    }

    private static List<AbilityRaw> LoadEvolutionTables()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .Single(e => e.EndsWith("evolution_classes.merged.json") || e.EndsWith("evolution_classes/merged.json"));

        if (resourceName == string.Empty)
            throw new NotImplementedException("can't find merged.json");

        var list = new List<AbilityRaw>();

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            return list;

        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var rows = JsonSerializer.Deserialize<List<AbilityRaw>>(json, opts) ?? new List<AbilityRaw>();
        foreach (var row in rows)
        {
            list.Add(row);
        }

        return list;
    }

    private static Dictionary<string, string> LoadNamedJson(string resourceSuffix)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var normalizedSuffix = resourceSuffix.Replace('\\', '.').Replace('/', '.');
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n =>
                n.EndsWith(resourceSuffix, StringComparison.OrdinalIgnoreCase)
                || n.EndsWith(normalizedSuffix, StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();

        using var doc = JsonDocument.Parse(json);
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            dict[prop.Name] = prop.Value.GetRawText();
        }

        return dict;
    }

    private static List<LifeScaleRow> LoadLifeScales()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n =>
                n.EndsWith("people/lifescales.json", StringComparison.OrdinalIgnoreCase)
                || n.EndsWith("people.lifescales.json", StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
            return new List<LifeScaleRow>();

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            return new List<LifeScaleRow>();

        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var data = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, List<int[]>>>>(json, opts)
                   ?? new Dictionary<string, Dictionary<string, List<int[]>>>();

        var rows = new List<LifeScaleRow>();
        foreach (var (race, classMap) in data)
        {
            foreach (var (className, pairs) in classMap)
            {
                for (var i = 0; i < pairs.Count; i++)
                {
                    var pair = pairs[i];
                    if (pair.Length < 2)
                        continue;

                    rows.Add(new LifeScaleRow
                    {
                        race = race,
                        @class = className,
                        idx = i,
                        body = pair[0],
                        loc = pair[1]
                    });
                }
            }
        }

        return rows;
    }

    private static int TryParseTableNumber(string resourceName)
    {
        var marker = "table_";
        var idx = resourceName.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return 0;

        var start = idx + marker.Length;
        var end = resourceName.IndexOf(".json", start, StringComparison.OrdinalIgnoreCase);
        if (end < 0)
            end = resourceName.Length;

        var slice = resourceName.Substring(start, end - start);
        return int.TryParse(slice, out var value) ? value : 0;
    }

    private static void AddParam(IDbCommand cmd, string name, object? value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }

    private static string DeterministicGuid(string input)
    {
        using var sha1 = SHA1.Create();
        var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(input.ToLowerInvariant()));
        Span<byte> guidBytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes).ToString();
    }

    private static int ParseCost(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return 0;

        var digits = new string(raw.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var value) ? value : 0;
    }

    private static string SerializeAvailability(JsonElement available)
    {
        if (available.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return "[]";

        return available.GetRawText();
    }

    private static string NormalizeForNgrams(string s)
    {
        var b = new StringBuilder();
        foreach (var ch in s)
        {
            if (char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
                b.Append(ch);
            else
                b.Append(' ');
        }

        return string.Join(' ', b.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
    }

    private static IEnumerable<string> GenerateNgrams(string s, int n)
    {
        if (s.Length <= n)
        {
            if (s.Length > 0)
                yield return s;
            yield break;
        }

        for (var i = 0; i <= s.Length - n; i++)
            yield return s.Substring(i, n);
    }

    private sealed class AbilityRaw
    {
        public JsonElement available { get; set; }
        public string index { get; set; } = string.Empty;
        public string? desc { get; set; }
        public string? cost { get; set; }
        public int table { get; set; }
        public List<string>? preReqs { get; set; }
        public string? sourceBook { get; set; }
    }

    private sealed class LifeScaleRow
    {
        public string race { get; set; } = string.Empty;
        public string @class { get; set; } = string.Empty;
        public int idx { get; set; }
        public int body { get; set; }
        public int loc { get; set; }
    }
}
