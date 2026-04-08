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

    [Fact]
    public async Task GetAllAsync_NormalizesShadowLegionCanonicalGrantReferencesAndUpgrades()
    {
        var all = await GuildsService.GetAllAsync();

        Assert.True(all.TryGetValue("Shadow Legion", out var guild));
        Assert.NotNull(guild);

        var basicWarCry = guild!.Benefits.Basic
            .Select(entry => entry.Ability)
            .FirstOrDefault(ability =>
                string.Equals(ability?.GrantId, "shadow-legion.basic.war-cry", System.StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(basicWarCry);
        Assert.Equal("ability.war-cry", basicWarCry!.AbilityRef);
        Assert.Equal("Innate", basicWarCry.Type);
        Assert.Equal("Shadow Legion!", basicWarCry.Name);

        var basicFirstAid = guild.Benefits.Basic
            .Select(entry => entry.Ability)
            .FirstOrDefault(ability =>
                string.Equals(ability?.GrantId, "shadow-legion.basic.first-aid", System.StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(basicFirstAid);
        Assert.Equal("ability.bandaging", basicFirstAid!.AbilityRef);

        var intermediateUpgrade = guild.Benefits.Intermediate
            .Select(entry => entry.Ability)
            .FirstOrDefault(ability =>
                string.Equals(ability?.GrantId, "shadow-legion.intermediate.shadow-legion-upgrade", System.StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(intermediateUpgrade);
        Assert.Equal("ability.determination", intermediateUpgrade!.AbilityRef);
        Assert.Equal("Innate", intermediateUpgrade.Type);
        Assert.Equal("Shadow Legion!", intermediateUpgrade.Name);

        var advancedUpgrade = guild.Benefits.Advanced
            .Select(entry => entry.Ability)
            .FirstOrDefault(ability =>
                string.Equals(ability?.GrantId, "shadow-legion.advanced.shadow-legion-extra-use", System.StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(advancedUpgrade);
        Assert.Equal(2, advancedUpgrade!.Count);
        Assert.Equal("ability.determination", advancedUpgrade.AbilityRef);

        var advancedChoiceNames = guild.Benefits.Advanced
            .SelectMany(entry => entry.Options ?? new List<GuildBenefitOption>())
            .SelectMany(option => option.Abilities ?? new List<AbilityDefinition>())
            .Select(ability => (ability.Name ?? string.Empty).Trim())
            .Where(name => name.Length > 0)
            .ToList();

        Assert.Contains("Shadow Legion!", advancedChoiceNames);
        Assert.Contains("Battle Fury", advancedChoiceNames);
        Assert.Contains("Medic", advancedChoiceNames);
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
    public async Task GetAllAsync_MapsDukesHeadSweethavenAndDisciplineOtherToCanonicalRefs()
    {
        var all = await GuildsService.GetAllAsync();

        Assert.True(all.TryGetValue("The Duke's Head Society", out var guild));
        Assert.NotNull(guild);

        var basicGrant = guild!.Benefits.Basic
            .Select(entry => entry.Ability)
            .FirstOrDefault(ability =>
                string.Equals(ability?.GrantId, "dukes-head.basic.the-people-of-sweethaven-have-spoken", System.StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(basicGrant);
        Assert.Equal("ability.the-people-of-sweethaven-have-spoken", basicGrant!.AbilityRef);
        Assert.Equal("The People of Sweethaven have spoken", basicGrant.Name);

        var intermediateUpgrade = guild.Benefits.Intermediate
            .Select(entry => entry.Ability)
            .FirstOrDefault(ability =>
                string.Equals(ability?.GrantId, "dukes-head.intermediate.the-people-of-sweethaven-have-spoken-upgrade", System.StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(intermediateUpgrade);
        Assert.Equal("ability.the-people-of-sweethaven-have-spoken", intermediateUpgrade!.AbilityRef);
        Assert.Equal(2, intermediateUpgrade.Count);

        var disciplineOther = guild.Benefits.Intermediate
            .Select(entry => entry.Ability)
            .FirstOrDefault(ability =>
                string.Equals(ability?.GrantId, "dukes-head.intermediate.discipline-other", System.StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(disciplineOther);
        Assert.Equal("ability.discipline", disciplineOther!.AbilityRef);
        Assert.Equal("Discipline Other", disciplineOther.Name);
    }

    [Fact]
    public async Task GetAllAsync_MapsOrderOfMercenariesUniteToGenericSelfOnlyGrantAndUpgrade()
    {
        var all = await GuildsService.GetAllAsync();

        Assert.True(all.TryGetValue("The Order of Mercenaries", out var guild));
        Assert.NotNull(guild);

        var baseGrant = guild!.Benefits.Advanced
            .Select(entry => entry.Ability)
            .FirstOrDefault(ability =>
                string.Equals(ability?.GrantId, "order-of-mercenaries.advanced.mercenaries-unite", System.StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(baseGrant);
        Assert.Equal("ability.use-self-only-beneficial-ability-on-other", baseGrant!.AbilityRef);
        Assert.Equal(1, baseGrant.Count);

        var upgradedGrant = guild.Benefits.Advanced
            .Select(entry => entry.Ability)
            .FirstOrDefault(ability =>
                string.Equals(ability?.GrantId, "order-of-mercenaries.advanced.mercenaries-unite-extra-use", System.StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(upgradedGrant);
        Assert.Equal("ability.use-self-only-beneficial-ability-on-other", upgradedGrant!.AbilityRef);
        Assert.Equal(2, upgradedGrant.Count);
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
    public async Task GuildsJson_DoesNotLeaveExactCanonicalAliasNameBenefitsUnmapped()
    {
        var abilitiesJson = await ServiceHelper.ReadPackageTextAsync("specialisation/abilities.json");
        using var abilitiesDoc = JsonDocument.Parse(abilitiesJson);
        var aliases = abilitiesDoc.RootElement.GetProperty("aliases")
            .EnumerateObject()
            .Select(property => property.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(System.StringComparer.OrdinalIgnoreCase);

        var guildsJson = await ServiceHelper.ReadPackageTextAsync("people/guilds.json");
        using var guildsDoc = JsonDocument.Parse(guildsJson);

        var offenders = new List<string>();
        foreach (var guild in guildsDoc.RootElement.EnumerateObject())
        {
            if (!guild.Value.TryGetProperty("Benefits", out var benefits) || benefits.ValueKind != JsonValueKind.Object)
                continue;

            foreach (var tier in new[] { "Basic", "Intermediate", "Advanced" })
            {
                if (!benefits.TryGetProperty(tier, out var tierElement))
                    continue;

                CollectUnmappedAliasEntries(guild.Name, tier, tierElement, aliases, offenders);
            }
        }

        Assert.True(offenders.Count == 0, $"Found unmapped exact alias guild benefits: {string.Join("; ", offenders)}");
    }

    [Fact]
    public async Task GuildsJson_BenefitAbilitiesContainKeyAndAbilityRef()
    {
        var guildsJson = await ServiceHelper.ReadPackageTextAsync("people/guilds.json");
        using var guildsDoc = JsonDocument.Parse(guildsJson);

        var offenders = new List<string>();
        foreach (var guild in guildsDoc.RootElement.EnumerateObject())
        {
            if (!guild.Value.TryGetProperty("Benefits", out var benefits) || benefits.ValueKind != JsonValueKind.Object)
                continue;

            foreach (var tier in new[] { "Basic", "Intermediate", "Advanced" })
            {
                if (!benefits.TryGetProperty(tier, out var tierElement))
                    continue;

                CollectMissingIdentityEntries(guild.Name, tier, tierElement, offenders);
            }
        }

        Assert.True(offenders.Count == 0, $"Found guild benefits missing Key/AbilityRef: {string.Join("; ", offenders)}");
    }

    private static void CollectUnmappedAliasEntries(
        string guildName,
        string tier,
        JsonElement element,
        IReadOnlySet<string> aliases,
        ICollection<string> offenders)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
                CollectUnmappedAliasEntries(guildName, tier, child, aliases, offenders);
            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
            return;

        if (!element.TryGetProperty("Name", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
            return;

        var name = (nameElement.GetString() ?? string.Empty).Trim();
        if (name.Length == 0 || !aliases.Contains(name))
            return;

        var hasRef = element.TryGetProperty("AbilityRef", out var refElement)
                     && refElement.ValueKind == JsonValueKind.String
                     && !string.IsNullOrWhiteSpace(refElement.GetString());
        if (hasRef)
            return;

        offenders.Add($"{guildName}/{tier}:{name}");
    }

    private static void CollectMissingIdentityEntries(
        string guildName,
        string tier,
        JsonElement element,
        ICollection<string> offenders)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
                CollectMissingIdentityEntries(guildName, tier, child, offenders);
            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
            return;

        if (!element.TryGetProperty("Name", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
            return;

        var name = (nameElement.GetString() ?? string.Empty).Trim();
        if (name.Length == 0)
            return;

        var hasKey = element.TryGetProperty("Key", out var keyElement)
                     && keyElement.ValueKind == JsonValueKind.String
                     && !string.IsNullOrWhiteSpace(keyElement.GetString());
        var hasRef = element.TryGetProperty("AbilityRef", out var refElement)
                     && refElement.ValueKind == JsonValueKind.String
                     && !string.IsNullOrWhiteSpace(refElement.GetString());

        if (!hasKey || !hasRef)
            offenders.Add($"{guildName}/{tier}:{name}");
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
