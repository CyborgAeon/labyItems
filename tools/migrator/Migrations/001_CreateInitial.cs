using FluentMigrator;

namespace migrator.Migrations
{
    [Migration(1)]
    public class CreateInitial : Migration
    {
        public override void Up()
        {
            // Mirror of the schema used by the generator - safe to run idempotently
            Create.Table("evocs")
                .WithColumn("id").AsString(36).PrimaryKey().NotNullable()
                .WithColumn("name").AsString().NotNullable()
                .WithColumn("name_lower").AsString().Nullable()
                .WithColumn("power").AsInt32().Nullable()
                .WithColumn("range").AsString().Nullable()
                .WithColumn("duration").AsString().Nullable()
                .WithColumn("verbal").AsString().Nullable()
                .WithColumn("fields_json").AsString().Nullable()
                .WithColumn("description").AsString().Nullable()
                .WithColumn("is_advanced").AsInt32().Nullable()
                .WithColumn("data_json").AsString().Nullable()
                .WithColumn("is_default").AsInt32().Nullable()
                .WithColumn("created_at").AsString().Nullable()
                .WithColumn("updated_at").AsString().Nullable();

            Create.Index("idx_evocs_name_lower").OnTable("evocs").OnColumn("name_lower").Ascending();

            Execute.Sql(@"CREATE VIRTUAL TABLE IF NOT EXISTS evocs_fts USING fts5(name, description);");
            Execute.Sql(@"CREATE TABLE IF NOT EXISTS evocs_fts_map(fts_rowid INTEGER PRIMARY KEY, evoc_id TEXT);");

            if (!Schema.Table("evoc_ngrams").Exists())
            {
                Create.Table("evoc_ngrams")
                    .WithColumn("token").AsString().NotNullable()
                    .WithColumn("evoc_id").AsString().NotNullable();
                Create.Index("idx_evoc_ngrams_token").OnTable("evoc_ngrams").OnColumn("token").Ascending();
            }

            if (!Schema.Table("evolution").Exists())
            {
                Create.Table("evolution")
                    .WithColumn("id").AsString(36).PrimaryKey().NotNullable()
                    .WithColumn("idx").AsString().NotNullable()
                    .WithColumn("idx_lower").AsString().Nullable()
                    .WithColumn("description").AsString().Nullable()
                    .WithColumn("cost").AsInt32().Nullable()
                    .WithColumn("available").AsString().Nullable()
                    .WithColumn("table_id").AsInt32().Nullable()
                    .WithColumn("data_json").AsString().Nullable()
                    .WithColumn("is_default").AsInt32().Nullable()
                    .WithColumn("created_at").AsString().Nullable()
                    .WithColumn("updated_at").AsString().Nullable();

                Create.Index("idx_evolution_idx_lower").OnTable("evolution").OnColumn("idx_lower").Ascending();
            }

            if (!Schema.Table("evolution_ngrams").Exists())
            {
                Create.Table("evolution_ngrams")
                    .WithColumn("token").AsString().NotNullable()
                    .WithColumn("evolution_id").AsString().NotNullable();
                Create.Index("idx_evolution_ngrams_token").OnTable("evolution_ngrams").OnColumn("token").Ascending();
            }

            if (!Schema.Table("seed_metadata").Exists())
            {
                Create.Table("seed_metadata")
                    .WithColumn("seed_version").AsString().Nullable()
                    .WithColumn("schema_version").AsInt64().Nullable()
                    .WithColumn("build_id").AsString().Nullable()
                    .WithColumn("checksum").AsString().Nullable()
                    .WithColumn("created_at").AsString().Nullable();
            }
        }

        public override void Down()
        {
            // no destructive down in initial migration
        }
    }
}