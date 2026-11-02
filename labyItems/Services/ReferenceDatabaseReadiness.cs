using Microsoft.Data.Sqlite;

namespace labyItems.Services;

public static class ReferenceDatabaseReadiness
{
    private static readonly string[] RequiredTables =
    {
        "classes", "races", "lifescales", "spells", "miracles", "evocs", "neuronics", "evolution", "ability_definitions"
    };

    public static void Validate(string dbPath, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString());
        connection.Open();
        var unavailable = new List<string>();
        foreach (var table in RequiredTables)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var exists = connection.CreateCommand();
            exists.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name";
            exists.Parameters.AddWithValue("$name", table);
            if (Convert.ToInt64(exists.ExecuteScalar()) == 0)
            {
                unavailable.Add(table);
                continue;
            }
            using var count = connection.CreateCommand();
            count.CommandText = $"SELECT EXISTS(SELECT 1 FROM \"{table}\" LIMIT 1)";
            if (Convert.ToInt64(count.ExecuteScalar()) == 0)
                unavailable.Add(table);
        }
        if (unavailable.Count > 0)
            throw new InvalidOperationException($"Reference data initialization is incomplete: {string.Join(", ", unavailable)}. Retry startup to repair the reference data.");
    }
}
