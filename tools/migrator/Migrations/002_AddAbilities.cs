using FluentMigrator;

namespace migrator.Migrations
{
    [Migration(2)]
    public sealed class AddAbilities : Migration
    {
        public override void Up()
        {
            if (Schema.Table("evolution").Exists())
            {
                if (!Schema.Table("evolution").Column("can_buy_multiple").Exists())
                    Alter.Table("evolution").AddColumn("can_buy_multiple").AsInt32().Nullable();

                if (!Schema.Table("evolution").Column("prereqs_json").Exists())
                    Alter.Table("evolution").AddColumn("prereqs_json").AsString().Nullable();
            }

            if (!Schema.Table("abilities").Exists())
            {
                Create.Table("abilities")
                    .WithColumn("id").AsString(36).PrimaryKey().NotNullable()
                    .WithColumn("idx").AsString().NotNullable()
                    .WithColumn("idx_lower").AsString().Nullable()
                    .WithColumn("description").AsString().Nullable()
                    .WithColumn("cost").AsInt32().Nullable()
                    .WithColumn("available").AsString().Nullable()
                    .WithColumn("table_id").AsInt32().Nullable()
                    .WithColumn("can_buy_multiple").AsInt32().Nullable()
                    .WithColumn("prereqs_json").AsString().Nullable()
                    .WithColumn("data_json").AsString().Nullable()
                    .WithColumn("is_default").AsInt32().Nullable()
                    .WithColumn("created_at").AsString().Nullable()
                    .WithColumn("updated_at").AsString().Nullable();

                Create.Index("idx_abilities_idx_lower").OnTable("abilities").OnColumn("idx_lower").Ascending();
            }

            if (!Schema.Table("abilities_ngrams").Exists())
            {
                Create.Table("abilities_ngrams")
                    .WithColumn("token").AsString().NotNullable()
                    .WithColumn("ability_id").AsString().NotNullable();
                Create.Index("idx_abilities_ngrams_token").OnTable("abilities_ngrams").OnColumn("token").Ascending();
                Create.Index("idx_abilities_ngrams_ability_id").OnTable("abilities_ngrams").OnColumn("ability_id").Ascending();
            }
        }

        public override void Down()
        {
            // no destructive down migration
        }
    }
}
