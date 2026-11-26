using labyItems.Models.Guilds;
using System.Linq;
using System.Text.Json;
using labyItems.Models.Characters;
using labyItems.Models.Rules;
using labyItems.Services;
using labyItems.Services.Guilds;
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
    public async Task GetAllAsync_NormalizesGuildBenefitAbilitiesWithCanonicalIdentity()
    {
        var all = await GuildsService.GetAllAsync();

        var abilities = all.Values
            .SelectMany(record => record.Benefits.Basic
                .Concat(record.Benefits.Intermediate)
                .Concat(record.Benefits.Advanced))
            .SelectMany(entry =>
            {
                var list = new List<AbilityDefinition>();
                if (entry.Ability != null)
                    list.Add(entry.Ability);
                if (entry.Options != null)
                {
                    foreach (var option in entry.Options)
                        list.AddRange(option.Abilities ?? new List<AbilityDefinition>());
                }
                return list;
            })
            .Where(ability => ability != null)
            .ToList();

        Assert.NotEmpty(abilities);
        Assert.All(abilities, ability =>
        {
            Assert.False(string.IsNullOrWhiteSpace(ability.Key));
            Assert.False(string.IsNullOrWhiteSpace(ability.AbilityRef));
        });
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
    public async Task ResolvesLoThiOwlWisdomProgression()
    {
        var guild = (await GuildsService.GetAllAsync())["Lo'Thi"];
        var basic = ResolveGrant(guild, "Lo'Thi", 1, "b.owl-s-wisdom");
        var advanced = ResolveGrant(guild, "Lo'Thi", 10000, "b.owl-s-wisdom");

        Assert.Equal(1, basic.Count);
        Assert.Equal(2, advanced.Count);
        Assert.Contains("major prayer", advanced.Effect ?? "", System.StringComparison.OrdinalIgnoreCase);

        var advancedOptions = guild.Benefits.Advanced.SelectMany(entry => entry.Options)
            .SelectMany(option => option.Abilities).Select(ability => ability.Name);
        Assert.Contains(advancedOptions, name => name.StartsWith("Predator Major:"));
        Assert.Contains(advancedOptions, name => name.StartsWith("Prey Major:"));
    }

    [Fact]
    public async Task ResolvesShadowLegionUpgradesAgainstTheirOriginalGrant()
    {
        const string name = "Shadow Legion";
        const string id = "shadow-legion.basic.war-cry";
        var guild = (await GuildsService.GetAllAsync())[name];
        var basic = ResolveGrant(guild, name, 1, id);
        var advanced = ResolveGrant(guild, name, 10000, id);
        var firstAid = ResolveGrant(guild, name, 1, "shadow-legion.basic.first-aid");

        Assert.Equal("ability.war-cry", basic.AbilityRef);
        Assert.Equal("Shadow Legion!", basic.Name);
        Assert.Equal("Innate", basic.Type);
        Assert.Equal("ability.bandaging", firstAid.AbilityRef);
        Assert.Equal(basic.GrantId, advanced.GrantId);
        Assert.Equal(1, advanced.Count);
        Assert.Contains("Determination", advanced.Effect ?? "", System.StringComparison.OrdinalIgnoreCase);

        var draft = new CharacterDraft { Points = 10000 };
        var resolution = GuildCharacterBenefitResolver.ResolveGrants(guild, draft, name);
        var choice = Assert.Single(resolution.Choices, choice => choice.Choice.Id == "a.mercenary-soldier-choice.choice");
        var options = choice.Options.SelectMany(option => option.Option.Grants)
            .Select(grant => GuildCharacterBenefitResolver.MaterializeGrantOption(guild, grant).Name).ToArray();
        Assert.Contains("Shadow Legion!", options);
        Assert.Contains("Battle Fury", options);
        Assert.Contains("Medic", options);
    }

    [Fact]
    public async Task GetAllAsync_Maps18thImperialLegionStaminaBenefitsToCanonicalStaminaRefs()
    {
        var all = await GuildsService.GetAllAsync();

        Assert.True(all.TryGetValue("18th Imperial Legion", out var guild));
        Assert.NotNull(guild);

        var basicOptionAbilities = guild!.Benefits.Basic
            .SelectMany(entry => entry.Options ?? new List<GuildBenefitOption>())
            .SelectMany(option => option.Abilities ?? new List<AbilityDefinition>())
            .ToList();
        Assert.Contains(basicOptionAbilities, ability =>
            string.Equals(ability.AbilityRef, "ability.6-2-stamina.1", System.StringComparison.OrdinalIgnoreCase));

        var advancedOptionAbilities = guild.Benefits.Advanced
            .SelectMany(entry => entry.Options ?? new List<GuildBenefitOption>())
            .SelectMany(option => option.Abilities ?? new List<AbilityDefinition>())
            .ToList();
        Assert.Contains(advancedOptionAbilities, ability =>
            string.Equals(ability.AbilityRef, "ability.6-2-stamina.1", System.StringComparison.OrdinalIgnoreCase));
        Assert.Contains(advancedOptionAbilities, ability =>
            string.Equals(ability.AbilityRef, "ability.6-2-stamina.2", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ResolvesSweethavenTierPatchAndDisciplineOther()
    {
        const string name = "The Duke's Head Society";
        const string id = "dukes-head.basic.the-people-of-sweethaven-have-spoken";
        var guild = (await GuildsService.GetAllAsync())[name];
        var basic = ResolveGrant(guild, name, 1, id);
        var advanced = ResolveGrant(guild, name, 10000, id);
        var discipline = ResolveGrant(guild, name, 10000, "dukes-head.intermediate.discipline-other");

        Assert.Equal("ability.the-people-of-sweethaven-have-spoken", basic.AbilityRef);
        Assert.Equal("The People of Sweethaven have spoken", basic.Name);
        Assert.Equal(2, advanced.Count);
        Assert.Equal("ability.discipline", discipline.AbilityRef);
        Assert.Equal("Discipline Other", discipline.Name);
    }

    [Fact]
    public async Task MercenariesUniteAdditionalUseRequiresPurchasedStatus()
    {
        const string name = "The Order of Mercenaries";
        var guild = (await GuildsService.GetAllAsync())[name];
        var grant = ResolveGrant(guild, name, 10000, "order-of-mercenaries.advanced.mercenaries-unite");
        Assert.Equal("ability.use-self-only-beneficial-ability-on-other", grant.AbilityRef);
        Assert.Equal(1, grant.Count);
        Assert.Contains(guild.Details, detail => detail.Value.Contains("Experienced Mercenary"));
    }

    [Fact]
    public async Task GetAllAsync_MapsFrontlineBattleLineAndSplitCompoundGuildBenefitsToCanonicalRefs()
    {
        var all = await GuildsService.GetAllAsync();

        Assert.True(all.TryGetValue("Frontline", out var frontline));
        Assert.NotNull(frontline);

        var frontlineAdvanced = frontline!.Benefits.Advanced
            .Select(entry => entry.Ability)
            .Where(ability => ability != null)
            .ToList();
        Assert.Contains(frontlineAdvanced, ability => string.Equals(ability!.AbilityRef, "ability.the-battle-line", System.StringComparison.OrdinalIgnoreCase));
        Assert.Contains(frontlineAdvanced, ability => string.Equals(ability!.AbilityRef, "ability.immunity-to-repels", System.StringComparison.OrdinalIgnoreCase));
        Assert.Contains(frontlineAdvanced, ability => string.Equals(ability!.AbilityRef, "ability.immunity-to-involuntary-mystical-transportation", System.StringComparison.OrdinalIgnoreCase));
        Assert.Contains(frontlineAdvanced, ability => string.Equals(ability!.AbilityRef, "ability.immunity-to-supernatural-fumble-disarm", System.StringComparison.OrdinalIgnoreCase));

        Assert.True(all.TryGetValue("Wahadamune", out var wahadamune));
        Assert.NotNull(wahadamune);
        var wahadamuneBasic = wahadamune!.Benefits.Basic
            .Select(entry => entry.Ability)
            .Where(ability => ability != null)
            .ToList();
        Assert.Contains(wahadamuneBasic, ability => string.Equals(ability!.AbilityRef, "ability.6-2-stamina.1", System.StringComparison.OrdinalIgnoreCase));
        Assert.Contains(wahadamuneBasic, ability => string.Equals(ability!.AbilityRef, "ability.war-cry", System.StringComparison.OrdinalIgnoreCase));

        Assert.True(all.TryGetValue("Earthsunder", out var earthsunder));
        Assert.NotNull(earthsunder);
        var earthsunderAdvanced = earthsunder!.Benefits.Advanced
            .Select(entry => entry.Ability)
            .Where(ability => ability != null)
            .ToList();
        Assert.Contains(earthsunderAdvanced, ability => string.Equals(ability!.AbilityRef, "ability.6-2-stamina.1", System.StringComparison.OrdinalIgnoreCase));
        Assert.Contains(earthsunderAdvanced, ability => string.Equals(ability!.AbilityRef, "ability.war-cry", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuildsJson_UsesStableGrantIdsAndCanonicalReferences()
    {
        using var document = JsonDocument.Parse(await ServiceHelper.ReadPackageTextAsync("people/guilds.json"));
        Assert.Equal(3, document.RootElement.GetProperty("$schemaVersion").GetInt32());
        foreach (var guild in document.RootElement.GetProperty("guilds").EnumerateObject())
        {
            Assert.False(guild.Value.TryGetProperty("MiracleList", out _));
            foreach (var tier in guild.Value.GetProperty("Benefits").EnumerateObject())
            {
                foreach (var node in tier.Value.EnumerateArray())
                {
                    Assert.False(string.IsNullOrWhiteSpace(node.GetProperty("Id").GetString()));
                    if (!node.TryGetProperty("Kind", out var kind) || kind.GetString() == "Grant")
                    {
                        var abilityRef = node.GetProperty("AbilityRef").GetString();
                        Assert.False(string.IsNullOrWhiteSpace(abilityRef));
                        if (node.TryGetProperty("Key", out var key))
                            Assert.NotEqual(abilityRef, key.GetString());
                    }
                }
            }
        }
    }

    [Fact]
    public async Task MiracleListsAreSeparateAndFirstEligibleMembershipIsPreserved()
    {
        var lists = await GuildMiracleListCatalogue.GetAllAsync();
        Assert.Equal(17, lists.Count);
        Assert.Equal(309, lists.Values.SelectMany(levels => levels.Values).Sum(entries => entries.Count));
        var (guild, list) = GuildMiracleAccessResolver.ResolveMembership(
            ["Shadow Legion", "church of the tryst", "Church of Knowledge"], lists);
        Assert.Equal("church of the tryst", guild);
        Assert.Contains("Halt", list!["1"]);
        Assert.Equal((null, null), GuildMiracleAccessResolver.ResolveMembership(["Shadow Legion"], lists));
    }

    [Theory]
    [InlineData(0, 3, 1)]
    [InlineData(250, 6, 2)]
    [InlineData(1000, 9, 3)]
    public async Task MercenaryLifeSetsOneCumulativeAmount(int points, int body, int locations)
    {
        const string name = "The Order of Mercenaries";
        var guild = (await GuildsService.GetAllAsync())[name];
        var ability = ResolveGrant(guild, name, points, "b.guild-life");
        Assert.Equal(new[] { body, locations }, ability.Amount);
    }

    [Fact]
    public async Task TierMiracleAccessDoesNotMutateThePackagedList()
    {
        const string rings = "The Rings of Talthar";
        var records = await GuildsService.GetAllAsync();
        var ordinary = (await GuildMiracleListCatalogue.GetAllAsync())[rings];
        var additions = GuildCharacterBenefitResolver.ResolveGrants(records[rings], new CharacterDraft(), rings)
            .MiracleAdditions;
        var effective = GuildMiracleAccessResolver.AddOrdinaryAccess(ordinary, additions,
            name => name == "Dismiss Ghoul" ? (name, 3) : null);

        Assert.Contains("Dismiss Ghoul", effective["3"]);
        Assert.DoesNotContain("Dismiss Ghoul", ordinary["3"]);
    }

    private static AbilityDefinition ResolveGrant(GuildRecord guild, string name, int points, string id)
    {
        var result = GuildCharacterBenefitResolver.ResolveGrants(guild, new CharacterDraft { Points = points }, name);
        var grant = Assert.Single(result.Grants, grant => grant.Id == id);
        return GuildCharacterBenefitResolver.MaterializeGrant(guild, grant);
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
