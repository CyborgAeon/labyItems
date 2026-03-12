using System.Linq;
using System.Text.Json;
using labyItems.Models.Characters;
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
    public void GetAlignmentRule_BuildsFallbackFromAvailability()
    {
        var record = new GuildRecord
        {
            Availability = new GuildAvailability
            {
                Whitelist = new GuildAvailabilityRules
                {
                    Alignments = new GuildAvailabilityAlignments
                    {
                        Order = new() { "Lawful" },
                        Moral = new() { "Good" }
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
                Whitelist = new GuildAvailabilityRules
                {
                    Alignments = new GuildAvailabilityAlignments
                    {
                        Order = new() { "Chaotic" },
                        Moral = new() { "Good" }
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
