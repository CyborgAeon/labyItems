namespace labyItems.Services;

/// <summary>Runtime hooks installed before login; tests can use their own isolated stores.</summary>
public static class AccountDataAccess
{
    public static AccountWorkspace? Workspace { get; set; }
    public static Func<string>? CacheDirectoryProvider { get; set; }
    public static IDisposable BeginWrite()
    {
        if (Workspace == null) return new WriteLease(null);
        return Workspace.BeginWrite();
    }
    private sealed class WriteLease(AccountWorkspace? workspace) : IDisposable
    {
        public void Dispose() => workspace?.Checkpoint();
    }
}
