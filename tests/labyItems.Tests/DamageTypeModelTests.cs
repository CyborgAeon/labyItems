using System.Text.Json;
using labyItems.Models.Enums;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class DamageTypeModelTests
{
    [Fact]
    public void SpellDamage_AcceptsSingleDamageType_AndImmunityName()
    {
        const string json = """
            {
              "name": "Fire Blast",
              "immunityName": "Fire",
              "Damage": { "DamageType": "Fire", "amount": [[12,2]], "type": ["Blast"] }
            }
            """;

        var spell = JsonSerializer.Deserialize<SpellService.SpellRaw>(json);

        Assert.Equal(DamageTypeEnum.Fire, Assert.Single(spell!.GetDamageCategories()));
        Assert.Equal("Fire", Assert.Single(spell.immunityName));
    }

    [Fact]
    public void SpellDamage_AcceptsMultipleDamageTypes_AndImmunities()
    {
        const string json = """
            {
              "name": "Mixed",
              "immunityName": ["Pain", "Neuronic"],
              "Damage": { "DamageType": ["Neuronic", "Pain"], "amount": [[6,1]] }
            }
            """;

        var spell = JsonSerializer.Deserialize<SpellService.SpellRaw>(json);

        Assert.Equal(new[] { DamageTypeEnum.Neuronic, DamageTypeEnum.Pain }, spell!.GetDamageCategories());
        Assert.Equal(new[] { "Pain", "Neuronic" }, spell.immunityName);
    }
}
