using FluentMigrator;

namespace MigrationsLib.Migrations;

[Migration(11)]
public sealed class RemoveHumanBarbarianSubtypeOption : Migration
{
    public override void Up()
    {
        Execute.WithConnection((conn, tran) =>
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tran;
            cmd.CommandText = """
                UPDATE races
                SET data_json = replace(
                    replace(data_json,
                        'Standard,Baronial,Ishmaic,Barbarian,Amlesian',
                        'Standard,Baronial,Ishmaic,Amlesian'),
                    'Standard, Baronial, Ishmaic, Barbarian, Amlesian',
                    'Standard, Baronial, Ishmaic, Amlesian')
                WHERE lower(name) = 'human'
                  AND data_json IS NOT NULL;
                """;
            cmd.ExecuteNonQuery();
        });
    }

    public override void Down()
    {
        // no-op
    }
}
