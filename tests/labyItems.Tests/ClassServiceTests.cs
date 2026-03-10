using System.Linq;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class ClassServiceTests : ServiceTestBase
{
    [Fact]
    public async Task GetAllAsync_LoadsClassDefinitions()
    {
        var all = await ClassService.GetAllAsync();

        Assert.NotEmpty(all);
        Assert.Contains(all, kvp => kvp.Value != null && kvp.Value.Levels.Count > 0);
    }
}
