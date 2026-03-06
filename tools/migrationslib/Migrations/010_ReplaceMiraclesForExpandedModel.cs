using System.Data;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentMigrator;

namespace MigrationsLib.Migrations;

[Migration(10)]
public sealed class ReplaceMiraclesForExpandedModel : Migration
{
    public override void Up()
    {
        EnsureMiraclesTable();
        Execute.Sql("CREATE INDEX IF NOT EXISTS idx_miracles_name_lower ON miracles(name_lower);");

        var miracles = LoadMiraclesFromResource();
        if (miracles.Count == 0)
            return;

        Execute.WithConnection((conn, tran) =>
        {
            using (var delete = conn.CreateCommand())
            {
                delete.Transaction = tran;
                delete.CommandText = "DELETE FROM miracles;";
                delete.ExecuteNonQuery();
            }

            using var insert = conn.CreateCommand();
            insert.Transaction = tran;
            insert.CommandText = @"INSERT OR IGNORE INTO miracles (id, name, name_lower, power, sphere, alignment, description, is_advanced, data_json, created_at, updated_at)
VALUES (@id, @name, @name_lower, @power, @sphere, @alignment, @description, @is_advanced, @data_json, @created_at, @updated_at);";

            var now = DateTime.UtcNow.ToString("o");
            var index = 0;
            foreach (var m in miracles)
            {
                var id = DeterministicGuid(m, index++);
                var name = (m.name ?? string.Empty).Trim();
                var description = (m.description ?? string.Empty).Trim();
                var sphere = (m.sphere ?? string.Empty).Trim();
                var alignment = (m.alignment ?? string.Empty).Trim();
                var isAdvanced = m.isAdvanced ? 1 : 0;

                insert.Parameters.Clear();
                AddParam(insert, "@id", id);
                AddParam(insert, "@name", name);
                AddParam(insert, "@name_lower", name.ToLowerInvariant());
                AddParam(insert, "@power", m.power);
                AddParam(insert, "@sphere", sphere);
                AddParam(insert, "@alignment", alignment);
                AddParam(insert, "@description", description);
                AddParam(insert, "@is_advanced", isAdvanced);
                AddParam(insert, "@data_json", JsonSerializer.Serialize(m));
                AddParam(insert, "@created_at", now);
                AddParam(insert, "@updated_at", now);

                insert.ExecuteNonQuery();
            }
        });
    }

    public override void Down()
    {
        Execute.Sql("DELETE FROM miracles;");
    }

    private void EnsureMiraclesTable()
    {
        if (Schema.Table("miracles").Exists())
            return;

        Create.Table("miracles")
            .WithColumn("id").AsString(36).PrimaryKey().NotNullable()
            .WithColumn("name").AsString().NotNullable()
            .WithColumn("name_lower").AsString().Nullable()
            .WithColumn("power").AsInt32().Nullable()
            .WithColumn("sphere").AsString().Nullable()
            .WithColumn("alignment").AsString().Nullable()
            .WithColumn("description").AsString().Nullable()
            .WithColumn("is_advanced").AsInt32().Nullable()
            .WithColumn("data_json").AsString().Nullable()
            .WithColumn("created_at").AsString().Nullable()
            .WithColumn("updated_at").AsString().Nullable();
    }

    private static List<MiracleRaw> LoadMiraclesFromResource()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n =>
                n.EndsWith("miracles.json", StringComparison.OrdinalIgnoreCase) ||
                n.EndsWith("base-miracles1.json", StringComparison.OrdinalIgnoreCase) ||
                n.EndsWith("miracles1.json", StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
            return new List<MiracleRaw>();

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            return new List<MiracleRaw>();

        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<List<MiracleRaw>>(json, opts) ?? new List<MiracleRaw>();
    }

    private static string DeterministicGuid(MiracleRaw m, int index)
    {
        using var sha1 = SHA1.Create();
        var input = $"{index}|{m.name}|{m.power}|{m.alignment}|{m.sphere}|{m.description}";
        var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(input.ToLowerInvariant()));
        Span<byte> guidBytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes).ToString();
    }

    private static void AddParam(IDbCommand cmd, string name, object? value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }

    private sealed class MiracleRaw
    {
        public int power { get; set; }
        public string? name { get; set; }
        public string? description { get; set; }
        public string? sphere { get; set; }
        public bool isAdvanced { get; set; }
        public string? alignment { get; set; }
        public string? verbal { get; set; }
        public string? range { get; set; }
        public string? duration { get; set; }
        public string? gesture { get; set; }
        public string? level { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }
}
