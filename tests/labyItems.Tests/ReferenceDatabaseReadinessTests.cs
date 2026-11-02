using labyItems.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace labyItems.Tests;

public sealed class ReferenceDatabaseReadinessTests
{
    [Fact]
    public void Validate_RejectsMissingTablesAndEmptyCatalogues()
    {
        WithDatabase((path, connection) =>
        {
            using var setup = connection.CreateCommand();
            setup.CommandText = "CREATE TABLE spells (id TEXT PRIMARY KEY);";
            setup.ExecuteNonQuery();

            var exception = Assert.Throws<InvalidOperationException>(() => ReferenceDatabaseReadiness.Validate(path));

            Assert.Contains("classes", exception.Message);
            Assert.Contains("spells", exception.Message);
            Assert.Contains("Retry", exception.Message);
        });
    }

    [Fact]
    public void Validate_RequiresEveryCatalogueToRemainPopulated()
    {
        WithDatabase((path, connection) =>
        {
            foreach (var table in new[] { "classes", "races", "lifescales", "spells", "miracles", "evocs", "neuronics", "evolution", "ability_definitions" })
            {
                using var setup = connection.CreateCommand();
                setup.CommandText = $"CREATE TABLE {table} (id TEXT PRIMARY KEY); INSERT INTO {table} VALUES ('reference');";
                setup.ExecuteNonQuery();
            }
            ReferenceDatabaseReadiness.Validate(path);

            using var delete = connection.CreateCommand();
            delete.CommandText = "DELETE FROM miracles";
            delete.ExecuteNonQuery();
            var exception = Assert.Throws<InvalidOperationException>(() => ReferenceDatabaseReadiness.Validate(path));
            Assert.Contains("miracles", exception.Message);
            Assert.DoesNotContain("classes", exception.Message);
        });
    }

    private static void WithDatabase(Action<string, SqliteConnection> action)
    {
        var path = Path.Combine(Path.GetTempPath(), $"laby-readiness-{Guid.NewGuid():N}.db");
        try
        {
            using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
            connection.Open();
            action(path, connection);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }
}
