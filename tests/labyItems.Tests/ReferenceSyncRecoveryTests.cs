using System.Text;
using labyItems.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Maui.Storage;
using Xunit;

namespace labyItems.Tests;

public sealed class ReferenceSyncRecoveryTests : ServiceTestBase
{
    [Fact]
    public async Task EvolutionSync_RepairsMissingPackagedRow_WithUnchangedChecksum()
    {
        var dbPath = await CreateDatabaseAsync();
        const string seed = """[{"index":"first","table":1},{"index":"second","table":1}]""";
        FileSystem.SetPackageOverride("evolution_classes/merged.json",
            () => new MemoryStream(Encoding.UTF8.GetBytes(seed)));
        var sync = new EvolutionDataSynchronizer(NullLogger<EvolutionDataSynchronizer>.Instance);
        await sync.EnsureCurrentAsync(dbPath);

        using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync();
        await ExecuteAsync(conn, "DELETE FROM evolution WHERE idx = 'second';");
        await sync.EnsureCurrentAsync(dbPath);

        Assert.Equal(2L, await ScalarAsync(conn, "SELECT COUNT(*) FROM evolution WHERE is_default = 1;"));
        Assert.True(await ScalarAsync(conn, "SELECT COUNT(*) FROM evolution_ngrams;") > 0);
    }

    [Theory]
    [InlineData("ability_definitions")]
    [InlineData("ability_effect_instructions")]
    public async Task AbilitySync_RepairsDeletedDefaults_WithUnchangedChecksumAndBuild(string table)
    {
        var dbPath = await CreateDatabaseAsync();
        var sync = new AbilityDefinitionDataSynchronizer(NullLogger<AbilityDefinitionDataSynchronizer>.Instance);
        await sync.EnsureCurrentAsync(dbPath);
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync();
        var expected = await ScalarAsync(conn, $"SELECT COUNT(*) FROM {table} WHERE is_default = 1;");
        Assert.True(expected > 0);
        await ExecuteAsync(conn, $"DELETE FROM {table} WHERE rowid = (SELECT rowid FROM {table} WHERE is_default = 1 LIMIT 1);");

        await sync.EnsureCurrentAsync(dbPath);

        Assert.Equal(expected, await ScalarAsync(conn, $"SELECT COUNT(*) FROM {table} WHERE is_default = 1;"));
    }

    [Fact]
    public async Task AbilitySync_PreservesCustomSameKeyOverride_WhenRepairingAnotherDefault()
    {
        var dbPath = await CreateDatabaseAsync();
        const string seed = """
            {"abilities":{"custom-key":{"name":"Packaged original","effect":"Original"},
            "missing-key":{"name":"Other packaged ability"}}}
            """;
        FileSystem.SetPackageOverride("specialisation/abilities.json",
            () => new MemoryStream(Encoding.UTF8.GetBytes(seed)));
        var sync = new AbilityDefinitionDataSynchronizer(NullLogger<AbilityDefinitionDataSynchronizer>.Instance);
        await sync.EnsureCurrentAsync(dbPath);
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync();
        await ExecuteAsync(conn, """
            UPDATE ability_definitions SET is_default=0, effect_text='Custom effect' WHERE ability_key='custom-key';
            INSERT INTO ability_effect_instructions(id,ability_key,instruction_json,is_default)
                VALUES ('custom-instruction','custom-key','{}',0);
            DELETE FROM ability_definitions WHERE ability_key='missing-key';
            """);

        await sync.EnsureCurrentAsync(dbPath);
        await sync.EnsureCurrentAsync(dbPath);

        Assert.Equal(1L, await ScalarAsync(conn, "SELECT COUNT(*) FROM ability_definitions WHERE ability_key='custom-key' AND is_default=0 AND effect_text='Custom effect';"));
        Assert.Equal(1L, await ScalarAsync(conn, "SELECT COUNT(*) FROM ability_effect_instructions WHERE id='custom-instruction' AND is_default=0;"));
        Assert.Equal(1L, await ScalarAsync(conn, "SELECT COUNT(*) FROM ability_definitions WHERE ability_key='missing-key' AND is_default=1;"));
    }

    private static async Task<string> CreateDatabaseAsync()
    {
        var path = Path.Combine(ServiceTestEnvironment.CacheDirectory, $"reference-recovery-{Guid.NewGuid():N}.db");
        await File.WriteAllBytesAsync(path, Array.Empty<byte>());
        return path;
    }

    private static async Task ExecuteAsync(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<long> ScalarAsync(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }
}
