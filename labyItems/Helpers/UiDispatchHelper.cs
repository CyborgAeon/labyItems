using Microsoft.Maui.ApplicationModel;

namespace labyItems.Helpers;

internal static class UiDispatchHelper
{
    public static void BeginOnMainThread(Action action)
    {
        if (action == null)
            return;

        if (MainThread.IsMainThread)
        {
            action();
            return;
        }

        MainThread.BeginInvokeOnMainThread(action);
    }

    public static void RunFireAndForget(Func<Task> work, string operation)
    {
        _ = RunFireAndForgetCoreAsync(work, operation);
    }

    private static async Task RunFireAndForgetCoreAsync(Func<Task> work, string operation)
    {
        if (work == null)
            return;

        try
        {
            await work().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            RuntimeLog.Write(
                operation,
                $"Unhandled fire-and-forget operation failure in '{operation}'.",
                ex);
        }
    }
}
