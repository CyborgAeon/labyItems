using labyItems.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace labyItems.Tests;

public sealed class FreshInstallReferenceDataTests : ServiceTestBase
{
    [Fact]
    public async Task PackagedJsonBootstrapsUsableReferenceDataWithoutGeneratedDatabase()
    {
        var path = Path.Combine(Path.GetTempPath(), $"laby-fresh-install-{Guid.NewGuid():N}.db");
        try
        {
            LightweightMigrator.ApplyInitialSchema(path);
            var files = new PackagedFiles();
            await new ReferenceCatalogueDataSynchronizer(files, NullLogger<ReferenceCatalogueDataSynchronizer>.Instance).EnsureCurrentAsync(path);
            await new EvolutionDataSynchronizer(NullLogger<EvolutionDataSynchronizer>.Instance).EnsureCurrentAsync(path);
            await new AbilityDefinitionDataSynchronizer(NullLogger<AbilityDefinitionDataSynchronizer>.Instance).EnsureCurrentAsync(path);
            await new CharacterReferenceDataSynchronizer(NullLogger<CharacterReferenceDataSynchronizer>.Instance, files).EnsureCurrentAsync(path);

            ReferenceDatabaseReadiness.Validate(path);
            using var connection = new SqliteConnection($"Data Source={path}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM classes WHERE data_json IS NOT NULL";
            Assert.True(Convert.ToInt32(command.ExecuteScalar()) > 10);
            command.CommandText = "SELECT COUNT(*) FROM spells WHERE data_json IS NOT NULL";
            Assert.True(Convert.ToInt32(command.ExecuteScalar()) > 100);
            command.CommandText = "SELECT COUNT(*) FROM evolution";
            Assert.True(Convert.ToInt32(command.ExecuteScalar()) > 100);
            command.CommandText = "SELECT COUNT(*) FROM ability_definitions";
            Assert.True(Convert.ToInt32(command.ExecuteScalar()) > 100);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    private sealed class PackagedFiles : IFileService
    {
        public string CacheDirectory => ServiceTestEnvironment.CacheDirectory;
        public string AppDataDirectory => ServiceTestEnvironment.AppDataDirectory;
        public string CombineCachePath(string fileName) => Path.Combine(CacheDirectory, fileName);
        public Task<string> ReadPackageTextAsync(string relativePath)
            => File.ReadAllTextAsync(Path.Combine(ServiceTestEnvironment.PackageRoot, relativePath));
        public Task WriteTextAsync(string path, string content) => File.WriteAllTextAsync(path, content);
    }
}
