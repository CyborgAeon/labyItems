using System.Text.Json;
using System.Text.Json.Serialization;
using labyItems.Models;
using labyItems.Models.Characters;
using labyItems.Models.Guilds;
using labyItems.Services;
using labyItems.Services.Guilds;
using Xunit;

namespace labyItems.Tests;

public sealed class GuildGrantResolverTests : ServiceTestBase
{
    [Theory]
    [InlineData("gold", "ivory")]
    [InlineData("ivory", "gold")]
    public async Task Brood_UsesTheBasicBranchAtAllTiers(string branch, string other)
    {
        var nodes = await LoadAsync("The Brood", 3);
        var result = GuildGrantResolver.Resolve(nodes, new()
        {
            GuildName = "The Brood",
            AdventureSelections = Selections(("b.choice-2", [branch]))
        });

        Assert.Contains(result.Grants, grant => grant.InstanceId.Contains($"b.choice-2/{branch}~1/"));
        Assert.Contains(result.Grants, grant => grant.InstanceId.Contains($"i.choice-2/{branch}~1/"));
        Assert.DoesNotContain(result.Grants, grant => grant.InstanceId.Contains($"/{other}~"));
        Assert.Contains(result.Grants, grant => grant.Id == "a.strength-of-family" || grant.Id == "a.just-like-a-family");
        var upgraded = Assert.Single(result.Grants, grant => grant.Id == (branch == "gold" ? "b.true-believer-gold" : "i.mobility"));
        Assert.Contains(upgraded.Patches, patch => !string.IsNullOrWhiteSpace(patch.Overrides?.Effect));
        if (branch == "gold")
            Assert.Contains(Assert.Single(result.Grants, grant => grant.Id == "b.shield-self").Patches, patch => patch.Count == 8);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 3)]
    [InlineData(3, 5)]
    public async Task CouncilOfJade_ProgressionSetsTotalAllowance(int tiers, int allowance)
    {
        var nodes = await LoadAsync("Council of Jade", tiers);
        var result = GuildGrantResolver.Resolve(nodes, new() { GuildName = "Council of Jade" });

        Assert.Equal(allowance, Assert.Single(result.Choices, choice => choice.Choice.Id == "b.ways-of-the-families").Allowance);
    }

    [Fact]
    public async Task CouncilOfJade_ResolvesSeparateNestedSelectionsForRepeatableCrane()
    {
        var nodes = await LoadAsync("Council of Jade", 3);
        const string choice = "b.ways-of-the-families";
        const string crane = "teachings-of-the-crane";
        string Nested(int occurrence) => $"{choice}/{crane}~{occurrence}/b.{crane}/b.{crane}.items";

        var result = GuildGrantResolver.Resolve(nodes, new()
        {
            GuildName = "Council of Jade",
            AdventureSelections = Selections((choice, [crane, crane]), (Nested(1), ["1-mac"]), (Nested(2), ["1-sac"]))
        });

        Assert.Equal(2, result.Grants.Count(grant => grant.Id == $"b.{crane}"));
        Assert.Contains(result.Grants, grant => grant.Id == "b.1-mac");
        Assert.Contains(result.Grants, grant => grant.Id == "b.1-sac");

        var duplicate = GuildGrantResolver.Resolve(nodes, new()
        {
            GuildName = "Council of Jade",
            AdventureSelections = Selections((choice, [crane, crane]), (Nested(1), ["1-mac"]), (Nested(2), ["1-mac"]))
        });

        Assert.Single(duplicate.Grants, grant => grant.Id == "b.1-mac");
        Assert.Contains(duplicate.Diagnostics, diagnostic => diagnostic.Code == "DuplicateSelection");
    }

    [Fact]
    public async Task ApothecarySociety_KeepsMembershipSelectionOutOfAdventureState()
    {
        var nodes = await LoadAsync("Apothecary Society", 1);
        var result = GuildGrantResolver.Resolve(nodes, new()
        {
            GuildName = "Apothecary Society",
            Selections = Selections(("b.choice-1", ["drinker"])),
            AdventureSelections = Selections(("b.choice-1", ["maker"]))
        });

        var choice = Assert.Single(result.Choices);
        Assert.Equal("OnJoin", choice.Timing);
        Assert.Equal(["drinker"], choice.SelectedOptions);
        Assert.All(result.Grants, grant => Assert.Contains("/drinker~1/", grant.InstanceId));
        Assert.NotEmpty(result.Grants);
    }

    [Fact]
    public async Task KnightsErrant_GrantsBothArmourOptionsAtIntermediate()
    {
        var basic = await LoadAsync("Knights Errant", 1);
        var intermediate = await LoadAsync("Knights Errant", 2);
        var initial = GuildGrantResolver.Resolve(basic, new());
        var initialChoice = Assert.Single(initial.Choices);
        var selections = Selections((initialChoice.SelectionKey, [initialChoice.Options[0].Option.Id]));

        var basicSelected = GuildGrantResolver.Resolve(basic, new() { AdventureSelections = selections });
        var advancedChoice = GuildGrantResolver.Resolve(intermediate, new());
        var selected = Assert.Single(basicSelected.Choices);
        var both = Assert.Single(advancedChoice.Choices);
        Assert.Single(selected.SelectedOptions);
        Assert.Equal(2, both.SelectedOptions.Count);
        Assert.True(both.GrantsAll);
        Assert.Single(basicSelected.Grants, grant => grant.InstanceId.Contains($"{initialChoice.SelectionKey}/"));
        Assert.Equal(2, advancedChoice.Grants.Count(grant => grant.InstanceId.Contains($"{initialChoice.SelectionKey}/")));

        var records = await GuildsService.GetAllAsync();
        var record = records["Knights Errant"];
        var choices = advancedChoice.Grants
            .Where(grant => grant.InstanceId.Contains($"{initialChoice.SelectionKey}/"))
            .SelectMany(grant => AbilityDraftBuilder.ParseAbility(GuildCharacterBenefitResolver.MaterializeGrant(record, grant), null))
            .ToArray();
        Assert.Equal(2, choices.Length);
        Assert.All(choices, grant => Assert.True(grant.HasConditionalArmour));
        var armour = ArmourBonusResolver.ResolveMaxPerSourceTotals(choices);
        Assert.Equal(0, armour.Mac);
        Assert.Equal(0, armour.Sac);
    }

    [Fact]
    public async Task WithoutBanners_DistinguishesUnknownAdventureBracketFromConfirmedAbsence()
    {
        var nodes = await LoadAsync("Without Banners", 2);
        var context = new GuildGrantResolutionContext
        {
            GuildName = "Without Banners",
            AdventureSelections = Selections(("b.draw-on-the-banner", ["fires-of-the-banner"]))
        };

        var unknown = GuildGrantResolver.Resolve(nodes, context);
        var absent = GuildGrantResolver.Resolve(nodes, context with
        {
            ResolveRuleField = field => field == "Adventure.GuildMemberBracket" ? ["Warrior"] : null
        });
        var present = GuildGrantResolver.Resolve(nodes, context with
        {
            ResolveRuleField = field => field == "Adventure.GuildMemberBracket" ? ["Wizard"] : null
        });

        Assert.Equal(GuildGrantEligibility.Unknown, Fire(unknown).Eligibility);
        Assert.Equal(GuildGrantEligibility.Unavailable, Fire(absent).Eligibility);
        Assert.Equal(GuildGrantEligibility.Available, Fire(present).Eligibility);
        Assert.DoesNotContain(unknown.Grants, FireGrant);
        Assert.DoesNotContain(absent.Grants, FireGrant);
        Assert.Contains(present.Grants, FireGrant);

        static GuildGrantChoiceOptionState Fire(GuildGrantResolutionResult result)
            => result.Choices.Single(choice => choice.Choice.Id == "b.draw-on-the-banner")
                .Options.Single(option => option.Option.Id == "fires-of-the-banner");

        static bool FireGrant(ResolvedGuildGrant grant) => grant.Id == "b.draw-on-the-banner.fires-of-the-banner";
    }

    [Fact]
    public async Task MercenariesDefence_ProgressesToFullWarCryWithoutIncreasingUniteUses()
    {
        var nodes = await LoadAsync("The Order of Mercenaries", 3);
        var result = GuildGrantResolver.Resolve(nodes, new() { GuildName = "The Order of Mercenaries" });

        var defence = Assert.Single(result.Grants, grant => grant.Id == "b.mercenaries-defence");
        var unite = Assert.Single(result.Grants, grant => grant.Id == "order-of-mercenaries.advanced.mercenaries-unite");
        Assert.Equal("ability.war-cry", defence.Ability.AbilityRef);
        Assert.Equal(2, defence.Patches.Count);
        Assert.Contains("30 seconds", defence.Patches[0].Overrides?.Effect);
        Assert.Contains("full war cry", defence.Patches[1].Overrides?.Effect);
        Assert.Equal("ability.use-self-only-beneficial-ability-on-other", unite.Ability.AbilityRef);
        Assert.Equal(1, unite.Ability.Count);
        Assert.Empty(unite.Patches);
    }

    [Fact]
    public void Upgrades_UseGrantIdentityEvenWhenCanonicalAbilityIsShared()
    {
        var nodes = new GuildGrantNode[]
        {
            new() { Id = "first", Ability = new AbilityDefinition { AbilityRef = "ability.war-cry", Count = 1 } },
            new() { Id = "second", Ability = new AbilityDefinition { AbilityRef = "ability.war-cry", Count = 1 } },
            new() { Id = "extra", Kind = "Upgrade", Mode = "Patch", TargetGrantId = "second", Patch = new() { CountDelta = 2 } },
            new() { Id = "missing", Kind = "Upgrade", Mode = "Patch", TargetGrantId = "absent", Patch = new() { CountDelta = 1 } }
        };

        var result = GuildGrantResolver.Resolve(nodes, new() { GuildName = "Member Guild" });

        Assert.Empty(Assert.Single(result.Grants, grant => grant.Id == "first").Patches);
        Assert.Equal(2, Assert.Single(Assert.Single(result.Grants, grant => grant.Id == "second").Patches).CountDelta);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "MissingUpgradeTarget");
    }

    [Fact]
    public void LegacySelection_MigratesOnlyAnExplicitMapping()
    {
        const string legacyKey = "Guild::Basic::1";
        var options = new GuildGrantOption[] { new() { Id = "first" }, new() { Id = "second" } };
        var nodes = new GuildGrantNode[]
        {
            new() { Kind = "Choice", Id = "choice", Options = options, LegacySelectionKey = legacyKey, LegacyOptionIds = ["second", "first"] },
            new() { Kind = "Choice", Id = "unmapped", Options = options }
        };
        var result = GuildGrantResolver.Resolve(nodes, new()
        {
            GuildName = "Guild",
            LegacySelections = new Dictionary<string, int> { [legacyKey] = 1, ["Guild::Basic::2"] = 1 }
        });

        Assert.Equal(["second"], result.MigratedSelections["choice"]);
        Assert.Empty(Assert.Single(result.Choices, choice => choice.Choice.Id == "unmapped").SelectedOptions);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "UnmappedLegacySelection");
    }

    [Fact]
    public async Task Brood_UsesBasicLegacyChoiceWhenLaterLegacyTiersDisagree()
    {
        var nodes = await LoadAsync("The Brood", 3);
        var result = GuildGrantResolver.Resolve(nodes, new()
        {
            GuildName = "The Brood",
            LegacySelections = new Dictionary<string, int>
            {
                ["The Brood::Basic::1"] = 1,
                ["The Brood::Intermediate::1"] = 2
            }
        });

        Assert.Equal(["gold"], result.MigratedAdventureSelections["b.choice-2"]);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "ConflictingLegacySelection");
        Assert.DoesNotContain(result.Grants, grant => grant.InstanceId.Contains("/ivory~"));

        var newAdventure = GuildGrantResolver.Resolve(nodes, new()
        {
            GuildName = "The Brood",
            CompletedAdventureMigrations = new HashSet<string> { "b.choice-2" },
            LegacySelections = new Dictionary<string, int> { ["The Brood::Basic::1"] = 1 }
        });
        Assert.Empty(newAdventure.MigratedAdventureSelections);
        Assert.DoesNotContain(newAdventure.Grants, grant => grant.InstanceId.Contains("/gold~"));
    }

    [Fact]
    public void BlankSelectionSlotDoesNotMoveLaterSelectionInThePicker()
    {
        var result = GuildGrantResolver.Resolve([new()
        {
            Kind = "Choice",
            Id = "choice",
            Select = 2,
            Options = [new() { Id = "first" }, new() { Id = "second" }]
        }], new() { Selections = Selections(("choice", ["", "second"])) });

        Assert.Equal(["", "second"], Assert.Single(result.Choices).SelectedOptions);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task LegacyCharacterSnapshot_RoundTripsMembershipAndExpiresAdventureChoices()
    {
        const string membershipGuild = "The White Lotus Society";
        const string adventureGuild = "The Brood";
        const string membershipChoice = "b.choice-3";
        const string adventureChoice = "b.choice-2";
        var draft = Assert.IsType<CharacterDraft>(LiteDbService.ToDraft(new Character
        {
            Id = "legacy-guild-character",
            DraftSnapshot = """
                {
                  "Name": "Legacy Guild Character",
                  "Guilds": ["The White Lotus Society", "The Brood"],
                  "Points": 3000,
                  "GuildBenefitSelections": {
                    "The White Lotus Society::Basic::1": 2,
                    "The Brood::Basic::1": 1
                  }
                }
                """
        }));
        var records = await GuildsService.GetAllAsync();
        foreach (var guild in draft.Guilds)
        {
            var resolution = GuildCharacterBenefitResolver.ResolveGrants(records[guild], draft, guild);
            GuildCharacterBenefitResolver.ApplyVerifiedMigrations(draft, guild, resolution);
        }

        Assert.Equal(["heart-of-the-lotus"], draft.GuildGrantSelections[membershipGuild][membershipChoice]);
        Assert.Equal(["gold"], draft.AdventureGuildGrantSelections[adventureGuild][adventureChoice]);

        var directory = Directory.CreateTempSubdirectory("laby-guild-snapshot-");
        var originalProvider = LiteDbService.PersonalDatabasePathProvider;
        try
        {
            LiteDbService.PersonalDatabasePathProvider = () => Path.Combine(directory.FullName, "characters.db");
            var stored = LiteDbService.UpsertDraft(draft);
            var loaded = Assert.IsType<Character>(LiteDbService.GetCharacterById(stored.Id));
            var restored = Assert.IsType<CharacterDraft>(LiteDbService.ToDraft(loaded));

            Assert.Equal(["heart-of-the-lotus"], restored.GuildGrantSelections[membershipGuild][membershipChoice]);
            Assert.Empty(restored.AdventureGuildGrantSelections);
            Assert.Contains(adventureChoice, restored.CompletedGuildAdventureMigrations[adventureGuild]);
            Assert.Equal(1, restored.GuildBenefitSelections["The Brood::Basic::1"]);

            var resolvedMembership = GuildCharacterBenefitResolver.ResolveGrants(records[membershipGuild], restored, membershipGuild);
            var resolvedAdventure = GuildCharacterBenefitResolver.ResolveGrants(records[adventureGuild], restored, adventureGuild);
            Assert.Empty(resolvedMembership.MigratedSelections);
            Assert.Contains(resolvedMembership.Grants, grant => grant.Id == "b.heart-of-the-lotus");
            Assert.Empty(resolvedAdventure.MigratedAdventureSelections);
            Assert.DoesNotContain(resolvedAdventure.Grants, grant => grant.InstanceId.Contains("/gold~"));
        }
        finally
        {
            LiteDbService.PersonalDatabasePathProvider = originalProvider;
            directory.Delete(recursive: true);
        }
    }

    private static Dictionary<string, IReadOnlyList<string>> Selections(params (string Id, string[] Options)[] selections)
        => selections.ToDictionary(pair => pair.Id, pair => (IReadOnlyList<string>)pair.Options);

    private static async Task<GuildGrantNode[]> LoadAsync(string guild, int tiers)
    {
        var json = await ServiceHelper.ReadPackageTextAsync("people/guilds.json");
        using var document = JsonDocument.Parse(json);
        var benefits = document.RootElement.GetProperty("guilds").GetProperty(guild).GetProperty("Benefits");
        string[] tierNames = ["Basic", "Intermediate", "Advanced"];
        var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
        return tierNames.Take(tiers)
            .Where(tier => benefits.TryGetProperty(tier, out _))
            .SelectMany(tier => benefits.GetProperty(tier).EnumerateArray())
            .Select(node => GuildGrantNode.Parse(node, options))
            .ToArray();
    }
}
