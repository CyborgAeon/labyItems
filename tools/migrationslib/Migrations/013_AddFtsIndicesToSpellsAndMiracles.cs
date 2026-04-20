using FluentMigrator;

namespace MigrationsLib.Migrations;

/// <summary>
/// Migration to add Full-Text Search (FTS5) indices to spell and miracle tables.
/// This significantly improves search performance for text-based queries.
/// Idempotent: Safe to run multiple times.
/// </summary>
[Migration(13)]
public sealed class AddFtsIndicesToSpellsAndMiracles : Migration
{
    public override void Up()
    {
        // Create FTS5 virtual table for spells if it doesn't exist
        Execute.Sql(@"
            CREATE VIRTUAL TABLE IF NOT EXISTS spells_fts USING fts5(
                name,
                description,
                colour,
                content=spells,
                content_rowid=rowid
            );
        ");

        // Create FTS5 virtual table for miracles if it doesn't exist
        Execute.Sql(@"
            CREATE VIRTUAL TABLE IF NOT EXISTS miracles_fts USING fts5(
                name,
                description,
                sphere,
                content=miracles,
                content_rowid=rowid
            );
        ");

        // Populate FTS tables from content tables (idempotent)
        Execute.Sql(@"
            INSERT OR IGNORE INTO spells_fts(rowid, name, description, colour)
            SELECT rowid, name, description, colour FROM spells;
        ");

        Execute.Sql(@"
            INSERT OR IGNORE INTO miracles_fts(rowid, name, description, sphere)
            SELECT rowid, name, description, sphere FROM miracles;
        ");

        // Create indices for frequently searched columns (if not already indexed)
        Execute.Sql("CREATE INDEX IF NOT EXISTS idx_spells_level ON spells(level);");
        Execute.Sql("CREATE INDEX IF NOT EXISTS idx_spells_is_advanced ON spells(is_advanced);");
        Execute.Sql("CREATE INDEX IF NOT EXISTS idx_spells_colour ON spells(colour);");

        Execute.Sql("CREATE INDEX IF NOT EXISTS idx_miracles_sphere ON miracles(sphere);");
        Execute.Sql("CREATE INDEX IF NOT EXISTS idx_miracles_is_advanced ON miracles(is_advanced);");

        // Ensure evolution table has proper indices
        Execute.Sql("CREATE INDEX IF NOT EXISTS idx_evolution_table_id ON evolution(table_id);");
        Execute.Sql("CREATE INDEX IF NOT EXISTS idx_evocs_is_advanced ON evocs(is_advanced);");
    }

    public override void Down()
    {
        // Drop FTS tables (virtual tables)
        Execute.Sql("DROP TABLE IF EXISTS spells_fts;");
        Execute.Sql("DROP TABLE IF EXISTS miracles_fts;");

        // Drop indices
        Execute.Sql("DROP INDEX IF EXISTS idx_spells_level;");
        Execute.Sql("DROP INDEX IF EXISTS idx_spells_is_advanced;");
        Execute.Sql("DROP INDEX IF EXISTS idx_spells_colour;");
        Execute.Sql("DROP INDEX IF EXISTS idx_miracles_sphere;");
        Execute.Sql("DROP INDEX IF EXISTS idx_miracles_is_advanced;");
        Execute.Sql("DROP INDEX IF EXISTS idx_evolution_table_id;");
        Execute.Sql("DROP INDEX IF EXISTS idx_evocs_is_advanced;");
    }
}
