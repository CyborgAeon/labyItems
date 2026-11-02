using labyItems.Models.WorldMap;

namespace labyItems.Services;

public interface IWorldRegionRepository
{
    Task<IReadOnlyList<WorldRegion>> GetRegionsAsync();
    IReadOnlyList<WorldStatsPeriod> GetPeriods();
    IReadOnlyList<WorldStatistic> GetStatistics(string statisticsId, WorldStatsPeriod period);
    IReadOnlyList<string> GetStatisticsRegions();
}
