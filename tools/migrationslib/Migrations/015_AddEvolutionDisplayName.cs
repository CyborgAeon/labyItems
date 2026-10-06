using FluentMigrator;

namespace MigrationsLib.Migrations;

[Migration(15)]
public sealed class AddEvolutionDisplayName : Migration
{
    public override void Up()
    {
        if (!Schema.Table("evolution").Column("display_name").Exists())
        {
            Alter.Table("evolution")
                .AddColumn("display_name").AsString().Nullable();
        }

        Execute.Sql(@"
UPDATE evolution
SET display_name = idx
WHERE display_name IS NULL OR trim(display_name) = '';

CREATE INDEX IF NOT EXISTS idx_evolution_display_name
ON evolution(display_name COLLATE NOCASE);");
    }

    public override void Down()
    {
        Execute.Sql("DROP INDEX IF EXISTS idx_evolution_display_name;");
        if (Schema.Table("evolution").Column("display_name").Exists())
            Delete.Column("display_name").FromTable("evolution");
    }
}
