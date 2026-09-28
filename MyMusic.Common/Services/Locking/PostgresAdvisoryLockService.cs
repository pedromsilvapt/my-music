using Microsoft.EntityFrameworkCore;

namespace MyMusic.Common.Services;

/// <summary>
///     <see cref="IAdvisoryLockService"/> backed by PostgreSQL transaction-level advisory locks
///     (<c>pg_advisory_xact_lock</c>). PostgreSQL releases them automatically on commit or rollback, so they cannot leak
///     when a request fails or is cancelled.
/// </summary>
public class PostgresAdvisoryLockService : IAdvisoryLockService
{
    public async Task<IAsyncDisposable> AcquireTransactionLocksAsync(MusicDbContext db,
        IEnumerable<AdvisoryLockKey> keys, CancellationToken cancellationToken = default)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Advisory transaction locks require an active database transaction; outside of one they would be released immediately.");
        }

        foreach (var key in AdvisoryLockKey.Normalize(keys))
        {
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({key.ClassId}, {key.ObjectId})",
                cancellationToken);
        }

        return NoopAsyncDisposable.Instance;
    }

    private sealed class NoopAsyncDisposable : IAsyncDisposable
    {
        public static readonly NoopAsyncDisposable Instance = new();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
