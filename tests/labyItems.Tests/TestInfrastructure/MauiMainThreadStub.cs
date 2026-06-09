namespace Microsoft.Maui.ApplicationModel;

public interface IAppInfo
{
    string VersionString { get; }
    string BuildString { get; }
}

public static class AppInfo
{
    public static IAppInfo Current { get; set; } = new TestAppInfo();

    private sealed class TestAppInfo : IAppInfo
    {
        public string VersionString => "test";
        public string BuildString => "0";
    }
}

public static class MainThread
{
    public static bool IsMainThread => true;

    public static Task InvokeOnMainThreadAsync(Action action)
    {
        action?.Invoke();
        return Task.CompletedTask;
    }

    public static Task<T> InvokeOnMainThreadAsync<T>(Func<T> action)
        => Task.FromResult(action());
}
