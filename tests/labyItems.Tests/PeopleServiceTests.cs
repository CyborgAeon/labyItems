using System;
using System.Collections.Generic;
using System.Linq;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class PeopleServiceTests : ServiceTestBase
{
    [Fact]
    public async Task GetAllAsync_LoadsPeopleDefinitions()
    {
        var all = await PeopleService.GetAllAsync();

        Assert.NotEmpty(all);
        Assert.Contains(all, kvp => !string.IsNullOrWhiteSpace(kvp.Key));
    }

    [Fact]
    public async Task GetAllAsync_SpiritlessTagOnlyOnExpectedRaces()
    {
        var all = await PeopleService.GetAllAsync();

        var spiritlessRaces = all
            .Where(pair => pair.Value.Tags.Any(tag => tag.Equals("Spiritless", StringComparison.OrdinalIgnoreCase)))
            .Select(pair => pair.Key)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var expected = new[] { "Athfanal", "Elf", "Faerie", "Farfolk", "Samila" }
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.Equal(expected, spiritlessRaces);
    }

    [Fact]
    public async Task GetAllAsync_RaceLevelledAbilitiesContainNormalizedResistanceRefs()
    {
        var all = await PeopleService.GetAllAsync();

        Assert.True(HasRaceAbilityRef(all, "Elf", "ability.spiritless"));
        Assert.True(HasRaceAbilityRef(all, "Athfanal", "ability.spiritless"));
        Assert.True(HasRaceAbilityRef(all, "Faerie", "ability.spiritless"));
        Assert.True(HasRaceAbilityRef(all, "Farfolk", "ability.spiritless"));

        Assert.True(HasRaceAbilityRef(all, "Half Elf", "ability.half-spirit"));
        Assert.True(HasRaceAbilityRef(all, "Half Athfanal", "ability.half-spirit"));
        Assert.True(HasRaceAbilityRef(all, "Calabrim", "ability.half-spirit"));
        Assert.True(HasRaceAbilityRef(all, "Alfar", "ability.half-spirit"));

        Assert.True(HasRaceAbilityRef(all, "Samila", "ability.half-effect-magic"));
        Assert.True(HasRaceAbilityRef(all, "Samila", "ability.spiritless"));
        Assert.True(HasRaceAbilityRef(all, "Samila", "ability.mindless"));
    }

    private static bool HasRaceAbilityRef(
        IReadOnlyDictionary<string, PeopleRecord> races,
        string raceName,
        string abilityRef)
    {
        if (!races.TryGetValue(raceName, out var race))
            return false;

        foreach (var level in race.LevelledAbilities.Values)
        {
            foreach (var ability in level)
            {
                if (ability == null)
                    continue;

                if (abilityRef.Equals(ability.AbilityRef, StringComparison.OrdinalIgnoreCase)
                    || abilityRef.Equals(ability.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
