using System.Text.Json;
using labyItems.Models.WorldMap;
using Microsoft.Data.Sqlite;
using Microsoft.Maui.Storage;

namespace labyItems.Services;

// Public, packaged reference data only. Never reads the active member database.
public sealed class WorldRegionRepository : IWorldRegionRepository
{
    private readonly string databasePath;
    public WorldRegionRepository(string databasePath) => this.databasePath = databasePath;

    public static WorldRegionRepository CreatePublic() => new(Path.Combine(
        FileSystem.AppDataDirectory, "account-vaults", "public.db"));

    public static async Task SeedAsync(string publicDatabasePath)
    {
        var months = JsonSerializer.Deserialize<List<string>>(await ServiceHelper.ReadPackageTextAsync("world-map/stats-index.json")) ?? [];
        var seeds = new List<string>();
        foreach (var month in months)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(month, @"^\d{4}-(0[1-9]|1[0-2])$"))
                throw new InvalidDataException("Invalid packaged world statistics month.");
            seeds.Add(await ServiceHelper.ReadPackageTextAsync($"world-map/stats/{month}.sql"));
        }
        using var connection = new SqliteConnection($"Data Source={publicDatabasePath};Pooling=False");
        connection.Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        foreach (var sql in seeds)
        {
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public async Task<IReadOnlyList<WorldRegion>> GetRegionsAsync() =>
        JsonSerializer.Deserialize<List<WorldRegion>>(await ServiceHelper.ReadPackageTextAsync("world-map/regions.json")) ?? [];

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly;Pooling=False");
        connection.Open();
        return connection;
    }

    public IReadOnlyList<WorldStatsPeriod> GetPeriods()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT Year, Month FROM WorldStatistic ORDER BY Year DESC, Month DESC";
        using var reader = command.ExecuteReader();
        var result = new List<WorldStatsPeriod>();
        while (reader.Read()) result.Add(new(reader.GetInt32(0), reader.GetInt32(1)));
        return result;
    }

    public IReadOnlyList<string> GetStatisticsRegions()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT RegionId FROM WorldStatistic ORDER BY RegionId";
        using var reader = command.ExecuteReader();
        var result = new List<string>();
        while (reader.Read()) result.Add(reader.GetString(0));
        return result;
    }

    public IReadOnlyList<WorldStatistic> GetStatistics(string statisticsId, WorldStatsPeriod period)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Category, Name, Value FROM WorldStatistic WHERE RegionId=$id AND Year=$year AND Month=$month ORDER BY Category, Ordinal";
        command.Parameters.AddWithValue("$id", statisticsId);
        command.Parameters.AddWithValue("$year", period.Year);
        command.Parameters.AddWithValue("$month", period.Month);
        using var reader = command.ExecuteReader();
        var result = new List<WorldStatistic>();
        while (reader.Read()) result.Add(new(reader.GetString(0), reader.GetString(1), reader.GetDecimal(2)));
        return result;
    }
}
