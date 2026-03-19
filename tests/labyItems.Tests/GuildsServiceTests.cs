using System.Linq;
using System.Text.Json;
using labyItems.Models.Characters;
using labyItems.Models.Rules;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class GuildsServiceTests : ServiceTestBase
{
    [Fact]
    public async Task GetAllAsync_LoadsGuildDefinitions()
    {
        var all = await GuildsService.GetAllAsync();

        Assert.NotEmpty(all);
    }

    [Fact]
    public async Task GetGuildNamesAsync_ReturnsSortedNames()
    {
        var names = await GuildsService.GetGuildNamesAsync();

        var sorted = names.OrderBy(x => x, System.StringComparer.OrdinalIgnoreCase).ToList();
        Assert.Equal(sorted, names);
    }

    [Fact]
    public async Task GetAllAsync_LoadsOptionalLoreFieldsWhenPresent()
    {
        var all = await GuildsService.GetAllAsync();

        var withLore = all.Values.FirstOrDefault(record =>
            !string.IsNullOrWhiteSpace(record.PreRequisites)
            || !string.IsNullOrWhiteSpace(record.Restrictions)
            || !string.IsNullOrWhiteSpace(record.Ethos)
            || !string.IsNullOrWhiteSpace(record.Background));

        Assert.NotNull(withLore);
    }

    [Fact]
    public async Task GetAllAsync_LoadsGuildLogoFieldWhenPresent()
    {
        var all = await GuildsService.GetAllAsync();

        Assert.True(all.TryGetValue("Church of Iron & Empire", out var church));
        Assert.NotNull(church);
        Assert.Equal("~/guilds/logos/iron_and_empire.png", church!.Logo);
    }

    [Fact]
    public async Task GetAllAsync_LoadsDenominationalMiracleReferenceWhenPresent()
    {
        var all = await GuildsService.GetAllAsync();

        Assert.True(all.TryGetValue("Church of Certizal, Shadow of the First Evil", out var church));
        Assert.NotNull(church);
        Assert.Equal("The Evil Within", church!.DenominationalMiracle?.Ref);
        Assert.Equal("This miracle may be learnt as normal by priests of Certizal post 8th.", church.DenominationalMiracleNote);
    }

    [Fact]
    public async Task GuildsJson_DoesNotContainLegacyAlignmentRuleField()
    {
        var json = await ServiceHelper.ReadPackageTextAsync("people/guilds.json");
        using var document = JsonDocument.Parse(json);

        Assert.False(ContainsAlignmentRuleKey(document.RootElement));
    }

    [Fact]
    public void GetAlignmentRule_BuildsFromAvailabilityRules()
    {
        var record = new GuildRecord
        {
            Availability = new GuildAvailability
            {
                Rules = new List<RuleClause>
                {
                    new()
                    {
                        Field = "Alignment.Order",
                        Operator = RuleComparisonOp.In,
                        Value = new() { "Lawful" }
                    },
                    new()
                    {
                        Field = "Alignment.Moral",
                        Operator = RuleComparisonOp.In,
                        Value = new() { "Good" }
                    }
                }
            }
        };

        var rule = GuildsService.GetAlignmentRule(record);

        Assert.NotNull(rule);
        Assert.Equal("restrict", rule!.Mode);
        Assert.Contains(OrderAxis.Lawful, rule.Allowed!.Order!);
        Assert.Contains(MoralAxis.Good, rule.Allowed!.Moral!);
    }

    [Fact]
    public void GetAlignmentRule_PrefersAvailabilityOverLegacyAlignmentRuleField()
    {
        var record = new GuildRecord
        {
            AlignmentRule = new AlignmentRule
            {
                Mode = "restrict",
                Allowed = new AllowedAxes
                {
                    Moral = new List<MoralAxis> { MoralAxis.Evil },
                    Order = new List<OrderAxis> { OrderAxis.Lawful }
                }
            },
            Availability = new GuildAvailability
            {
                Rules = new List<RuleClause>
                {
                    new()
                    {
                        Field = "Alignment.Order",
                        Operator = RuleComparisonOp.In,
                        Value = new() { "Chaotic" }
                    },
                    new()
                    {
                        Field = "Alignment.Moral",
                        Operator = RuleComparisonOp.In,
                        Value = new() { "Good" }
                    }
                }
            }
        };

        var rule = GuildsService.GetAlignmentRule(record);

        Assert.NotNull(rule);
        Assert.Contains(OrderAxis.Chaotic, rule!.Allowed!.Order!);
        Assert.Contains(MoralAxis.Good, rule.Allowed!.Moral!);
        Assert.DoesNotContain(MoralAxis.Evil, rule.Allowed!.Moral!);
    }

    [Fact]
    public void GetAlignmentRule_BuildsFallbackFromAvailabilityRules_WhenRepresentable()
    {
        var record = new GuildRecord
        {
            Availability = new GuildAvailability
            {
                Rules = new List<RuleClause>
                {
                    new()
                    {
                        Field = "Alignment.Moral",
                        Operator = RuleComparisonOp.In,
                        Value = new List<string> { "Good" }
                    },
                    new()
                    {
                        Field = "Status",
                        Operator = RuleComparisonOp.NotIn,
                        Value = new List<string> { "Outlawed" }
                    }
                }
            }
        };

        var rule = GuildsService.GetAlignmentRule(record);

        Assert.NotNull(rule);
        Assert.Contains(MoralAxis.Good, rule!.Allowed!.Moral!);
    }

    [Fact]
    public void GetAlignmentRule_ReturnsNullForConflictingAlignmentRules()
    {
        var record = new GuildRecord
        {
            Availability = new GuildAvailability
            {
                Rules = new List<RuleClause>
                {
                    new()
                    {
                        Field = "Alignment.Moral",
                        Operator = RuleComparisonOp.In,
                        Value = new List<string> { "Good" }
                    },
                    new()
                    {
                        Field = "Alignment.Moral",
                        Operator = RuleComparisonOp.In,
                        Value = new List<string> { "Neutral" }
                    }
                }
            }
        };

        var rule = GuildsService.GetAlignmentRule(record);

        Assert.Null(rule);
    }

    [Fact]
    public async Task GetAllAsync_ExpandsTotemicGuardiansPrimalSoulChoiceSets()
    {
        var all = await GuildsService.GetAllAsync();

        Assert.True(all.TryGetValue("Totemic Guardians", out var guild));
        Assert.NotNull(guild);

        var basicNames = guild!.Benefits.Basic
            .SelectMany(entry => entry.Options ?? new List<GuildBenefitOption>())
            .SelectMany(option => option.Abilities ?? new List<AbilityDefinition>())
            .Select(ability => ability.Name ?? string.Empty)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        Assert.Contains(basicNames, name => name.StartsWith("Predator Minor:", System.StringComparison.OrdinalIgnoreCase));
        Assert.Contains(basicNames, name => name.StartsWith("Prey Minor:", System.StringComparison.OrdinalIgnoreCase));

        var intermediateNames = guild.Benefits.Intermediate
            .SelectMany(entry => entry.Options ?? new List<GuildBenefitOption>())
            .SelectMany(option => option.Abilities ?? new List<AbilityDefinition>())
            .Select(ability => ability.Name ?? string.Empty)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        Assert.Contains(intermediateNames, name => name.StartsWith("Predator Medium:", System.StringComparison.OrdinalIgnoreCase));
        Assert.Contains(intermediateNames, name => name.StartsWith("Prey Medium:", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetAllAsync_LoadsLoThiUpdatesAndAdvancedTotemicMajorChoices()
    {
        var all = await GuildsService.GetAllAsync();

        Assert.True(all.TryGetValue("Lo'Thi", out var guild));
        Assert.NotNull(guild);

        var intermediateOwlsWisdom = guild!.Benefits.Intermediate
            .Select(entry => entry.Ability)
            .FirstOrDefault(ability => string.Equals(ability?.Name, "Owl's Wisdom", System.StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(intermediateOwlsWisdom);
        Assert.Equal("Update", intermediateOwlsWisdom!.Type);
        Assert.Equal(1, intermediateOwlsWisdom.Count);
        Assert.Contains("major prayer", intermediateOwlsWisdom.Effect ?? string.Empty, System.StringComparison.OrdinalIgnoreCase);

        var advancedOwlsWisdom = guild.Benefits.Advanced
            .Select(entry => entry.Ability)
            .FirstOrDefault(ability => string.Equals(ability?.Name, "Owl's Wisdom", System.StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(advancedOwlsWisdom);
        Assert.Equal(2, advancedOwlsWisdom!.Count);

        var advancedTotemicMarkings = guild.Benefits.Advanced
            .Select(entry => entry.Ability)
            .FirstOrDefault(ability => string.Equals(ability?.Name, "Totemic Markings", System.StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(advancedTotemicMarkings);
        Assert.Equal(new List<int> { 6, 2 }, advancedTotemicMarkings!.Amount);

        var advancedChoiceNames = guild.Benefits.Advanced
            .SelectMany(entry => entry.Options ?? new List<GuildBenefitOption>())
            .SelectMany(option => option.Abilities ?? new List<AbilityDefinition>())
            .Select(ability => ability.Name ?? string.Empty)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        Assert.Contains(advancedChoiceNames, name => name.StartsWith("Predator Major:", System.StringComparison.OrdinalIgnoreCase));
        Assert.Contains(advancedChoiceNames, name => name.StartsWith("Prey Major:", System.StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsAlignmentRuleKey(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, "alignmentRule", System.StringComparison.OrdinalIgnoreCase))
                        return true;

                    if (ContainsAlignmentRuleKey(property.Value))
                        return true;
                }

                return false;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (ContainsAlignmentRuleKey(item))
                        return true;
                }

                return false;

            default:
                return false;
        }
    }
}
