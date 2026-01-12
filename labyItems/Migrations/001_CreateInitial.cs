using FluentMigrator;

namespace labyItems.Migrations
{
    [Migration(1)]
    public class CreateInitial : Migration
    {
        public override void Up()
        {
            // Create main evocs table
            Create.Table("evocs")
                .WithColumn("id").AsString(36).PrimaryKey().NotNullable()
                .WithColumn("name").AsString().NotNullable()
                .WithColumn("description").AsString().Nullable()
                .WithColumn("data_json").AsString().Nullable()
                .WithColumn("name_lower").AsString().Nullable();

            Create.Index("idx_evocs_name_lower").OnTable("evocs").OnColumn("name_lower").Ascending();

            // FTS5 virtual table for evocs (name + description)
            Execute.Sql(@"CREATE VIRTUAL TABLE IF NOT EXISTS evocs_fts USING fts5(name, description, content='evocs', content_rowid='rowid');");

            // n-gram tokens table for substring search
            Create.Table("evoc_ngrams")
                .WithColumn("id").AsInt32().PrimaryKey().Identity()
                .WithColumn("evoc_id").AsString(36).NotNullable()
                .WithColumn("ngram").AsString().NotNullable();
            Create.Index("idx_evoc_ngrams_ngram").OnTable("evoc_ngrams").OnColumn("ngram").Ascending();

            // Evolution unified table
            Create.Table("evolution")
                .WithColumn("id").AsString(36).PrimaryKey().NotNullable()
                .WithColumn("table_id").AsInt32().NotNullable()
                .WithColumn("index_in_table").AsInt32().NotNullable()
                .WithColumn("name").AsString().NotNullable()
                .WithColumn("description").AsString().Nullable()
                .WithColumn("data_json").AsString().Nullable()
                .WithColumn("name_lower").AsString().Nullable();

            Create.Index("idx_evolution_name_lower").OnTable("evolution").OnColumn("name_lower").Ascending();

            // n-gram tokens table for evolution
            Create.Table("evolution_ngrams")
                .WithColumn("id").AsInt32().PrimaryKey().Identity()
                .WithColumn("evolution_id").AsString(36).NotNullable()
                .WithColumn("ngram").AsString().NotNullable();
            Create.Index("idx_evolution_ngrams_ngram").OnTable("evolution_ngrams").OnColumn("ngram").Ascending();
        }

        public override void Down()
        {
            Delete.Index("idx_evolution_ngrams_ngram").OnTable("evolution_ngrams");
            Delete.Table("evolution_ngrams");

            Delete.Index("idx_evolution_name_lower").OnTable("evolution");
            Delete.Table("evolution");

            Delete.Index("idx_evoc_ngrams_ngram").OnTable("evoc_ngrams");
            Delete.Table("evoc_ngrams");

            Execute.Sql("DROP TABLE IF EXISTS evocs_fts;");
            Delete.Index("idx_evocs_name_lower").OnTable("evocs");
            Delete.Table("evocs");
        }
    }
}