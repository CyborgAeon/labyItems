using labyItems.Models.Characters;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class CharacterSpendServiceTests
{
    private readonly CharacterSpendService _service = new();

    [Fact]
    public void GetOrCreateSelected_SeedsOnlyChosenCreationAbilitiesWithLevelLabels()
    {
        var draft = new CharacterDraft
        {
            Abilities =
            [
                new AbilityDraft { Name = "1st Weapon Mastery", AbilityKey = "weapon-mastery", Source = "Specialisation: Warrior", LevelGained = 1 },
                new AbilityDraft { Name = "See Through Darkness", Source = "Race", LevelGained = 1 }
            ]
        };

        var spend = _service.GetOrCreateSelected(draft);
        var row = Assert.Single(_service.BuildRows(spend));

        Assert.Equal("1st Weapon Mastery", row.Name);
        Assert.Equal("lvl 1", row.CostText);
        Assert.Equal("0", row.TotalText);
    }

    [Fact]
    public void AddEntry_RequiresSequentialLevelsForEachMultiPathIndependently()
    {
        var spend = new CharacterSpend { Name = "Test" };
        _service.AddEntry(spend, MultiClass("warrior", 1));
        _service.AddEntry(spend, MultiClass("scout", 1));
        _service.AddEntry(spend, MultiClass("warrior", 2));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            _service.AddEntry(spend, MultiClass("warrior", 4)));

        Assert.Contains("expected level 3", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MoveToPosition_MovesSelectionAsStableBlock()
    {
        var spend = new CharacterSpend
        {
            Name = "Test",
            Entries =
            [
                Ability("a", 1),
                Ability("b", 2),
                Ability("c", 3),
                Ability("d", 4),
                Ability("e", 5)
            ]
        };

        _service.MoveToPosition(spend, ["b", "d"], 1);

        Assert.Equal(["b", "d", "a", "c", "e"], spend.Entries.Select(entry => entry.Id));
        Assert.Equal([1, 2, 3, 4, 5], spend.Entries.Select(entry => entry.Position));
    }

    [Fact]
    public void MoveToTop_AllowsAVisibleMultiClassLevelToBeReorderedIndependently()
    {
        var spend = new CharacterSpend
        {
            Name = "Test",
            Entries =
            [
                Ability("ability", 1),
                new CharacterSpendEntry { Id = "level-1", Position = 2, Type = CharacterSpendEntryType.MultiClass, Key = "armour-master", Name = "Armour Master level 1", PurchaseLevel = 1 },
                new CharacterSpendEntry { Id = "level-2", Position = 3, Type = CharacterSpendEntryType.MultiClass, Key = "armour-master", Name = "Armour Master level 2", PurchaseLevel = 2 }
            ]
        };

        _service.MoveToTop(spend, ["level-2"]);

        Assert.Equal(["level-2", "ability", "level-1"], spend.Entries.Select(entry => entry.Id));
        Assert.Equal([1, 2, 3], spend.Entries.Select(entry => entry.Position));
    }

    [Fact]
    public void BuildRows_AccumulatesPaidCostsButNotCreationChoiceLevels()
    {
        var spend = new CharacterSpend
        {
            Name = "Test",
            Entries =
            [
                new CharacterSpendEntry { Id = "choice", Position = 1, Type = CharacterSpendEntryType.Ability, Name = "Mastery", Cost = 0, CharacterLevel = 2, IsCharacterCreationChoice = true },
                new CharacterSpendEntry { Id = "paid", Position = 2, Type = CharacterSpendEntryType.Ability, Name = "Militant", Cost = 40 }
            ]
        };

        var rows = _service.BuildRows(spend);

        Assert.Equal("lvl 2", rows[0].CostText);
        Assert.Equal("0", rows[0].TotalText);
        Assert.Equal("40", rows[1].CostText);
        Assert.Equal("40", rows[1].TotalText);
    }

    private static CharacterSpendEntry MultiClass(string key, int level)
        => new()
        {
            Type = CharacterSpendEntryType.MultiClass,
            Key = key,
            Name = key,
            PurchaseLevel = level,
            Cost = level * 100
        };

    private static CharacterSpendEntry Ability(string id, int position)
        => new()
        {
            Id = id,
            Position = position,
            Type = CharacterSpendEntryType.Ability,
            Key = id,
            Name = id
        };
}
