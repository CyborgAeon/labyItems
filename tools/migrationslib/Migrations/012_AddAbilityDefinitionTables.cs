using FluentMigrator;

namespace MigrationsLib.Migrations;

[Migration(12)]
public sealed class AddAbilityDefinitionTables : Migration
{
    public override void Up()
    {
        if (!Schema.Table("ability_definitions").Exists())
        {
            Create.Table("ability_definitions")
                .WithColumn("id").AsString(36).PrimaryKey().NotNullable()
                .WithColumn("ability_key").AsString().NotNullable()
                .WithColumn("name").AsString().NotNullable()
                .WithColumn("type").AsString().Nullable()
                .WithColumn("source").AsString().Nullable()
                .WithColumn("lore").AsString().Nullable()
                .WithColumn("effect_text").AsString().Nullable()
                .WithColumn("system_effects_json").AsString().Nullable()
                .WithColumn("raw_data_json").AsString().Nullable()
                .WithColumn("metadata_json").AsString().Nullable()
                .WithColumn("is_default").AsInt32().Nullable()
                .WithColumn("created_at").AsString().Nullable()
                .WithColumn("updated_at").AsString().Nullable();
        }

        Execute.Sql("CREATE UNIQUE INDEX IF NOT EXISTS idx_ability_definitions_key ON ability_definitions(ability_key);");

        if (!Schema.Table("ability_choice_sets").Exists())
        {
            Create.Table("ability_choice_sets")
                .WithColumn("id").AsString(36).PrimaryKey().NotNullable()
                .WithColumn("choice_set_key").AsString().NotNullable()
                .WithColumn("raw_data_json").AsString().Nullable()
                .WithColumn("metadata_json").AsString().Nullable()
                .WithColumn("is_default").AsInt32().Nullable()
                .WithColumn("created_at").AsString().Nullable()
                .WithColumn("updated_at").AsString().Nullable();
        }

        Execute.Sql("CREATE UNIQUE INDEX IF NOT EXISTS idx_ability_choice_sets_key ON ability_choice_sets(choice_set_key);");

        if (!Schema.Table("ability_effect_instructions").Exists())
        {
            Create.Table("ability_effect_instructions")
                .WithColumn("id").AsString(36).PrimaryKey().NotNullable()
                .WithColumn("ability_key").AsString().NotNullable()
                .WithColumn("instruction_json").AsString().NotNullable()
                .WithColumn("is_default").AsInt32().Nullable()
                .WithColumn("created_at").AsString().Nullable()
                .WithColumn("updated_at").AsString().Nullable();
        }

        Execute.Sql("CREATE INDEX IF NOT EXISTS idx_ability_effect_instructions_key ON ability_effect_instructions(ability_key);");
    }

    public override void Down()
    {
        // no-op
    }
}
