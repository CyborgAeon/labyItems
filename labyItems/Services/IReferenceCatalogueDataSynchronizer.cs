namespace labyItems.Services;

public interface IReferenceCatalogueDataSynchronizer
{
    Task EnsureCurrentAsync(string dbPath, CancellationToken cancellationToken = default);
}
