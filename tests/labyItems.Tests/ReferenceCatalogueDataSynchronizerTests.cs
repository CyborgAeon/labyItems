using System.Text.Json;
using labyItems.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace labyItems.Tests;

public sealed class ReferenceCatalogueDataSynchronizerTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"catalogue-sync-{Guid.NewGuid():N}.db");
    private static Dictionary<string, string> Assets() => new()
    {
        ["grimoire/allSpells.json"] = """[{"name":"Test spell","level":2,"colour":"Blue","IsAdvanced":true,"sourcePage":17,"Heal":{"amount":[[6,6]]}}]""",
        ["words_from_above/miracles.json"] = """[{"name":"Test miracle","power":3,"sphere":"Healing","alignment":"Good","description":"Test","immunityName":["Poison"]}]""",
        ["druids_way/evocs.json"] = """[{"name":"Test evocation","power":1,"fields":["Spring","Forests"],"description":"Test nature","Damage":{"amount":[[1,1]],"ArmourType":"PAC"}}]""",
        ["o-insight/o_insight_neuronics.json"] = """[{"name":"Test neuronic","power":4,"tree":"Passive","immunities":["Mind effects"],"Damage":{"amount":[[2,2]]}}]"""
    };
    private static ReferenceCatalogueDataSynchronizer Create(IReadOnlyDictionary<string, string>? assets = null) =>
        new(new PackageFiles(assets ?? Assets()), NullLogger<ReferenceCatalogueDataSynchronizer>.Instance);

    [Fact]
    public async Task FreshDatabase_SeedsAllCataloguesAndPreservesCompleteJson()
    {
        await Create().EnsureCurrentAsync(_dbPath);
        using var connection = Open();
        foreach (var table in new[] { "spells", "miracles", "evocs", "neuronics" })
            Assert.Equal(1L, Scalar(connection, $"SELECT COUNT(*) FROM {table};"));
        Assert.Equal(1L, Scalar(connection, "SELECT is_advanced FROM spells;"));
        Assert.Equal("Passive", Scalar(connection, "SELECT type FROM neuronics;"));
        using var spell = JsonDocument.Parse((string)Scalar(connection, "SELECT data_json FROM spells;"));
        Assert.Equal(17, spell.RootElement.GetProperty("sourcePage").GetInt32());
        Assert.Equal(6, spell.RootElement.GetProperty("Heal").GetProperty("amount")[0][0].GetInt32());
        using var neuronic = JsonDocument.Parse((string)Scalar(connection, "SELECT data_json FROM neuronics;"));
        Assert.Equal("Mind effects", neuronic.RootElement.GetProperty("immunities")[0].GetString());
        Assert.Equal(1L, Scalar(connection, "SELECT COUNT(*) FROM evocs_fts WHERE evocs_fts MATCH 'nature';"));
        Assert.Equal(1L, Scalar(connection, "SELECT COUNT(*) FROM evocs_fts_map m JOIN evocs e ON e.id=m.evoc_id;"));
        Assert.True((long)Scalar(connection, "SELECT COUNT(*) FROM evoc_ngrams;") > 0);
    }

    [Fact]
    public async Task PartialDatabase_RepairsMissingDefaultsAndKeepsCustomRowsAndExistingIds()
    {
        var sync = Create();
        await sync.EnsureCurrentAsync(_dbPath);
        using (var connection = Open())
        {
            Execute(connection, """
                UPDATE spells SET id='existing-spell', description='existing description';
                DELETE FROM miracles;
                DELETE FROM evocs;
                DELETE FROM neuronics;
                INSERT INTO evocs(id,name,name_lower,power,fields_json,data_json,is_default)
                    VALUES ('custom','Test evocation','test evocation',1,'["Spring","Forests"]','{"name":"Test evocation","nonStandard":true,"description":"custom"}',0);
                """);
        }
        await sync.EnsureCurrentAsync(_dbPath);
        await sync.EnsureCurrentAsync(_dbPath);
        using var repaired = Open();
        Assert.Equal("existing-spell", Scalar(repaired, "SELECT id FROM spells;"));
        Assert.Equal("existing description", Scalar(repaired, "SELECT description FROM spells;"));
        Assert.Equal(1L, Scalar(repaired, "SELECT COUNT(*) FROM miracles;"));
        Assert.Equal(1L, Scalar(repaired, "SELECT COUNT(*) FROM neuronics;"));
        Assert.Equal(2L, Scalar(repaired, "SELECT COUNT(*) FROM evocs;"));
        Assert.Equal(1L, Scalar(repaired, "SELECT COUNT(*) FROM evocs WHERE id='custom' AND is_default=0;"));
        Assert.Contains("custom", (string)Scalar(repaired, "SELECT data_json FROM evocs WHERE id='custom';"));
        Assert.Equal(2L, Scalar(repaired, "SELECT COUNT(*) FROM evocs_fts_map;"));
    }

    [Fact]
    public async Task EvocationSearchTokens_MatchSharedNormalizationIncludingShortNames()
    {
        var assets = Assets();
        assets["druids_way/evocs.json"] = """[{"name":"A!","power":1,"fields":[]},{"name":"Nature's Gift","power":1,"fields":[]}]""";
        await Create(assets).EnsureCurrentAsync(_dbPath);
        using var connection = Open();
        Assert.Equal(1L, Scalar(connection, "SELECT COUNT(*) FROM evoc_ngrams WHERE token='a';"));
        Assert.Equal(1L, Scalar(connection, "SELECT COUNT(*) FROM evoc_ngrams WHERE token='nat';"));
        Assert.Equal(0L, Scalar(connection, "SELECT COUNT(*) FROM evoc_ngrams WHERE token LIKE '%!%' OR token LIKE '%''%';"));
    }

    [Fact]
    public async Task ExistingFieldOrderAndWhitespace_DoNotCreateDuplicateEvocation()
    {
        await Create().EnsureCurrentAsync(_dbPath);
        using (var connection = Open())
            Execute(connection, "UPDATE evocs SET fields_json='[ \"Forests\", \"Spring\" ]', id='old-default';");
        await Create().EnsureCurrentAsync(_dbPath);
        using var result = Open();
        Assert.Equal(1L, Scalar(result, "SELECT COUNT(*) FROM evocs;"));
        Assert.Equal("old-default", Scalar(result, "SELECT id FROM evocs;"));
    }

    [Fact]
    public async Task InvalidPackagedAsset_FailsBeforeWritingAnyRows()
    {
        var assets = Assets();
        assets["o-insight/o_insight_neuronics.json"] = "[]";
        await Assert.ThrowsAsync<InvalidDataException>(() => Create(assets).EnsureCurrentAsync(_dbPath));
        Assert.False(File.Exists(_dbPath));
    }

    [Fact]
    public async Task Cancellation_DoesNotCreateDatabase()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create().EnsureCurrentAsync(_dbPath, cancellation.Token));
        Assert.False(File.Exists(_dbPath));
    }

    [Fact]
    public async Task PackagedRepositoryCatalogues_SeedWithoutGeneratedDatabase()
    {
        var root = FindRepositoryRoot();
        var assets = Assets().Keys.ToDictionary(path => path,
            path => File.ReadAllText(Path.Combine(root, "labyItems", "Resources", "Raw", path)));
        await Create(assets).EnsureCurrentAsync(_dbPath);
        using var connection = Open();
        foreach (var (path, table) in new[]
        {
            ("grimoire/allSpells.json", "spells"), ("words_from_above/miracles.json", "miracles"),
            ("druids_way/evocs.json", "evocs"), ("o-insight/o_insight_neuronics.json", "neuronics")
        })
        {
            using var json = JsonDocument.Parse(assets[path]);
            Assert.Equal(json.RootElement.GetArrayLength(), (long)Scalar(connection, $"SELECT COUNT(*) FROM {table};"));
        }
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        return connection;
    }
    private static object Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar()!;
    }
    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "labyItems.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }
    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
    private sealed class PackageFiles(IReadOnlyDictionary<string, string> assets) : IFileService
    {
        public string CacheDirectory => Path.GetTempPath();
        public string AppDataDirectory => Path.GetTempPath();
        public string CombineCachePath(string fileName) => Path.Combine(CacheDirectory, fileName);
        public Task<string> ReadPackageTextAsync(string relativePath) => Task.FromResult(assets[relativePath]);
        public Task WriteTextAsync(string path, string content) => File.WriteAllTextAsync(path, content);
    }
}
