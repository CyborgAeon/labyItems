using labyItems.Models.WorldMap;
using labyItems.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace labyItems.Tests;

public sealed class WorldRegionRepositoryTests : ServiceTestBase
{
    [Fact]
    public async Task MonthlySeedIsIdempotentAndPreservesHistoricalMonths()
    {
        var path = Path.Combine(Path.GetTempPath(), $"world-{Guid.NewGuid():N}.db");
        try
        {
            await WorldRegionRepository.SeedAsync(path);
            using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "INSERT INTO WorldStatistic VALUES (2025,12,'Alguz','Administration','Fealty',7,0,1)";
                command.ExecuteNonQuery();
            }
            await WorldRegionRepository.SeedAsync(path);
            var repository = new WorldRegionRepository(path);
            Assert.Equal(new[] { new WorldStatsPeriod(2026, 11), new WorldStatsPeriod(2025, 12) }, repository.GetPeriods());
            Assert.Equal(20, repository.GetStatisticsRegions().Count);
            Assert.Equal(7m, Assert.Single(repository.GetStatistics("Alguz", new(2025, 12))).Value);
            var alguz = repository.GetStatistics("Alguz", new(2026, 11));
            Assert.Equal(30, alguz.Count);
            Assert.Equal(15m, alguz.Single(s => s.Name == "Volksmight").Value);
            Assert.Equal(100m, alguz.Single(s => s.Name == "Talthar").Value);
            Assert.Empty(repository.GetStatistics("Alguz", new(2026, 10)));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ExplicitSvgMappingsResolveSourceSpellingAndDoNotInventCompositeValues()
    {
        var repository = new WorldRegionRepository("unused");
        var regions = await repository.GetRegionsAsync();
        Assert.Equal(70, regions.Count);
        var svg = System.Xml.Linq.XDocument.Load(Path.Combine(ServiceTestEnvironment.RepoRoot, "labyItems", "Resources", "Images", "primus-overlay.svg"));
        var pathIds = svg.Descendants().Where(e => e.Attribute("data-type") != null).Select(e => (string)e.Attribute("id")!).OrderBy(id => id).ToArray();
        Assert.Equal(pathIds, regions.Select(r => r.Id).OrderBy(id => id));
        Assert.Equal("Rodane", regions.Single(r => r.Id == "loc-rodanne").StatisticsId);
        Assert.Equal("Mordreds Rest", regions.Single(r => r.Id == "loc-mordreds-rest").StatisticsId);
        Assert.Null(regions.Single(r => r.Id == "region-alguz").StatisticsId);
        Assert.Equal(new[] { "Alguz" }, regions.Single(r => r.Id == "region-alguz").RelatedStatisticsIds);
        Assert.Null(regions.Single(r => r.Id == "loc-gileenim").StatisticsId);
        Assert.Equal(regions.Count, regions.Select(r => r.Id).Distinct().Count());
    }

    [Fact]
    public async Task EveryMappedPlaceExistsAndSourceCollectionsHaveTheirOwnEntries()
    {
        var path = Path.Combine(Path.GetTempPath(), $"world-{Guid.NewGuid():N}.db");
        try
        {
            await WorldRegionRepository.SeedAsync(path);
            var repository = new WorldRegionRepository(path);
            foreach (var region in await repository.GetRegionsAsync())
                {
                    if (region.StatisticsId is not null) Assert.Contains(region.StatisticsId, repository.GetStatisticsRegions());
                    foreach (var place in region.RelatedStatisticsIds) Assert.Contains(place, repository.GetStatisticsRegions());
                }
            foreach (var name in repository.GetStatisticsRegions())
            {
                var stats = repository.GetStatistics(name, new(2026, 11));
                Assert.Equal(8, stats.Count(s => s.Category == "Administration"));
                Assert.Equal(14, stats.Count(s => s.Category == "Dragons"));
            }
            Assert.NotEqual(
                repository.GetStatistics("Alguz", new(2026, 11)).Count(s => s.Category == "Deities and Orders"),
                repository.GetStatistics("Bildteve", new(2026, 11)).Count(s => s.Category == "Deities and Orders"));
        }
        finally { File.Delete(path); }
    }
}
