using System.Data;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentMigrator;

namespace MigrationsLib.Migrations;

[Migration(14)]
public sealed class AddNeuronics : Migration
{
    public override void Up()
    {
        EnsureNeuronicsTable();
        Execute.Sql("CREATE INDEX IF NOT EXISTS idx_neuronics_name_lower ON neuronics(name_lower);");
        Execute.Sql("CREATE INDEX IF NOT EXISTS idx_neuronics_type ON neuronics(type);");

        var neuronics = LoadNeuronicsFromResource();
        if (neuronics.Count == 0)
            return;

        Execute.WithConnection((conn, tran) =>
        {
            using (var delete = conn.CreateCommand())
            {
                delete.Transaction = tran;
                delete.CommandText = "DELETE FROM neuronics WHERE COALESCE(is_default, 0) = 1;";
                delete.ExecuteNonQuery();
            }

            using var insert = conn.CreateCommand();
            insert.Transaction = tran;
            insert.CommandText = @"
INSERT OR REPLACE INTO neuronics
(id, name, name_lower, power, type, range, duration, immunities, description, notes, as_per, todo, damage_json, data_json, is_default, created_at, updated_at)
VALUES
(@id, @name, @name_lower, @power, @type, @range, @duration, @immunities, @description, @notes, @as_per, @todo, @damage_json, @data_json, @is_default, @created_at, @updated_at);";

            var now = DateTime.UtcNow.ToString("o");
            var index = 0;
            foreach (var neuronic in neuronics)
            {
                var id = DeterministicGuid(neuronic, index++);
                var name = (neuronic.name ?? string.Empty).Trim();
                if (name.Length == 0)
                    continue;

                var type = ResolveType(neuronic.tree);
                var range = (neuronic.range ?? string.Empty).Trim();
                var duration = (neuronic.duration ?? string.Empty).Trim();
                var immunities = (neuronic.immunities ?? string.Empty).Trim();
                var description = (neuronic.description ?? string.Empty).Trim();
                var notes = (neuronic.notes ?? string.Empty).Trim();
                var asPer = (neuronic.asPer ?? string.Empty).Trim();
                var todo = (neuronic.todo ?? string.Empty).Trim();
                var damageJson = neuronic.Damage == null ? null : JsonSerializer.Serialize(neuronic.Damage, JsonOptions);

                if (type.Equals("None", StringComparison.OrdinalIgnoreCase))
                    neuronic.tree = string.Empty;
                else
                    neuronic.tree = type;

                insert.Parameters.Clear();
                AddParam(insert, "@id", id);
                AddParam(insert, "@name", name);
                AddParam(insert, "@name_lower", name.ToLowerInvariant());
                AddParam(insert, "@power", Math.Max(0, neuronic.power));
                AddParam(insert, "@type", type);
                AddParam(insert, "@range", range);
                AddParam(insert, "@duration", duration);
                AddParam(insert, "@immunities", immunities);
                AddParam(insert, "@description", description);
                AddParam(insert, "@notes", notes);
                AddParam(insert, "@as_per", asPer);
                AddParam(insert, "@todo", todo);
                AddParam(insert, "@damage_json", damageJson);
                AddParam(insert, "@data_json", JsonSerializer.Serialize(neuronic, JsonOptions));
                AddParam(insert, "@is_default", 1);
                AddParam(insert, "@created_at", now);
                AddParam(insert, "@updated_at", now);

                insert.ExecuteNonQuery();
            }
        });
    }

    public override void Down()
    {
        Execute.Sql("DROP INDEX IF EXISTS idx_neuronics_name_lower;");
        Execute.Sql("DROP INDEX IF EXISTS idx_neuronics_type;");
        if (Schema.Table("neuronics").Exists())
            Delete.Table("neuronics");
    }

    private void EnsureNeuronicsTable()
    {
        if (!Schema.Table("neuronics").Exists())
        {
            Create.Table("neuronics")
                .WithColumn("id").AsString(36).PrimaryKey().NotNullable()
                .WithColumn("name").AsString().NotNullable()
                .WithColumn("name_lower").AsString().Nullable()
                .WithColumn("power").AsInt32().Nullable()
                .WithColumn("type").AsString().Nullable()
                .WithColumn("range").AsString().Nullable()
                .WithColumn("duration").AsString().Nullable()
                .WithColumn("immunities").AsString().Nullable()
                .WithColumn("description").AsString().Nullable()
                .WithColumn("notes").AsString().Nullable()
                .WithColumn("as_per").AsString().Nullable()
                .WithColumn("todo").AsString().Nullable()
                .WithColumn("damage_json").AsString().Nullable()
                .WithColumn("data_json").AsString().Nullable()
                .WithColumn("is_default").AsInt32().Nullable()
                .WithColumn("created_at").AsString().Nullable()
                .WithColumn("updated_at").AsString().Nullable();
            return;
        }

        if (!Schema.Table("neuronics").Column("is_default").Exists())
            Alter.Table("neuronics").AddColumn("is_default").AsInt32().Nullable();
    }

