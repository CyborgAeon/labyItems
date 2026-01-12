using System;
using Microsoft.Extensions.DependencyInjection;
using FluentMigrator.Runner;
using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;
using MigrationsLib.Migrations;

namespace migrator
{
    public class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("Usage: migrator <path-to-db>");
                return 2;
            }

            var dbPath = args[0];
            if (!System.IO.File.Exists(dbPath))
            {
                Console.Error.WriteLine($"DB file not found: {dbPath}");
                return 3;
            }

            var services = new ServiceCollection()
                .AddFluentMigratorCore()
                .ConfigureRunner(rb => rb
                    .AddSQLite()
                    .WithGlobalConnectionString($"Data Source={dbPath}")
                    .ScanIn(typeof(MigrationsLib.Migrations.InitialMigration).Assembly).For.Migrations())
                .AddLogging(lb => lb.AddConsole())
                .BuildServiceProvider(false);

            using (var scope = services.CreateScope())
            {
                var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
                Console.WriteLine("Running migrations...");
                runner.MigrateUp();
                Console.WriteLine("Migrations complete.");
            }

            try
            {
                using var conn = new SqliteConnection($"Data Source={dbPath}");
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT MAX(version) FROM VersionInfo;"; // VersionInfo is maintained by FluentMigrator

                var res = cmd.ExecuteScalar();
                long schemaVersion = 0;
                if (res != null && long.TryParse(res.ToString(), out var v)) schemaVersion = v;

                if (schemaVersion > 0)
                {
                    using var upd = conn.CreateCommand();
                    upd.CommandText = "UPDATE seed_metadata SET schema_version = @schema WHERE rowid = (SELECT rowid FROM seed_metadata ORDER BY rowid DESC LIMIT 1);";
                    upd.Parameters.AddWithValue("@schema", schemaVersion);
                    var r = upd.ExecuteNonQuery();
                    Console.WriteLine($"Updated seed_metadata.schema_version to {schemaVersion} (rows affected: {r})");
                }
                else
                {
                    Console.WriteLine("No VersionInfo entries found; leaving seed_metadata.schema_version unchanged.");
                }
                conn.Close();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error updating seed metadata: " + ex.Message);
                return 4;
            }

            return 0;
        }
    }
}
