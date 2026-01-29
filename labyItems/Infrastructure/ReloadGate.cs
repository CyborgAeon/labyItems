using System;
using System.Threading;

namespace labyItems.Infrastructure;

public sealed class ReloadGate : IDisposable
{
    private readonly object _sync = new();
    private CancellationTokenSource? _cts;
    private int _generation;
    private bool _disposed;

    public ReloadSession Begin(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (_disposed)
                return new ReloadSession(null, CancellationToken.None, 0, disposed: true);

            _generation++;

            var previous = _cts;
            _cts = cancellationToken.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                : new CancellationTokenSource();

            previous?.Cancel();
            previous?.Dispose();

            return new ReloadSession(this, _cts.Token, _generation, disposed: false);
        }
    }

    public void Cancel()
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            _generation++;
            var previous = _cts;
            _cts = null;
            previous?.Cancel();
            previous?.Dispose();
        }
    }

    internal bool IsCurrent(int generation)
        => !_disposed && Volatile.Read(ref _generation) == generation;

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            _disposed = true;
            _generation++;
            var previous = _cts;
            _cts = null;
            previous?.Cancel();
            previous?.Dispose();
        }
    }

    public readonly struct ReloadSession
    {
        private readonly ReloadGate? _owner;
        private readonly int _generation;
        private readonly bool _disposed;

        public CancellationToken Token { get; }

        internal ReloadSession(ReloadGate? owner, CancellationToken token, int generation, bool disposed)
        {
            _owner = owner;
            Token = token;
            _generation = generation;
            _disposed = disposed;
        }

        public bool IsCurrent => !_disposed && _owner != null && _owner.IsCurrent(_generation);
        public bool IsCanceledOrStale => !IsCurrent || Token.IsCancellationRequested;

        public void ThrowIfCancelledOrStale()
        {
            if (!IsCurrent)
                throw new OperationCanceledException(Token);

            Token.ThrowIfCancellationRequested();
        }
    }
}