    private static List<NeuronicRaw> LoadNeuronicsFromResource()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("o_insight_neuronics.json", StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
            return new List<NeuronicRaw>();

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            return new List<NeuronicRaw>();

        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        return JsonSerializer.Deserialize<List<NeuronicRaw>>(json, JsonOptions) ?? new List<NeuronicRaw>();
    }

    private static string ResolveType(string? tree)
    {
        var token = (tree ?? string.Empty).Trim();
        if (token.Equals("Active", StringComparison.OrdinalIgnoreCase))
            return "Active";
        if (token.Equals("Passive", StringComparison.OrdinalIgnoreCase))
            return "Passive";

        return "None";
    }

    private static string DeterministicGuid(NeuronicRaw neuronic, int index)
    {
        using var sha1 = SHA1.Create();
        var input = $"{index}|{neuronic.name}|{neuronic.power}|{ResolveType(neuronic.tree)}";
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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class NeuronicDamageRaw
    {
        [JsonConverter(typeof(IntArrayListConverter))]
        public List<int[]>? amount { get; set; }
        public List<string>? type { get; set; }
        [JsonConverter(typeof(IntArrayListConverter))]
        public List<int[]>? ArmourApplies { get; set; }
        public string? ArmourType { get; set; }
        public List<int>? PACDam { get; set; }
    }

    private sealed class NeuronicRaw
    {
        public int power { get; set; }
        public string? name { get; set; }
        public string? description { get; set; }
        public string? range { get; set; }
        public string? duration { get; set; }
        [JsonConverter(typeof(SingleOrArrayStringConverter))]
        public string? immunities { get; set; }
        public string? tree { get; set; }
        public string? notes { get; set; }
        public string? todo { get; set; }
        public string? asPer { get; set; }

        [JsonPropertyName("Damage")]
        public NeuronicDamageRaw? Damage { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }

    private sealed class IntArrayListConverter : JsonConverter<List<int[]>?>
    {
        public override List<int[]>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            var result = new List<int[]>();
            if (reader.TokenType != JsonTokenType.StartArray)
                return result;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    return result;

                if (reader.TokenType == JsonTokenType.StartArray)
                    result.Add(ReadIntArray(ref reader));
                else if (reader.TokenType == JsonTokenType.Number)
                    result.Add(new[] { ReadNumberAsInt(ref reader) });
                else
                    reader.Skip();
            }

            return result;
        }

        public override void Write(Utf8JsonWriter writer, List<int[]>? value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStartArray();
            foreach (var pair in value)
            {
                writer.WriteStartArray();
                foreach (var number in pair ?? Array.Empty<int>())
                    writer.WriteNumberValue(number);
                writer.WriteEndArray();
            }
            writer.WriteEndArray();
        }

        private static int[] ReadIntArray(ref Utf8JsonReader reader)
        {
            var values = new List<int>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    return values.ToArray();

                if (reader.TokenType == JsonTokenType.Number)
                    values.Add(ReadNumberAsInt(ref reader));
                else
                    reader.Skip();
            }

            return values.ToArray();
        }

        private static int ReadNumberAsInt(ref Utf8JsonReader reader)
        {
            if (reader.TryGetInt32(out var value))
                return value;

            if (reader.TryGetDouble(out var dbl))
                return (int)Math.Round(dbl);

            return 0;
        }
    }

    private sealed class SingleOrArrayStringConverter : JsonConverter<string?>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType == JsonTokenType.String)
                return reader.GetString();

            if (reader.TokenType != JsonTokenType.StartArray)
            {
                reader.Skip();
                return null;
            }

            var values = new List<string>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    break;

                if (reader.TokenType == JsonTokenType.String)
                {
                    var value = reader.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                        values.Add(value.Trim());
                }
                else
                {
                    reader.Skip();
                }
            }

            return string.Join(", ", values);
        }

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStringValue(value);
        }
    }
}
