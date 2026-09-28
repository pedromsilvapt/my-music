using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace MyMusic.Common.Services;

/// <summary>
///     In-process <see cref="IUserImportThrottle"/>: one semaphore per user. Must be registered as a singleton so every
///     request shares the same slots.
/// </summary>
public class UserImportThrottle(IOptions<Config> config) : IUserImportThrottle
{
    private readonly ConcurrentDictionary<long, SemaphoreSlim> _userSlots = new();

    public async Task<IDisposable> AcquireAsync(long userId, CancellationToken cancellationToken = default)
    {
        var slots = _userSlots.GetOrAdd(userId, _ =>
        {
            var limit = Math.Max(1, config.Value.MaxConcurrentImportsPerUser);
            return new SemaphoreSlim(limit, limit);
        });

        await slots.WaitAsync(cancellationToken);

        return new Slot(slots);
    }

    private sealed class Slot(SemaphoreSlim slots) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            // Guard against double disposal handing out more slots than the limit
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                slots.Release();
            }
        }
    }
}
