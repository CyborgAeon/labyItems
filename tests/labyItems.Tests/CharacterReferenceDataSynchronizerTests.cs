using System.Text.Json;
using labyItems.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace labyItems.Tests;

public sealed class CharacterReferenceDataSynchronizerTests : IDisposable
{
    private readonly string _dbPath;

    public CharacterReferenceDataSynchronizerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"laby-sync-{Guid.NewGuid():N}.db");
        CreateSchema(_dbPath);
    }

    [Fact]
    public async Task EnsureCurrentAsync_RefreshesStandardReferenceData_AndPreservesNonStandardRows()
    {
        SeedExistingData(_dbPath);

        var fileService = new FakeFileService(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["people/classes.json"] = """
{
  "Unholy Champion": {
    "Levels": {
      "2": [
        { "Name": "Unholy Champion Devotion", "Type": "Overwrite", "OverwriteKey": "Unholy Champion Devotion" }
      ],
      "6": [
        { "Name": "Unholy Champion Specialist", "Type": "Overwrite", "OverwriteKey": "Unholy Champion Specialist" }
      ]
    }
  }
}
""",
            ["people/people.json"] = """
{
  "Human": {
    "Description": "Baseline human"
  }
}
""",
            ["people/lifescales.json"] = """
{
  "Human": {
    "Unholy Champion": [
      [12, 6],
      [13, 6]
    ]
  }
}
"""
        });

        var synchronizer = new CharacterReferenceDataSynchronizer(
            NullLogger<CharacterReferenceDataSynchronizer>.Instance,
            fileService);

        await synchronizer.EnsureCurrentAsync(_dbPath);

        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var unholyJson = ReadScalar(conn, "SELECT data_json FROM classes WHERE lower(name) = 'unholy champion' LIMIT 1;");
        Assert.Contains("Unholy Champion Devotion", unholyJson, StringComparison.Ordinal);
        Assert.Contains("Unholy Champion Specialist", unholyJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Unholy Champion Specialist skill", unholyJson, StringComparison.Ordinal);

        var nonStandardCount = Convert.ToInt32(ReadScalar(conn,
            "SELECT COUNT(1) FROM classes WHERE lower(name) = 'my custom class' AND instr(data_json, '\"NonStandard\":true') > 0;"));
        Assert.Equal(1, nonStandardCount);

        var defaultLifeScaleBody = Convert.ToInt32(ReadScalar(conn,
            "SELECT body FROM lifescales WHERE lower(race) = 'human' AND lower(\"class\") = 'unholy champion' AND idx = 1;"));
        Assert.Equal(12, defaultLifeScaleBody);

        var preservedCustomLifeScale = Convert.ToInt32(ReadScalar(conn,
            "SELECT COUNT(1) FROM lifescales WHERE lower(race) = 'my custom race' AND lower(\"class\") = 'my custom class';"));
        Assert.Equal(1, preservedCustomLifeScale);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_dbPath))
                File.Delete(_dbPath);
        }
        catch
        {
        }
    }

    private static void CreateSchema(string dbPath)
    {
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
CREATE TABLE classes (
  id TEXT PRIMARY KEY,
  name TEXT NOT NULL,
  name_lower TEXT,
  data_json TEXT,
  created_at TEXT,
  updated_at TEXT
);
CREATE TABLE races (
  id TEXT PRIMARY KEY,
  name TEXT NOT NULL,
  name_lower TEXT,
  data_json TEXT,
  created_at TEXT,
  updated_at TEXT
);
CREATE TABLE lifescales (
  race TEXT NOT NULL,
  "class" TEXT NOT NULL,
  idx INTEGER NOT NULL,
  body INTEGER NOT NULL,
  loc INTEGER NOT NULL
);
CREATE TABLE seed_metadata (
  seed_version TEXT,
  schema_version INTEGER,
  build_id TEXT,
  checksum TEXT,
  created_at TEXT
);
""";
        cmd.ExecuteNonQuery();
    }

    private static void SeedExistingData(string dbPath)
    {
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();

        var now = DateTime.UtcNow.ToString("o");

        Execute(conn,
            "INSERT INTO classes (id, name, name_lower, data_json, created_at, updated_at) VALUES ($id, $name, $lower, $data, $created, $updated);",
            ("$id", "std-unholy"),
            ("$name", "Unholy Champion"),
            ("$lower", "unholy champion"),
            ("$data", """
{"Levels":{"2":[{"Name":"Repel Good","Type":"Static"}],"6":[{"Name":"Unholy Champion Specialist skill","Type":"Overwrite","OverwriteKey":"Unholy Champion Specialist skill"}]}}
"""),
            ("$created", now),
            ("$updated", now));

        Execute(conn,
            "INSERT INTO classes (id, name, name_lower, data_json, created_at, updated_at) VALUES ($id, $name, $lower, $data, $created, $updated);",
            ("$id", "custom-class"),
            ("$name", "My Custom Class"),
            ("$lower", "my custom class"),
            ("$data", """{"NonStandard":true,"Levels":{"1":[{"Name":"Custom Ability"}]}}"""),
            ("$created", now),
            ("$updated", now));

        Execute(conn,
            "INSERT INTO races (id, name, name_lower, data_json, created_at, updated_at) VALUES ($id, $name, $lower, $data, $created, $updated);",
            ("$id", "std-human"),
            ("$name", "Human"),
            ("$lower", "human"),
            ("$data", """{"Description":"Old human"}"""),
            ("$created", now),
            ("$updated", now));

        Execute(conn,
            "INSERT INTO races (id, name, name_lower, data_json, created_at, updated_at) VALUES ($id, $name, $lower, $data, $created, $updated);",
            ("$id", "custom-race"),
            ("$name", "My Custom Race"),
            ("$lower", "my custom race"),
            ("$data", """{"NonStandard":true,"Description":"Custom race"}"""),
            ("$created", now),
            ("$updated", now));

        Execute(conn,
            "INSERT INTO lifescales (race, \"class\", idx, body, loc) VALUES ($race, $class, $idx, $body, $loc);",
            ("$race", "Human"),
            ("$class", "Unholy Champion"),
            ("$idx", 1),
            ("$body", 99),
            ("$loc", 99));

        Execute(conn,
            "INSERT INTO lifescales (race, \"class\", idx, body, loc) VALUES ($race, $class, $idx, $body, $loc);",
            ("$race", "My Custom Race"),
            ("$class", "My Custom Class"),
            ("$idx", 1),
            ("$body", 7),
            ("$loc", 3));
    }

    private static void Execute(SqliteConnection conn, string sql, params (string Name, object Value)[] parameters)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value);
        cmd.ExecuteNonQuery();
    }

    private static string ReadScalar(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar()?.ToString() ?? string.Empty;
    }

    private sealed class FakeFileService : IFileService
    {
        private readonly IReadOnlyDictionary<string, string> _assets;

        public FakeFileService(IReadOnlyDictionary<string, string> assets)
        {
            _assets = assets;
        }

        public string CacheDirectory => Path.GetTempPath();
        public string AppDataDirectory => Path.GetTempPath();

        public string CombineCachePath(string fileName)
            => Path.Combine(CacheDirectory, fileName ?? string.Empty);

        public Task<string> ReadPackageTextAsync(string relativePath)
            => Task.FromResult(_assets.TryGetValue(relativePath, out var value) ? value : string.Empty);

        public Task WriteTextAsync(string path, string content)
            => File.WriteAllTextAsync(path, content ?? string.Empty);
    }
}
