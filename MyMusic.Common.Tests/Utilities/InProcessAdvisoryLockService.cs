using System.Collections.Concurrent;
using MyMusic.Common.Services;

namespace MyMusic.Common.Tests.Utilities;

/// <summary>
///     <see cref="IAdvisoryLockService"/> backed by in-process semaphores, standing in for PostgreSQL advisory locks
///     (which SQLite does not have). Locks are released when the handle is disposed. Tests can also hold keys
///     themselves, to play the role of a concurrent import.
/// </summary>
public class InProcessAdvisoryLockService : IAdvisoryLockService
{
    private readonly ConcurrentDictionary<AdvisoryLockKey, SemaphoreSlim> _locks = new();

    private readonly ConcurrentDictionary<AdvisoryLockKey, TaskCompletionSource> _waiters = new();

    /// <summary>The normalized keys of every acquisition, in call order.</summary>
    public ConcurrentQueue<IReadOnlyList<AdvisoryLockKey>> Acquisitions { get; } = new();

    public async Task<IAsyncDisposable> AcquireTransactionLocksAsync(MusicDbContext db,
        IEnumerable<AdvisoryLockKey> keys, CancellationToken cancellationToken = default)
    {
        var normalized = AdvisoryLockKey.Normalize(keys);
        Acquisitions.Enqueue(normalized);

        return await AcquireAsync(normalized, cancellationToken);
    }

    /// <summary>Holds <paramref name="key"/> until the returned handle is disposed.</summary>
    public Task<IAsyncDisposable> HoldAsync(AdvisoryLockKey key) => AcquireAsync([key], CancellationToken.None);

    /// <summary>Completes once some acquisition is blocked waiting for <paramref name="key"/>.</summary>
    public Task WaitingFor(AdvisoryLockKey key) =>
        _waiters.GetOrAdd(key, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).Task;

    private async Task<IAsyncDisposable> AcquireAsync(IReadOnlyList<AdvisoryLockKey> keys,
        CancellationToken cancellationToken)
    {
        var acquired = new List<SemaphoreSlim>();

        try
        {
            foreach (var key in keys)
            {
                var keyLock = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

                if (!keyLock.Wait(0))
                {
                    _waiters.GetOrAdd(key, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously))
                        .TrySetResult();

                    await keyLock.WaitAsync(cancellationToken);
                }

                acquired.Add(keyLock);
            }
        }
        catch
        {
            acquired.ForEach(keyLock => keyLock.Release());
            throw;
        }

        return new Handle(acquired);
    }

    private sealed class Handle(List<SemaphoreSlim> acquired) : IAsyncDisposable
    {
        private int _released;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                acquired.ForEach(keyLock => keyLock.Release());
            }

            return ValueTask.CompletedTask;
        }
    }
}
