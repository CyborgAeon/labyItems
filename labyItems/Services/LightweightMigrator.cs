using System;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace labyItems.Services
{
	public static class LightweightMigrator
	{
		public static void ApplyInitialSchema(string dbPath, ILogger? logger = null)
		{
			try
			{
				using var conn = new SqliteConnection($"Data Source={dbPath}");
				conn.Open();

				using var tx = conn.BeginTransaction();
				using var cmd = conn.CreateCommand();

				cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS evocs (
  id TEXT PRIMARY KEY,
  name TEXT NOT NULL,
  name_lower TEXT,
  power INTEGER,
  range TEXT,
  duration TEXT,
  verbal TEXT,
  fields_json TEXT,
  description TEXT,
  is_advanced INTEGER,
  data_json TEXT,
  is_default INTEGER,
  created_at TEXT,
  updated_at TEXT
);
CREATE INDEX IF NOT EXISTS idx_evocs_name_lower ON evocs(name_lower);

CREATE TABLE IF NOT EXISTS miracles (
  id TEXT PRIMARY KEY,
  name TEXT NOT NULL,
  name_lower TEXT,
  power INTEGER,
  sphere TEXT,
  alignment TEXT,
  description TEXT,
  is_advanced INTEGER,
  data_json TEXT,
  created_at TEXT,
  updated_at TEXT
);
CREATE INDEX IF NOT EXISTS idx_miracles_name_lower ON miracles(name_lower);

CREATE TABLE IF NOT EXISTS spells (
  id TEXT PRIMARY KEY,
  name TEXT NOT NULL,
  name_lower TEXT,
  level INTEGER,
  colour TEXT,
  range TEXT,
  duration TEXT,
  verbal TEXT,
  description TEXT,
  is_advanced INTEGER,
  data_json TEXT,
  created_at TEXT,
  updated_at TEXT
);
CREATE INDEX IF NOT EXISTS idx_spells_name_lower ON spells(name_lower);
";
				cmd.ExecuteNonQuery();

				// FTS5 creation can fail if not available in SQLite build; ignore errors here
				try
				{
					using var ftsCmd = conn.CreateCommand();
					ftsCmd.CommandText = "CREATE VIRTUAL TABLE IF NOT EXISTS evocs_fts USING fts5(name, description);";
					ftsCmd.ExecuteNonQuery();
					using var mapCmd = conn.CreateCommand();
					mapCmd.CommandText = "CREATE TABLE IF NOT EXISTS evocs_fts_map(fts_rowid INTEGER PRIMARY KEY, evoc_id TEXT);";
					mapCmd.ExecuteNonQuery();
				}
				catch (Exception ex)
				{
					logger?.LogWarning(ex, "FTS5 not available or failed to create evocs_fts: {Message}", ex.Message);
				}

				cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS evoc_ngrams(token TEXT, evoc_id TEXT);
CREATE INDEX IF NOT EXISTS idx_evoc_ngrams_token ON evoc_ngrams(token);
CREATE INDEX IF NOT EXISTS idx_evoc_ngrams_evoc_id ON evoc_ngrams(evoc_id);
";
				cmd.ExecuteNonQuery();

				cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS evolution (
  id TEXT PRIMARY KEY,
  idx TEXT NOT NULL,
  idx_lower TEXT,
  description TEXT,
  cost INTEGER,
  available TEXT,
  table_id INTEGER,
  can_buy_multiple INTEGER,
  prereqs_json TEXT,
  data_json TEXT,
  is_default INTEGER,
  created_at TEXT,
  updated_at TEXT
);
CREATE INDEX IF NOT EXISTS idx_evolution_idx_lower ON evolution(idx_lower);
";
				cmd.ExecuteNonQuery();

				cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS evolution_ngrams(token TEXT, evolution_id TEXT);
CREATE INDEX IF NOT EXISTS idx_evolution_ngrams_token ON evolution_ngrams(token);
CREATE INDEX IF NOT EXISTS idx_evolution_ngrams_evolution_id ON evolution_ngrams(evolution_id);
";
				cmd.ExecuteNonQuery();

				cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS seed_metadata (
  seed_version TEXT,
  schema_version INTEGER,
  build_id TEXT,
  checksum TEXT,
  created_at TEXT
);
";
				cmd.ExecuteNonQuery();

				tx.Commit();
				logger?.LogInformation("Lightweight schema applied to {DbPath}", dbPath);
				conn.Close();
			}
			catch (Exception ex)
			{
				logger?.LogError(ex, "Lightweight schema application failed: {Message}", ex.Message);
				throw;
			}
		}
	}
}
