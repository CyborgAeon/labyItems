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
}
