namespace labyItems.Tests;

public abstract class ServiceTestBase
{
    protected ServiceTestBase()
    {
        ServiceTestEnvironment.EnsureInitialized();
        ServiceCacheResetter.ResetAll();
    }
}
