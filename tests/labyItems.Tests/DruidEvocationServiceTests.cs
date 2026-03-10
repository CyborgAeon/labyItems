using System.Linq;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class DruidEvocationServiceTests : ServiceTestBase
{
    [Fact]
    public async Task GetAllAsync_LoadsPackagedEvocations()
    {
        var all = await DruidEvocationService.GetAllAsync();

        Assert.NotEmpty(all);
        Assert.All(all.Take(10), e => Assert.False(string.IsNullOrWhiteSpace(e.name)));
    }
}
