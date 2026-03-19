namespace Microsoft.Maui.ApplicationModel;

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
