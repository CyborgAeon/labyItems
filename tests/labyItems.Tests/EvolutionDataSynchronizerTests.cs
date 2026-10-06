using System.Text;
using System.Text.Json;
using labyItems.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace labyItems.Tests;

public sealed class EvolutionDataSynchronizerTests : ServiceTestBase
{
    [Fact]
    public async Task EnsureCurrentAsync_ReprocessesChangedJson_WhenBuildIdIsSame()
    {
        var dbPath = Path.Combine(
            ServiceTestEnvironment.CacheDirectory,
            $"evolution-sync-{Guid.NewGuid():N}.db");
        await File.WriteAllBytesAsync(dbPath, Array.Empty<byte>());
        await InsertStaleEvolutionMetadataAsync(dbPath);

        FileSystem.SetPackageOverride(
            "evolution_classes/merged.json",
            () => ToStream("""
                [
                  {
                    "index": "checksum-source-ability",
                    "displayName": "Checksum Source Ability",
                    "desc": "Fresh source row",
                    "cost": 10,
                    "table": 1,
                    "available": "Any",
                    "sourceBook": "Classes",
                    "abilityRef": "ability.test.checksum-source"
                  }
                ]
                """));
        FileSystem.SetPackageOverride("Manufacturers_guide/makes_abilities.json", () => ToStream("[]"));

        var synchronizer = new EvolutionDataSynchronizer(NullLogger<EvolutionDataSynchronizer>.Instance);

        await synchronizer.EnsureCurrentAsync(dbPath);

        using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT display_name, data_json FROM evolution WHERE idx = $idx LIMIT 1;";
        cmd.Parameters.AddWithValue("$idx", "checksum-source-ability");
        using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("Checksum Source Ability", reader.GetString(0));
        var dataJson = reader.GetString(1);

        using var doc = JsonDocument.Parse(dataJson);
        Assert.Equal("Classes", doc.RootElement.GetProperty("sourceBook").GetString());
        Assert.Equal("ability.test.checksum-source", doc.RootElement.GetProperty("abilityRef").GetString());
        Assert.Equal("checksum-source-ability", doc.RootElement.GetProperty("index").GetString());
        Assert.Equal("Checksum Source Ability", doc.RootElement.GetProperty("displayName").GetString());
    }

    private static async Task InsertStaleEvolutionMetadataAsync(string dbPath)
    {
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE seed_metadata (
              seed_version TEXT,
              schema_version INTEGER,
              build_id TEXT,
              checksum TEXT,
              created_at TEXT
            );

            INSERT INTO seed_metadata (seed_version, schema_version, build_id, checksum, created_at)
            VALUES ('evolution-defaults-v3', 1, 'test+0', 'OLD-CHECKSUM', '2026-01-01T00:00:00Z');
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    private static Stream ToStream(string value)
        => new MemoryStream(Encoding.UTF8.GetBytes(value));
}
