namespace labyItems.Services;

public interface IDatabaseInitializer
{
	Task InitializeAsync(CancellationToken cancellationToken = default);
}
