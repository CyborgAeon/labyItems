namespace labyItems.Services;

/// <summary>
/// Generic debounce implementation for async operations.
/// Useful for debouncing user input like search queries.
/// AOT-compatible: No reflection used.
/// </summary>
public sealed class DebouncedAsyncAction
{
    private CancellationTokenSource? _cts;
    private readonly int _delayMs;
    private readonly Func<CancellationToken, Task> _action;

    public DebouncedAsyncAction(int delayMs, Func<CancellationToken, Task> action)
    {
        if (delayMs < 0)
            throw new ArgumentOutOfRangeException(nameof(delayMs));

        _delayMs = delayMs;
        _action = action ?? throw new ArgumentNullException(nameof(action));
    }

    /// <summary>
    /// Triggers the debounced action. If called again before the previous delay completes,
    /// the previous execution is cancelled and a new one is scheduled.
    /// </summary>
    public void Trigger()
    {
        // Cancel any pending executions
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        var token = _cts.Token;

        // Schedule the action with a delay
        _ = Task.Delay(_delayMs, token).ContinueWith(
            async _ =>
            {
                if (!token.IsCancellationRequested)
                {
                    try
                    {
                        await _action(token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when debounce is re-triggered
                    }
                    catch (Exception ex)
                    {
                        ServiceHelper.LogDbError("DebouncedAsyncAction", ex);
                    }
                }
            },
            TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>
    /// Cancels any pending execution.
    /// </summary>
    public void Cancel()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    /// <summary>
    /// Immediately executes the action without waiting for the debounce delay.
    /// </summary>
    public async Task ExecuteImmediatelyAsync(CancellationToken cancellationToken = default)
    {
        Cancel();
        await _action(cancellationToken);
    }
}

/// <summary>
/// Debounce implementation for synchronous operations.
/// </summary>
public sealed class DebouncedAction
{
    private CancellationTokenSource? _cts;
    private readonly int _delayMs;
    private readonly Action _action;

    public DebouncedAction(int delayMs, Action action)
    {
        if (delayMs < 0)
            throw new ArgumentOutOfRangeException(nameof(delayMs));

        _delayMs = delayMs;
        _action = action ?? throw new ArgumentNullException(nameof(action));
    }

    /// <summary>
    /// Triggers the debounced action. If called again before the previous delay completes,
    /// the previous execution is cancelled and a new one is scheduled.
    /// </summary>
    public void Trigger()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        var token = _cts.Token;

        _ = Task.Delay(_delayMs, token).ContinueWith(
            _ =>
            {
                if (!token.IsCancellationRequested)
                {
                    try
                    {
                        _action();
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected
                    }
                    catch (Exception ex)
                    {
                        ServiceHelper.LogDbError("DebouncedAction", ex);
                    }
                }
            },
            TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>
    /// Cancels any pending execution.
    /// </summary>
    public void Cancel()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    /// <summary>
    /// Immediately executes the action without waiting for the debounce delay.
    /// </summary>
    public void ExecuteImmediately()
    {
        Cancel();
        _action();
    }
}
