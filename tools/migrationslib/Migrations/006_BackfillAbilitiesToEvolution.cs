using System.Data;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentMigrator;

namespace MigrationsLib.Migrations;

[Migration(6)]
public sealed class BackfillAbilitiesToEvolution : Migration
{
    private const int NgramSize = 3;

    public override void Up()
    {
        if (!Schema.Table("evolution").Exists())
            return;

        if (!Schema.Table("evolution").Column("can_buy_multiple").Exists())
            Alter.Table("evolution").AddColumn("can_buy_multiple").AsInt32().Nullable();

        if (!Schema.Table("evolution").Column("prereqs_json").Exists())
            Alter.Table("evolution").AddColumn("prereqs_json").AsString().Nullable();

        if (!Schema.Table("evolution_ngrams").Exists())
        {
            Create.Table("evolution_ngrams")
                .WithColumn("token").AsString().NotNullable()
                .WithColumn("evolution_id").AsString().NotNullable();
        }

        Execute.Sql("CREATE INDEX IF NOT EXISTS idx_evolution_ngrams_token ON evolution_ngrams(token);");
        Execute.Sql("CREATE INDEX IF NOT EXISTS idx_evolution_ngrams_evolution_id ON evolution_ngrams(evolution_id);");

        var abilities = LoadAllAbilitiesFromResources();
        if (abilities.Count == 0)
            return;

        Execute.WithConnection((conn, tran) =>
        {
            using var exists = conn.CreateCommand();
            exists.Transaction = tran;
            exists.CommandText = "SELECT 1 FROM evolution WHERE id = @id LIMIT 1;";

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
                var available = a.available;
                var costRaw = (a.cost ?? string.Empty).Trim();
                var canBuyMultiple = costRaw.Contains('*');
                var hasPlus = costRaw.Contains('+');
                var cost = ParseCost(costRaw);

                List<string>? preReqs = null;
                if (hasPlus)
                    preReqs = a.preReqs is { Count: > 0 } ? a.preReqs : new List<string>();
                else if (a.preReqs is { Count: > 0 })
                    preReqs = a.preReqs;

                var id = DeterministicGuid(name, a.table);

                exists.Parameters.Clear();
                AddParam(exists, "@id", id);
                if (exists.ExecuteScalar() != null)
                    continue;

                insert.Parameters.Clear();
                AddParam(insert, "@id", id);
                AddParam(insert, "@idx", name);
                AddParam(insert, "@idx_lower", name.ToLowerInvariant());
                AddParam(insert, "@description", desc);
                AddParam(insert, "@cost", cost);
                AddParam(insert, "@available", available);
                AddParam(insert, "@table_id", a.table);
                AddParam(insert, "@can_buy_multiple", canBuyMultiple ? 1 : 0);
                AddParam(insert, "@prereqs_json", preReqs is null ? null : JsonSerializer.Serialize(preReqs));
                var dataJson = JsonSerializer.Serialize(new
                {
                    available,
                    index = name,
                    desc,
                    cost,
                    a.table,
                    canBuyMultiple,
                    preReqs
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
        });
    }

    public override void Down()
    {
        if (!Schema.Table("evolution").Exists())
            return;

        var abilities = LoadAllAbilitiesFromResources();
        if (abilities.Count == 0)
            return;

        var hasNgrams = Schema.Table("evolution_ngrams").Exists();

        Execute.WithConnection((conn, tran) =>
        {
            using var deleteNgram = hasNgrams ? conn.CreateCommand() : null;
            if (deleteNgram != null)
            {
                deleteNgram.Transaction = tran;
                deleteNgram.CommandText = "DELETE FROM evolution_ngrams WHERE evolution_id = @id;";
            }

            using var delete = conn.CreateCommand();
            delete.Transaction = tran;
            delete.CommandText = "DELETE FROM evolution WHERE id = @id;";

            foreach (var a in abilities)
            {
                var name = (a.index ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var id = DeterministicGuid(name, a.table);

                if (deleteNgram != null)
                {
                    deleteNgram.Parameters.Clear();
                    AddParam(deleteNgram, "@id", id);
                    deleteNgram.ExecuteNonQuery();
                }

                delete.Parameters.Clear();
                AddParam(delete, "@id", id);
                delete.ExecuteNonQuery();
            }
        });
    }

    private static void AddParam(IDbCommand cmd, string name, object? value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }

    private static string DeterministicGuid(string name, int table)
    {
        using var sha1 = SHA1.Create();
        var input = $"evo|ability|{table}|{name}";
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

    private static List<AbilityRaw> LoadAllAbilitiesFromResources()
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
            .FirstOrDefault(n => n.EndsWith("makes_abilities.json", StringComparison.OrdinalIgnoreCase));
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
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(n => n.Contains("evolution_classes", StringComparison.OrdinalIgnoreCase) && n.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (resourceNames.Count == 0)
            return new List<AbilityRaw>();

        var list = new List<AbilityRaw>();
        foreach (var resourceName in resourceNames)
        {
            var table = TryParseTableNumber(resourceName);
            if (table <= 0)
                continue;

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
                continue;

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var rows = JsonSerializer.Deserialize<List<EvolutionRaw>>(json, opts) ?? new List<EvolutionRaw>();
            foreach (var row in rows)
            {
                list.Add(new AbilityRaw
                {
                    available = row.available,
                    index = row.index,
                    desc = row.desc,
                    cost = row.cost,
                    table = table
                });
            }
        }

        return list;
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

    private sealed class AbilityRaw
    {
        public List<string> available { get; set; } = new();
        public string index { get; set; } = string.Empty;
        public string? desc { get; set; }
        public string? cost { get; set; }
        public int table { get; set; }
        public List<string>? preReqs { get; set; }
    }

    private sealed class EvolutionRaw
    {
        public List<string> available { get; set; } = new();
        public string index { get; set; } = string.Empty;
        public string? desc { get; set; }
        public string? cost { get; set; }

    }
}
