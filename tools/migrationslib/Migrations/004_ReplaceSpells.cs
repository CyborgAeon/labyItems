using System.Data;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentMigrator;

namespace MigrationsLib.Migrations;

[Migration(4)]
public class ReplaceSpells : Migration
{
    public override void Up()
    {
        EnsureSpellsTable();
        Execute.Sql("CREATE INDEX IF NOT EXISTS idx_spells_name_lower ON spells(name_lower);");

        var spells = LoadSpellsFromResource();
        if (spells.Count == 0)
            return;

        Execute.WithConnection((conn, tran) =>
        {
            using (var delete = conn.CreateCommand())
            {
                delete.Transaction = tran;
                delete.CommandText = "DELETE FROM spells;";
                delete.ExecuteNonQuery();
            }

            using var insert = conn.CreateCommand();
            insert.Transaction = tran;
            insert.CommandText = @"INSERT OR IGNORE INTO spells (id, name, name_lower, level, colour, range, duration, verbal, description, is_advanced, data_json, created_at, updated_at)
VALUES (@id, @name, @name_lower, @level, @colour, @range, @duration, @verbal, @description, @is_advanced, @data_json, @created_at, @updated_at);";

            var now = DateTime.UtcNow.ToString("o");
            var index = 0;
            foreach (var s in spells)
            {
                var id = DeterministicGuid(s, index++);
                var name = (s.name ?? string.Empty).Trim();
                var description = (s.description ?? string.Empty).Trim();
                var colour = (s.colour ?? string.Empty).Trim();
                var range = (s.range ?? string.Empty).Trim();
                var duration = (s.duration ?? string.Empty).Trim();
                var verbal = (s.verbal ?? string.Empty).Trim();
                var isAdvanced = s.isAdvanced ? 1 : 0;

                insert.Parameters.Clear();
                AddParam(insert, "@id", id);
                AddParam(insert, "@name", name);
                AddParam(insert, "@name_lower", name.ToLowerInvariant());
                AddParam(insert, "@level", s.level);
                AddParam(insert, "@colour", colour);
                AddParam(insert, "@range", range);
                AddParam(insert, "@duration", duration);
                AddParam(insert, "@verbal", verbal);
                AddParam(insert, "@description", description);
                AddParam(insert, "@is_advanced", isAdvanced);
                AddParam(insert, "@data_json", JsonSerializer.Serialize(s));
                AddParam(insert, "@created_at", now);
                AddParam(insert, "@updated_at", now);

                insert.ExecuteNonQuery();
            }
        });
    }

    public override void Down()
    {
        Execute.Sql("DELETE FROM spells;");
    }

    private void EnsureSpellsTable()
    {
        if (Schema.Table("spells").Exists())
            return;

        Create.Table("spells")
            .WithColumn("id").AsString(36).PrimaryKey().NotNullable()
            .WithColumn("name").AsString().NotNullable()
            .WithColumn("name_lower").AsString().Nullable()
            .WithColumn("level").AsInt32().Nullable()
            .WithColumn("colour").AsString().Nullable()
            .WithColumn("range").AsString().Nullable()
            .WithColumn("duration").AsString().Nullable()
            .WithColumn("verbal").AsString().Nullable()
            .WithColumn("description").AsString().Nullable()
            .WithColumn("is_advanced").AsInt32().Nullable()
            .WithColumn("data_json").AsString().Nullable()
            .WithColumn("created_at").AsString().Nullable()
            .WithColumn("updated_at").AsString().Nullable();
    }

    private static void AddParam(IDbCommand cmd, string name, object? value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }

    private static string DeterministicGuid(SpellRaw s, int index)
    {
        using var sha1 = SHA1.Create();
        var input = $"{index}|{s.name}|{s.level}|{s.colour}|{s.range}|{s.duration}|{s.verbal}";
        var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(input.ToLowerInvariant()));
        Span<byte> guidBytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes).ToString();
    }

    private static List<SpellRaw> LoadSpellsFromResource()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n =>
                n.EndsWith("new_standard.json", StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
            return new List<SpellRaw>();

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            return new List<SpellRaw>();

        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<List<SpellRaw>>(json, opts) ?? new List<SpellRaw>();
    }

    private sealed class SpellRaw
    {
        public string? name { get; set; }
        public int level { get; set; }
        public string? colour { get; set; }
        public string? range { get; set; }
        public string? duration { get; set; }
        public string? verbal { get; set; }
        public string? description { get; set; }
        public bool isAdvanced { get; set; }
    }
}
