using System.Linq;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class SpecialisationServiceTests : ServiceTestBase
{
    [Fact]
    public async Task GetAllAsync_LoadsSpecialisationsAndParsesColourAbilityBlocks()
    {
        var all = await SpecialisationService.GetAllAsync();

        Assert.NotEmpty(all);
        Assert.Contains(all.Values, record => record.ColourAbilities is { Count: > 0 });
    }
}
