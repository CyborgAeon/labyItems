using System.Linq;
using System.Text.Json;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class DruidEvocationServiceTests : ServiceTestBase
{
    [Fact]
    public void EvocRaw_DefaultSerialization_PreservesCurrentAndLegacyImmunityProperties()
    {
        var source = new DruidEvocationService.EvocRaw
        {
            name = "Test evocation",
            immunity = "current immunity",
            ImmunityCompat = "legacy immunity"
        };

        var json = JsonSerializer.Serialize(source);
        var clone = JsonSerializer.Deserialize<DruidEvocationService.EvocRaw>(json);

        Assert.NotNull(clone);
        Assert.Equal("current immunity", clone.immunity);
        Assert.Equal("legacy immunity", clone.ImmunityCompat);
    }

    [Fact]
    public async Task GetAllAsync_LoadsPackagedEvocations()
    {
        var all = await DruidEvocationService.GetAllAsync();

        Assert.NotEmpty(all);
        Assert.All(all.Take(10), e => Assert.False(string.IsNullOrWhiteSpace(e.name)));
    }
}
